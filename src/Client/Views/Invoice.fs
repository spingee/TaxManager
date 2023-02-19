[<RequireQualifiedAccess>]
module Invoice

open System
open Fable.FontAwesome
open Fable.React.Props
open Fulma
open Shared.Invoice
open Utils
open Fable.React
open Fable.Core.JsInterop
open Elmish
open FsToolkit.ErrorHandling
open Api
open Common

let monthSelectPlugin (x: obj) : obj -> obj =
    importDefault "flatpickr/dist/plugins/monthSelect"



type Model =
    { ManDays: Validated<uint8>
      Rate: Validated<uint32>
      AdditionalItem: Validated<decimal option>
      OrderNumber: Validated<string option>
      AccountingPeriod: DateTime
      DateOfTaxableSupply: DateTime
      DueDate: DateTime
      SelectedCustomer: Validated<Customer>
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
    let { ManDays = mandays
          Rate = rate
          OrderNumber = orderNumber
          SelectedCustomer= customer } =
        input

    match (mandays.Parsed, rate.Parsed, orderNumber.Parsed,customer.Parsed ) with
    | Ok _, Ok _, Ok _, Ok _ -> true
    | _, _, _, _ -> false

type Msg =
    | AddInvoice of AsyncOperationStatus<Result<Invoice, string>>
    | SetRate of string
    | SetAccPeriod of DateTime
    | SetDateOfTaxableSupply of DateTime
    | SetDueDate of DateTime
    | SetManDays of string
    | SetAdditionalItem of string
    | SetOrderNumber of string
    | SetVat of string
    | SelectCustomer of string
    | HandleException of Exception
    | LoadCustomers of AsyncOperationStatus<Result<Customer list, string>>
    | BeginCreateCustomer
    | CustomerMsg of Customer.Msg
    | VatApplicable of bool
    | SearchBoxMsg of SearchBox.Msg
    | LoadDefaults of AsyncOperationStatus<Result<InvoiceDefaults, string>>

type ExtMsg =
    | NoOp
    | InvoiceAdded of Invoice



let init () =
    let submodel, cmd = Customer.init ()

    let searchBoxModel, searchBoxCmd =
        SearchBox.init invoiceApi.searchOrderNumber

    { Rate = Validated.success (InvoiceDefaults.Default.Rate.ToString()) <| InvoiceDefaults.Default.Rate
      ManDays = Validated.success (InvoiceDefaults.Default.ManDays.ToString()) InvoiceDefaults.Default.ManDays
      AdditionalItem = Validated.success "" None
      AccountingPeriod = InvoiceDefaults.Default.AccountingPeriod
      DateOfTaxableSupply = InvoiceDefaults.Default.DateOfTaxableSupply
      DueDate = InvoiceDefaults.Default.DueDate
      OrderNumber = Validated.success (match InvoiceDefaults.Default.OrderNumber with |Some s-> s |_ -> "") InvoiceDefaults.Default.OrderNumber
      VatApplicable = match InvoiceDefaults.Default.Vat with | Some _ -> true | _ -> false
      Vat = Validated.success (InvoiceDefaults.Default.Vat.ToString()) <| InvoiceDefaults.Default.Vat
      Customers = HasNotStartedYet
      AddingInvoice = HasNotStartedYet
      IsReadOnly = false
      Result = None
      CustomerModel = submodel
      SelectedCustomer = Validated.createEmpty()
      CreatingCustomer = false
      SearchBoxModel = searchBoxModel },


    Cmd.batch [ Cmd.map CustomerMsg cmd
                Cmd.ofMsg (LoadCustomers Started)
                Cmd.ofMsg (LoadDefaults Started)
                Cmd.map SearchBoxMsg searchBoxCmd ]

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExtMsg =
    let modelInvoiceInput = model

    match msg with
    | AddInvoice (Finished result) ->
        { model with
              Result =
                  match result with
                  | Ok a -> Some(Ok "Success")
                  | Error s -> Some(Error [ s ])
              IsReadOnly = false },
        toastMessage result,
        match result with
        | Ok i ->
            InvoiceAdded i
        | _ -> NoOp
    | AddInvoice Started ->
        let inv =
            validation {

                let! customer = model.SelectedCustomer.Parsed
                and! rateParsed = modelInvoiceInput.Rate.Parsed
                and! additionalItemParsed = modelInvoiceInput.AdditionalItem.Parsed
                and! manDaysParsed = modelInvoiceInput.ManDays.Parsed
                and! orderNumber = modelInvoiceInput.OrderNumber.Parsed
                and! vat = modelInvoiceInput.Vat.Parsed

                return
                    { Customer = customer
                      Rate = rateParsed
                      ManDays = manDaysParsed
                      AdditionalItem = additionalItemParsed
                      AccountingPeriod = modelInvoiceInput.AccountingPeriod
                      DateOfTaxableSupply = modelInvoiceInput.DateOfTaxableSupply
                      DueDate = DateOnly.FromDateTime modelInvoiceInput.DueDate
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
                    (fun r -> Finished(r) |> AddInvoice)
                    (exceptionToResult
                     >> (fun r -> Finished(r) |> AddInvoice))

            model, cmd, NoOp
        | Error str -> { model with Result = Some(Error str) }, Cmd.none, NoOp
    | SetRate v ->
        let rate =
            match UInt32.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v [ "Must be whole positive number less than 32768." ]

        { model with Rate = rate }, Cmd.none, NoOp
    | SetAccPeriod v -> { model with AccountingPeriod = v }, Cmd.none, NoOp
    | SetDateOfTaxableSupply v -> { model with DateOfTaxableSupply = v }, Cmd.none, NoOp
    | SetDueDate v -> { model with DueDate = v }, Cmd.none, NoOp
    | SetManDays v ->
        let md =
            match Byte.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v [ "Must be whole number 0-255." ]

        { model with ManDays = md }, Cmd.none, NoOp
    | SetAdditionalItem v ->
        let md =
            match String.IsNullOrEmpty(v) ,Decimal.TryParse v with
            | true ,_ -> Validated.success "" None
            | _,(true , a) when a >= 0M -> Validated.success v (Some a)
            | _ -> Validated.failure v [ "Must be positive decimal number." ]

        { model with AdditionalItem = md }, Cmd.none, NoOp
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
        let customer =
            match model.SelectedCustomer.Parsed with
            | Ok c -> Some c
            | _ -> None
        { model with CreatingCustomer = true }, Cmd.ofMsg (CustomerMsg(Customer.Start customer)), NoOp
    | CustomerMsg msg ->
        match msg with
        | Customer.Finish c ->
            let model =
                { model with
                      CreatingCustomer = false
                      SelectedCustomer = Validated.success "" c  }

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
                      SelectedCustomer = Validated.success "" custs[(int str)] }
            | _, _ -> { model with SelectedCustomer = Validated.createEmpty() }

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
                  | Ok cs when cs.Length > 0 -> Validated.success "" cs.Head
                  | _ -> Validated.createEmpty()
              Result =
                  match result with
                  | Ok _ -> None
                  | Error s -> Some(Error [ s ]) },
        Cmd.none,
        NoOp
    | VatApplicable b ->
        { model with
              VatApplicable = b
              Vat =
                  match b with
                  | true -> Validated.success "21" <| Some 21uy
                  | false -> Validated.success "" <| None },
        Cmd.none,
        NoOp
    | SearchBoxMsg msg ->
        let newSubModel, subCmd =
            SearchBox.update msg model.SearchBoxModel

        let cmd =
            match msg with
            | SearchBox.ChangeValue txt
            | SearchBox.SelectText txt -> Cmd.ofMsg (SetOrderNumber txt)

            | _ -> Cmd.none


        { model with
              SearchBoxModel = newSubModel },
        Cmd.batch [ Cmd.map SearchBoxMsg subCmd
                    cmd ],
        NoOp
    | LoadDefaults Started ->
        { model with
              Customers = InProgress None },
        Cmd.OfAsync.either
            invoiceApi.getInvoiceDefaults
            ()
            (Finished >> LoadDefaults)
            (exceptionToResult >> Finished >> LoadDefaults),
        NoOp

    | LoadDefaults (Finished result) ->
        let model =
            match result with
            | Ok  i ->
                { model with
                      Rate = Validated.success (i.Rate.ToString()) i.Rate
                      SelectedCustomer = match i.Customer with | Some  c ->  Validated.success "" c | _ -> Validated.createEmpty()
                      ManDays = Validated.success (i.ManDays.ToString()) i.ManDays
                      AccountingPeriod = i.AccountingPeriod
                      DateOfTaxableSupply = i.DateOfTaxableSupply
                      OrderNumber =
                          Validated.success
                              (i.OrderNumber
                               |> function
                                   | Some o -> o
                                   | _ -> "")
                              i.OrderNumber }
            | Error e ->  { model with  Result = Some(Error [ e ]) }

        model, Cmd.none, NoOp


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
                createTextField
                    "Total number of man days"
                    model.ManDays
                    [ Input.Placeholder "Total number of man days"
                      Input.OnChange(fun x -> SetManDays x.Value |> dispatch) ]
                createTextField
                    "Additional item (CZK)"
                    model.AdditionalItem
                    [ Input.Placeholder "Additional item (CZK)"
                      Input.OnChange(fun x -> SetAdditionalItem x.Value |> dispatch) ]
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
                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [ str "Date od taxable supply" ]
                        Flatpickr.flatpickr [ Flatpickr.OnChange(SetDateOfTaxableSupply >> dispatch)
                                              Flatpickr.Value(model.DateOfTaxableSupply)
                                              Flatpickr.DateFormat "d.m.Y"
                                              Flatpickr.Locale Flatpickr.Locales.czech
                                              Flatpickr.ClassName "input" ]
                    ]
                ]

                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [ str "Due date" ]
                        Flatpickr.flatpickr [ Flatpickr.OnChange(SetDueDate >> dispatch)
                                              Flatpickr.Value(model.DueDate)
                                              Flatpickr.DateFormat "d.m.Y"
                                              Flatpickr.Locale Flatpickr.Locales.czech
                                              Flatpickr.ClassName "input" ]
                    ]
                ]

                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [ str "Order number" ]
                        SearchBox.view
                            { Model =
                                  { model.SearchBoxModel with
                                        UserInput = model.OrderNumber.Raw }
                              Dispatch = dispatch << SearchBoxMsg }
                    ]
                ]
                Field.div [ Field.IsExpanded ] [
                    Label.label [] [ str "Customer" ]
                    Field.div [ Field.HasAddons ] [
                        Control.div [ Control.IsExpanded ] [
                            Select.select [ Select.IsFullWidth
                                            Select.IsLoading(model.Customers |> Deferred.inProgress) ] [
                                select [ OnChange(fun e -> dispatch <| SelectCustomer e.Value) ] [
                                    match model.SelectedCustomer.Parsed with
                                    | Ok _ -> ()
                                    | _ -> yield option [ Value("") ] [ str "" ]
                                    match model.Customers with
                                    | Resolved custs ->
                                        for i = 0 to custs.Length - 1 do
                                            let c = custs.[i]

                                            yield
                                                option [ Value(i)
                                                         Selected(model.SelectedCustomer = Validated.success "" c) ] [
                                                    str <| sprintf "%s (%i)" c.Name c.IdNumber
                                                ]
                                    | _ -> ()
                                ]
                            ]
                            match model.SelectedCustomer.Parsed with
                             | Error []
                             | Ok _ -> Html.none
                             | Error (e :: _) ->
                                    Help.help [ Help.Color IsDanger ] [
                                        str e
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
                                     Input.Props [ Size 2.0 ] ]

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
                        Button.button [ Button.Color IsPrimary
                                        Button.IsLoading model.IsReadOnly
                                        Button.Disabled(isValid model |> not)
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