module State

open Elmish
open Fable.Remoting.Client
open Shared
open Shared.Invoice
open System
open Types
open Utils
open FsToolkit.ErrorHandling

type Msg =
    | AddInvoice
    | AddedInvoice of Result<string, string>
    | SetRate of string
    | SetAccPeriod of DateTime
    | SetMandays of string
    | SetOrderNumber of string
    | SetCustomerIdNumber of string
    | SetCustomerVatId of string
    | SetCustomerName of string
    | SetCustomerAddress of string
    | SetCustomerNote of string
    | SelectCustomer of string
    | BeginCreateCustomer
    | EndCreateCustomer of bool
    | HandleException of Exception
    | LoadCustomers of AsyncOperationStatus<Result<Customer list, string>>
    | LoadInvoices of AsyncOperationStatus<Result<Invoice list, string>>
    | RemoveInvoice of Invoice * AsyncOperationStatus<Result<unit, string>>

let invoiceApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IInvoiceApi>



let init (): Model * Cmd<Msg> =
    let cmd =
        Cmd.batch [ Cmd.ofMsg (LoadCustomers Started)
                    Cmd.ofMsg (LoadInvoices Started) ]

    let model =
        { InvoiceInput =
              { Rate = Validated.success "6000" <| uint16 6000
                ManDays = Validated.success "20" 20uy
                AccountingPeriod = DateTime.Today.AddMonths(-1).Date
                OrderNumber = Validated.success "17Zak00002" <| Some "17Zak00002" }
          CustomerInput = Customer.defaultInput
          Title = "Submit invoice data"
          Result = None
          IsLoading = false
          CreatingCustomer = false
          Customers = HasNotStartedYet
          SelectedCustomer = None
          Invoices = HasNotStartedYet
          RemovingInvoice = None }



    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    let modelInvoiceInput = model.InvoiceInput
    match msg with
    | AddedInvoice result ->
        { model with
              Result = match result with | Ok a -> Some (Ok a) | Error s -> Some (Error [s])
              IsLoading = false },
        Cmd.none
    | AddInvoice ->
        let inv =
            validation {

                let! customer =
                    match model.SelectedCustomer with
                    | Some c -> Ok c
                    | None -> Error "No customer selected."

                let! rateParsed = modelInvoiceInput.Rate.Parsed
                let! manDaysParsed = modelInvoiceInput.ManDays.Parsed
                let! orderNumber = modelInvoiceInput.OrderNumber.Parsed
                return { Id = Guid.NewGuid()
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
                      IsLoading = true
                      Result = None
                      Invoices = model.Invoices |> Deferred.map (fun x -> inv :: x) }

            let cmd =
                Cmd.OfAsync.either invoiceApi.addInvoice inv AddedInvoice HandleException

            model, cmd
        | Error str -> { model with Result = Some(Error str) }, Cmd.none
    | SetRate v ->
        let rate =
            match UInt16.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v ["Must be whole positive number less than 32768."]

        { model with
              InvoiceInput = { modelInvoiceInput with Rate = rate } },
        Cmd.none
    | SetAccPeriod v ->
        { model with
              InvoiceInput =
                  { modelInvoiceInput with
                        AccountingPeriod = v } },
        Cmd.none
    | SetMandays v ->
        let md =
            match Byte.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v ["Must be whole number 0-255."]

        { model with
              InvoiceInput = { modelInvoiceInput with ManDays = md } },
        Cmd.none
    | SetOrderNumber v ->
        let orderNumber = if String.IsNullOrWhiteSpace(v) then None else Some v
        { model with
              InvoiceInput = { modelInvoiceInput with OrderNumber = Validated.success v orderNumber } },
        Cmd.none
    | SetCustomerIdNumber v ->
        let idNumber =
            match UInt32.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v ["Must be whole positive number less than ~4million."]

        { model with
              CustomerInput =
                  { model.CustomerInput with
                        IdNumber = idNumber } },
        Cmd.none
    | SetCustomerVatId v ->
        let model =
            { model with
                  CustomerInput =
                      { model.CustomerInput with
                            VatId =
                                createVatId v
                                |> function
                                | Ok x -> Validated.success v x
                                | Error _ -> Validated.failure v  ["Must be positive number prepended with ISO2 country code."]} }

        model, Cmd.none
    | SetCustomerName v ->
        let name =
            match String.IsNullOrEmpty(v) with
            | false -> Validated.success v v
            | true -> Validated.failure v ["Required."]

        { model with
              CustomerInput = { model.CustomerInput with Name = name } },
        Cmd.none
    | SetCustomerAddress v ->
        let address =
            match String.IsNullOrEmpty(v) with
            | false -> Validated.success v v
            | true -> Validated.failure v ["Required."]

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
              Result = Some(Error [exn.Message]) },
        Cmd.none
    | BeginCreateCustomer ->
        let input =
            model.SelectedCustomer
            |> Option.map (fun ci -> Customer.toCustomerInput ci)
            |> Option.defaultValue Customer.defaultInput

        { model with
              CreatingCustomer = true
              CustomerInput = input },
        Cmd.none
    | EndCreateCustomer true ->
        let cust =
            Customer.fromCustomerInput model.CustomerInput

        let model = { model with CreatingCustomer = false }
        match cust, model.Customers with
        | Ok c, Resolved custs ->
            let existing = custs |> List.tryFind (fun e -> c = e)

            match existing with
            | Some _ -> { model with SelectedCustomer = Some c }, Cmd.none
            | None ->
                { model with
                      Customers = Resolved <| c :: custs
                      SelectedCustomer = Some c },
                Cmd.none
        | _, _ -> model, Cmd.none
    | EndCreateCustomer false -> { model with CreatingCustomer = false }, Cmd.none
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
                  | Error s -> Some(Error [s]) },
        Cmd.none

    | LoadInvoices Started ->
        { model with Customers = InProgress },
        Cmd.OfAsync.either
            invoiceApi.getInvoices
            ()
            (Finished >> LoadInvoices)
            (exceptionToResult >> Finished >> LoadInvoices)
    | LoadInvoices (Finished result) ->
        { model with
              Invoices =
                  match result with
                  | Ok i -> Resolved i
                  | Error _ -> Resolved []
              Result =
                  match result with
                  | Ok _ -> None
                  | Error s -> Some(Error [s]) },
        Cmd.none
    | RemoveInvoice (inv, Started) ->
        { model with
              RemovingInvoice = Some inv },
        Cmd.OfAsync.either invoiceApi.removeInvoice inv.Id (fun x -> RemoveInvoice(inv, Finished(x))) (fun x ->
            RemoveInvoice(inv, Finished(exceptionToResult x)))
    | RemoveInvoice (inv, Finished result) ->
        { model with
              RemovingInvoice = None
              Invoices =
                  match result with
                  | Ok _ ->
                      (model.Invoices
                       |> Deferred.map (fun i -> i |> List.filter (fun x -> x <> inv)))
                  | Error e -> model.Invoices },
        Cmd.none
