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
      Rate: Validated<uint32>
      OrderNumber: Validated<string option>
      AccountingPeriod: DateTime
      SelectedCustomer: Customer option
      VatApplicable: bool
      Vat: Validated<uint8 option>
      Customers: Deferred<Customer list>
      IsReadOnly: bool
      CreatingCustomer: bool
      AddingInvoice: Deferred<unit>
      Result: Result<string, string list> option
      CustomerModel: Customer.Model
      SearchBoxModel: SearchBox.Model }

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
    | SetVat of string
    | SelectCustomer of string
    | HandleException of Exception
    | LoadCustomers of AsyncOperationStatus<Result<Customer list, string>>
    | BeginCreateCustomer
    | CustomerMsg of Customer.Msg
    | Select of string
    | VatApplicable of bool
    | SearchBoxMsg of SearchBox.Msg

type ExtMsg =
    | NoOp
    | InvoiceAdded of Invoice



let init () =
    let submodel, cmd = Customer.init ()
    let searchBoxModel, searchBoxCmd = SearchBox.init()

    { Rate = Validated.success "6000" <| uint32 6000
      ManDays = Validated.success "20" 20uy
      AccountingPeriod = DateTime.Today.AddMonths(-1).Date
      OrderNumber =
          Validated.success "17Zak00002"
          <| Some "17Zak00002"
      VatApplicable = true
      Vat = Validated.success "21" <| Some(uint8 21)
      Customers = HasNotStartedYet
      AddingInvoice = HasNotStartedYet
      IsReadOnly = false
      Result = None
      CustomerModel = submodel
      SelectedCustomer = None
      CreatingCustomer = false
      SearchBoxModel = searchBoxModel },


    Cmd.batch [ Cmd.map CustomerMsg cmd
                Cmd.ofMsg (LoadCustomers Started)
                Cmd.map SearchBoxMsg searchBoxCmd ]

let update (msg: Msg) (model: Model): Model * Cmd<Msg> * ExtMsg =
    let modelInvoiceInput = model

    match msg with
    | AddInvoice (Finished (inv, result)) ->
        { model with
              Result =
                  match result with
                  | Ok a -> Some(Ok "Success")
                  | Error s -> Some(Error [ s ])
              IsReadOnly = false },
        toastMessage result,
        match result with
        | Ok id ->
            let inv = { inv with Id = id }
            InvoiceAdded inv
        | _ -> NoOp
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
                and! vat = modelInvoiceInput.Vat.Parsed

                return
                    { Id = Guid.NewGuid()
                      Customer = customer
                      Rate = rateParsed
                      ManDays = manDaysParsed
                      AccountingPeriod = modelInvoiceInput.AccountingPeriod
                      OrderNumber = orderNumber
                      Vat = vat }
            }

        match inv with
        | Ok inv ->
            let model =
                { model with
                      IsReadOnly = true
                      Result = None }

            let cmd =
                Cmd.OfAsync.either
                    invoiceApi.addInvoice
                    inv
                    (fun r -> Finished(inv, r) |> AddInvoice)
                    (exceptionToResult
                     >> (fun r -> Finished(inv, r) |> AddInvoice))

            model, cmd, NoOp
        | Error str -> { model with Result = Some(Error str) }, Cmd.none, NoOp
    | SetRate v ->
        let rate =
            match UInt32.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v [ "Must be whole positive number less than 32768." ]

        { model with Rate = rate }, Cmd.none, NoOp
    | SetAccPeriod v -> { model with AccountingPeriod = v }, Cmd.none, NoOp
    | SetManDays v ->
        let md =
            match Byte.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v [ "Must be whole number 0-255." ]

        { model with ManDays = md }, Cmd.none, NoOp
    | SetOrderNumber v ->
        let orderNumber =
            if String.IsNullOrWhiteSpace(v) then
                None
            else
                Some v

        { model with
              OrderNumber = Validated.success v orderNumber },
        Cmd.none,
        NoOp
    | SetVat v ->
        let vat =
            match model.VatApplicable with
            | false -> Validated.success v None
            | true ->
                match Byte.TryParse v with
                | true, parsed -> Validated.success v <| Some parsed
                | false, _ -> Validated.failure v [ "Must be whole number 0-255." ]

        { model with Vat = vat }, Cmd.none, NoOp
    | HandleException exn ->
        { model with
              IsReadOnly = false
              Result = Some(Error [ exn.Message ]) },
        Cmd.none,
        NoOp
    | BeginCreateCustomer ->
        //model,Cmd.none
        { model with CreatingCustomer = true }, Cmd.ofMsg (CustomerMsg(Customer.Start model.SelectedCustomer)), NoOp
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
            Cmd.none,
            NoOp
        | _ ->
            let newSubModel, cmd = Customer.update msg model.CustomerModel

            { model with
                  CustomerModel = newSubModel },
            Cmd.map CustomerMsg cmd,
            NoOp
    | SelectCustomer str ->
        let model =
            match model.Customers, (String.IsNullOrWhiteSpace str) with
            | Resolved custs, false ->
                { model with
                      SelectedCustomer = Some custs.[(int str)] }
            | _, _ -> { model with SelectedCustomer = None }

        model, Cmd.none, NoOp
    | LoadCustomers Started ->
        { model with
              Customers = InProgress None },
        Cmd.OfAsync.either
            invoiceApi.getCustomers
            ()
            (Finished >> LoadCustomers)
            (exceptionToResult >> Finished >> LoadCustomers),
        NoOp
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
        Cmd.none,
        NoOp
    | Select s -> model, Cmd.ofMsg (SetOrderNumber s), NoOp
    | VatApplicable b -> { model with
                               VatApplicable = b
                               Vat= match b with
                                    | true -> Validated.success "21" <| Some 21uy
                                    | false -> Validated.success "" <| None }
                           , Cmd.none, NoOp
    | SearchBoxMsg msg ->
        let newSubModel, cmd = SearchBox.update msg model.SearchBoxModel

        { model with
              SearchBoxModel = newSubModel },
        Cmd.map SearchBoxMsg cmd,
        NoOp





