[<RequireQualifiedAccess>]
module Invoice

open System
open Fable.FontAwesome
open Fable.React.Props
open Fulma
open Shared
open Shared.Invoice
open Utils
open Fable.React
open Fable.Core.JsInterop
open Elmish
open FsToolkit.ErrorHandling
open Api
open Common

let monthSelectPlugin (x: obj): obj -> obj =
    importDefault "flatpickr/dist/plugins/monthSelect"

type Model =
    { ManDays: Validated<uint8>
      Rate: Validated<uint16>
      OrderNumber: Validated<string option>
      AccountingPeriod: DateTime
      SelectedCustomer: Customer option
      Customers: Deferred<Customer list>
      IsReadOnly: bool
      CreatingCustomer: bool
      AddingInvoice: Deferred<unit>
      Result: Result<string, string list> option
      CustomerModel: Customer.Model }

let isValid input =
    let { Model.ManDays = mandays
          Rate = rate
          OrderNumber = orderNumber } =
        input

    match (mandays.Parsed, rate.Parsed, orderNumber.Parsed) with
    | Ok _, Ok _, Ok _ -> true
    | _, _, _ -> false

let isModelValid model =
    isValid model
    && Option.isSome model.SelectedCustomer

type Msg =
    | AddInvoice of AsyncOperationStatus<Invoice.Invoice * Result<Guid, string>>
    | SetRate of string
    | SetAccPeriod of DateTime
    | SetManDays of string
    | SetOrderNumber of string
    | SelectCustomer of string
    | HandleException of Exception
    | LoadCustomers of AsyncOperationStatus<Result<Customer list, string>>
    | BeginCreateCustomer
    | CustomerMsg of Customer.Msg
    | Select of string

let init () =
    let submodel, cmd = Customer.init ()

    { Rate = Validated.success "6000" <| uint16 6000
      ManDays = Validated.success "20" 20uy
      AccountingPeriod = DateTime.Today.AddMonths(-1).Date
      OrderNumber =
          Validated.success "17Zak00002"
          <| Some "17Zak00002"
      Customers = HasNotStartedYet
      AddingInvoice = HasNotStartedYet
      IsReadOnly = false
      Result = None
      CustomerModel = submodel
      SelectedCustomer = None
      CreatingCustomer = false },


    Cmd.batch [ Cmd.map CustomerMsg cmd
                Cmd.ofMsg (LoadCustomers Started) ]

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    let modelInvoiceInput = model

    match msg with
    | AddInvoice (Finished (_, result)) ->
        { model with
              Result =
                  match result with
                  | Ok a -> Some(Ok "Success")
                  | Error s -> Some(Error [ s ])
              IsReadOnly = false },
        Cmd.none
    | AddInvoice Started ->
        let inv =
            validation {

                let! customer =
                    match model.SelectedCustomer with
                    | Some c -> Ok c
                    | None -> Error "No customer selected."

                and! rateParsed = modelInvoiceInput.Rate.Parsed

                and! manDaysParsed = modelInvoiceInput.ManDays.Parsed

                and! orderNumber = modelInvoiceInput.OrderNumber.Parsed

                return
                    { Id = Guid.NewGuid()
                      Customer = customer
                      Rate = rateParsed
                      ManDays = manDaysParsed
                      AccountingPeriod = modelInvoiceInput.AccountingPeriod
                      OrderNumber = orderNumber }
            }

        match inv with
        | Ok inv ->
            let model =
                { model with
                      IsReadOnly = true
                      Result = None }

            let cmd =
                Cmd.OfAsync.either invoiceApi.addInvoice inv (fun r -> Finished(inv, r) |> AddInvoice)
                    (exceptionToResult
                     >> (fun r -> Finished(inv, r) |> AddInvoice))

            model, cmd
        | Error str -> { model with Result = Some(Error str) }, Cmd.none
    | SetRate v ->
        let rate =
            match UInt16.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v [ "Must be whole positive number less than 32768." ]

        { model with Rate = rate }, Cmd.none
    | SetAccPeriod v -> { model with AccountingPeriod = v }, Cmd.none
    | SetManDays v ->
        let md =
            match Byte.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v [ "Must be whole number 0-255." ]

        { model with ManDays = md }, Cmd.none
    | SetOrderNumber v ->
        let orderNumber =
            if String.IsNullOrWhiteSpace(v) then None else Some v

        { model with
              OrderNumber = Validated.success v orderNumber },
        Cmd.none
    | HandleException exn ->
        { model with
              IsReadOnly = false
              Result = Some(Error [ exn.Message ]) },
        Cmd.none
    | BeginCreateCustomer ->
        //model,Cmd.none
        { model with CreatingCustomer = true }, Cmd.ofMsg (CustomerMsg(Customer.Start model.SelectedCustomer))
    | CustomerMsg msg ->
        match msg with
        | Customer.Finish c ->
            let model =
                { model with
                      CreatingCustomer = false
                      SelectedCustomer = Some c }

            let custs =
                match model.Customers with
                | Resolved custs ->
                    let existing = custs |> List.tryFind (fun e -> c = e)

                    match existing with
                    | Some _ -> custs
                    | None -> c :: custs
                | _ -> [ c ]

            { model with
                  Customers = Resolved custs },
            Cmd.none
        | _ ->
            let newSubModel, cmd = Customer.update msg model.CustomerModel

            { model with
                  CustomerModel = newSubModel },
            Cmd.map CustomerMsg cmd
    | SelectCustomer str ->
        let model =
            match model.Customers, (String.IsNullOrWhiteSpace str) with
            | Resolved custs, false ->
                { model with
                      SelectedCustomer = Some custs.[(int str)] }
            | _, _ -> { model with SelectedCustomer = None }

        model, Cmd.none
    | LoadCustomers Started ->
        { model with Customers = InProgress },
        Cmd.OfAsync.either
            invoiceApi.getCustomers
            ()
            (Finished >> LoadCustomers)
            (exceptionToResult >> Finished >> LoadCustomers)
    | LoadCustomers (Finished result) ->
        { model with
              Customers =
                  match result with
                  | Ok cs -> Resolved cs
                  | Error _ -> Resolved []
              SelectedCustomer =
                  match result with
                  | Ok cs when cs.Length > 0 -> Some cs.Head
                  | _ -> None
              Result =
                  match result with
                  | Ok _ -> None
                  | Error s -> Some(Error [ s ]) },
        Cmd.none
    | Select s -> model, Cmd.ofMsg (SetOrderNumber s)





