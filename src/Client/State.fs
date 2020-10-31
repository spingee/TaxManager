module State

open Elmish
open Fable.Remoting.Client
open Shared
open Shared.Invoice
open System
open Types

type Msg =
    | AddInvoice
    | AddedInvoice of Result<string, string>
    | SetRate of string
    | SetAccPeriod of DateTime
    | SetMandays of string
    | SetCustomerIdNumber of string
    | SetCustomerVatId of string
    | SetCustomerName of string
    | SetCustomerAddress of string
    | SetCustomerNote of string
    | SelectCustomer of string
    | BeginCreateCustomer
    | EndCreateCustomer of bool
    | HandleException of Exception
    | LoadCustomers of Result<Customer list, string>

let invoiceApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IInvoiceApi>



let init (): Model * Cmd<Msg> =
    let model =
        { InvoiceInput =
              { Rate = Validated.success "6000" <| uint16 6000
                ManDays = Validated.success "20" 20uy
                AccountingPeriod = DateTime.Now.AddMonths(-1) }
          CustomerInput = Customer.defaultInput
          Title = "Submit invoice data"
          Result = None
          IsLoading = true
          CreatingCustomer = false
          Customers = []
          SelectedCustomer = None }

    let cmd =
        Cmd.OfAsync.either invoiceApi.getCustomers () LoadCustomers HandleException

    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | AddedInvoice result ->
        { model with
              Result = Some result
              IsLoading = false },
        Cmd.none
    | AddInvoice ->
        let inv =
            result {

                let! customer =
                    match model.SelectedCustomer with
                    | Some c -> Ok c
                    | None -> Error "No customer selected."

                return { Id = Guid.NewGuid()
                         Customer = customer
                         Rate = model.InvoiceInput.Rate.Parsed.Value
                         ManDays = model.InvoiceInput.ManDays.Parsed.Value
                         AccountingPeriod = model.InvoiceInput.AccountingPeriod }
            }

        match inv with
        | Ok inv ->
            let model =
                { model with
                      IsLoading = true
                      Result = None }

            let cmd =
                Cmd.OfAsync.either invoiceApi.addInvoice inv AddedInvoice HandleException

            model, cmd
        | Error str -> { model with Result = Some(Error str) }, Cmd.none
    | SetRate v ->
        let rate =
            match UInt16.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v

        { model with
              InvoiceInput = { model.InvoiceInput with Rate = rate } },
        Cmd.none
    | SetAccPeriod v ->
        { model with
              InvoiceInput =
                  { model.InvoiceInput with
                        AccountingPeriod = v } },
        Cmd.none
    | SetMandays v ->
        let md =
            match Byte.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v

        { model with
              InvoiceInput = { model.InvoiceInput with ManDays = md } },
        Cmd.none
    | SetCustomerIdNumber v ->
        let idNumber =
            match UInt32.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v

        { model with
              CustomerInput =
                  { model.CustomerInput with
                        IdNumber = idNumber } },
        Cmd.none
    | SetCustomerVatId v ->
        let model' =
            result {
                let! vatId = createVatId v

                return { model with
                             CustomerInput =
                                 { model.CustomerInput with
                                       VatId = Validated.success v vatId } }
            }

        match model' with
        | Ok m -> m, Cmd.none
        | _ -> model, Cmd.none
    | SetCustomerName v ->
        let name =
            match String.IsNullOrEmpty(v) with
            | false -> Validated.success v v
            | true -> Validated.failure v

        { model with
              CustomerInput = { model.CustomerInput with Name = name } },
        Cmd.none
    | SetCustomerAddress v ->
        let address =
            match String.IsNullOrEmpty(v) with
            | false -> Validated.success v v
            | true -> Validated.failure v

        { model with
              CustomerInput =
                  { model.CustomerInput with
                        Address = address } },
        Cmd.none
    | SetCustomerNote v ->
        let note =
            match String.IsNullOrEmpty(v) with
            | true -> Validated.success "" None
            | false -> Validated.success v <| Some v

        { model with
              CustomerInput = { model.CustomerInput with Note = note } },
        Cmd.none
    | HandleException exn ->
        { model with
              IsLoading = false
              Result = Some(Error exn.Message) },
        Cmd.none
    | BeginCreateCustomer ->
        { model with
              CreatingCustomer = true
              CustomerInput =
                  Option.map (fun ci -> Customer.toCustomerInput ci) model.SelectedCustomer
                  |> Option.defaultValue Customer.defaultInput },
        Cmd.none
    | EndCreateCustomer true ->
        let cust =
            Customer.fromCustomerInput model.CustomerInput

        let model = { model with CreatingCustomer = false }
        match cust with
        | Some c ->
            let existing =
                model.Customers |> List.tryFind (fun e -> c = e)

            match existing with
            | Some _ -> { model with SelectedCustomer = Some c }, Cmd.none
            | None ->
                { model with
                      Customers = c :: model.Customers
                      SelectedCustomer = cust },
                Cmd.none
        | None -> model, Cmd.none
    | EndCreateCustomer false -> { model with CreatingCustomer = false }, Cmd.none
    | SelectCustomer str ->

        let model =
            if not (String.IsNullOrWhiteSpace str) then
                { model with
                      SelectedCustomer = Some model.Customers.[(int str)] }
            else
                { model with SelectedCustomer = None }

        model, Cmd.none
    | LoadCustomers result ->
        { model with
              Customers =
                  match result with
                  | Ok cs -> cs
                  | Error _ -> []
              SelectedCustomer =
                  match result with
                  | Ok cs when cs.Length > 0 -> Some cs.Head
                  | _ -> None
              Result =
                  match result with
                  | Ok _ -> None
                  | Error s -> Some(Error s)
              IsLoading = false },
        Cmd.none