type Props = { Model: Model; Dispatch: Msg -> unit }

let elmishView name render =
    FunctionComponent.Of(render, name, equalsButFunctions)

open Fable.React.Standard

let view =
    elmishView "InvoiceForm"
    <| fun { Props.Model = model
             Dispatch = dispatch } ->
        Box.box' [] [

            fieldset [ ReadOnly model.IsReadOnly ] [
                createTextField
                    "Man-day rate"
                    model.Rate
                    [ Input.Placeholder "Man-day rate in CZK"
                      Input.OnChange(fun x -> SetRate x.Value |> dispatch) ]

                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [ str "Month of year" ]
                        Flatpickr.flatpickr [ Flatpickr.OnChange(SetAccPeriod >> dispatch)
                                              Flatpickr.Value(
                                                  System.DateTime(
                                                      int model.AccountingPeriod.Year,
                                                      int model.AccountingPeriod.Month,
                                                      1
                                                  )
                                              )
                                              Flatpickr.DateFormat "F Y"
                                              Flatpickr.custom
                                                  "plugins"
                                                  [| monthSelectPlugin (
                                                      {| shorthand = true
                                                         dateFormat = "F Y"
                                                         altFormat = "F Y" |}
                                                  ) |]

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

                // AutoComplete.textBox { Search= invoiceApi.searchOrderNumber; Dispatch = Select >> dispatch; DebounceTimeout=400 }
                SearchBox.view { Model = model.SearchBoxModel
                                 Dispatch = dispatch << SearchBoxMsg }
                Field.div [ Field.IsExpanded ] [
                    Label.label [] [ str "Customer" ]
                    Field.div [ Field.HasAddons ] [
                        Control.div [ Control.IsExpanded ] [
                            Select.select [ Select.IsFullWidth
                                            Select.IsLoading(model.Customers |> Deferred.inProgress) ] [
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

                Field.div [ Field.IsGroupedRight
                            Field.HasAddons ] [
                    Control.p [] [
                        Checkbox.checkbox [] [
                            Checkbox.input [ Props [ OnChange(fun e -> dispatch <| VatApplicable e.Checked)
                                                     Checked model.VatApplicable ] ]
                            str " Vat applicable"
                        ]
                    ]

                    Control.p [ Control.Props [ Style [ if (not model.VatApplicable) then
                                                            Visibility "hidden" ] ] ] [
                        Input.text [ match model.Vat.Parsed with
                                     | Error []
                                     | Ok _ -> ()
                                     | Error _ -> Input.Color IsDanger
                                     Input.Value(model.Vat.Raw)
                                     Input.OnChange(fun x -> SetVat x.Value |> dispatch)
                                     Input.Placeholder "21"
                                     Input.Props [ Size 1.0 ] ]

                        match model.Vat.Parsed with
                        | Error []
                        | Ok _ -> Html.none
                        | Error (e :: _) ->
                            Help.help [ Help.Color IsDanger ] [
                                str e
                            ]
                    ]
                    Control.p [ Control.Props [ Style [ if (not model.VatApplicable) then
                                                            Visibility "hidden" ] ] ] [
                        str "%"
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
            ]
            Customer.view
                { Model = model.CustomerModel
                  Dispatch = (dispatch << CustomerMsg) }
        ]