type Props = { Model: Model; Dispatch: Msg -> unit }
let elmishView name render =
    FunctionComponent.Of(render, name, equalsButFunctions)

open Fable.React.Standard

let view =
    elmishView "InvoiceForm"
    <| fun { Props.Model = model
             Dispatch = dispatch } ->
        Box.box' [] [
            fieldset [ HTMLAttr.ReadOnly model.IsReadOnly ] [
                createTextField
                    "Man-day rate"
                    model.Rate
                    [ Input.Placeholder "Man-day rate in CZK"
                      Input.OnChange(fun x -> SetRate x.Value |> dispatch) ]

                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [ str "Month of year" ]
                        Flatpickr.flatpickr [ Flatpickr.OnChange(SetAccPeriod >> dispatch)
                                              Flatpickr.Value
                                                  (System.DateTime
                                                      (int model.AccountingPeriod.Year,
                                                       int model.AccountingPeriod.Month,
                                                       1))
                                              Flatpickr.DateFormat "F Y"
                                              Flatpickr.custom
                                                  "plugins"
                                                  [| monthSelectPlugin
                                                      ({| shorthand = true
                                                          dateFormat = "F Y"
                                                          altFormat = "F Y" |}) |]

                                                  true
                                              Flatpickr.Locale Flatpickr.Locales.czech
                                              Flatpickr.ClassName "input" ]
                    ]
                ]
                createTextField
                    "Total number of man days"
                    model.ManDays
                    [ Input.Placeholder "Total number of man days"
                      Input.OnChange(fun x -> SetManDays x.Value |> dispatch) ]
                createTextField
                    "Order number"
                    model.OrderNumber
                    [ Input.Placeholder "Order number"
                      Input.OnChange(fun x -> SetOrderNumber x.Value |> dispatch) ]

                AutoComplete.textBox { Search= invoiceApi.searchOrderNumber; Dispatch = Select >> dispatch; DebounceTimeout=400 }

                Field.div [ Field.IsExpanded ] [
                    Label.label [] [ str "Customer" ]
                    Field.div [ Field.HasAddons ] [
                        Control.div [ Control.IsExpanded ] [
                            Select.select [ Select.IsFullWidth
                                            Select.IsLoading(model.Customers = InProgress) ] [
                                select [ OnChange(fun e -> dispatch <| SelectCustomer e.Value) ] [
                                    match model.SelectedCustomer with
                                    | None -> yield option [ Value("") ] [ str "" ]
                                    | _ -> ()
                                    match model.Customers with
                                    | Resolved custs ->
                                        for i = 0 to custs.Length - 1 do
                                            let c = custs.[i]

                                            yield
                                                option [ Value(i)
                                                         Selected(model.SelectedCustomer = Some c) ] [
                                                    str <| sprintf "%s (%i)" c.Name c.IdNumber
                                                ]
                                    | _ -> ()
                                ]
                            ]
                        ]
                        Control.p [] [
                            Button.a [ Button.Color IsPrimary
                                       Button.OnClick(fun _ -> dispatch BeginCreateCustomer) ] [
                                Icon.icon [] [
                                    Fa.i [ Fa.Solid.Plus ] []
                                ]
                            ]
                        ]
                    ]
                ]
                Field.div [ Field.IsGrouped ] [
                    Control.p [] [
                        Button.a [ Button.Color IsPrimary
                                   Button.IsLoading model.IsReadOnly
                                   //Button.Disabled(isModelValid model |> not)
                                   Button.OnClick(fun _ -> dispatch (AddInvoice Started)) ] [
                            str "Add"
                        ]
                    ]
                ]
                Common.notification model.Result
            ]
            Customer.view
                { Model = model.CustomerModel
                  Dispatch = (dispatch << CustomerMsg) }
        ]
