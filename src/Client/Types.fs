module Types

open Shared.Invoice
open System
open Utils
open FsToolkit.ErrorHandling


type Validated<'t> = { Raw: string; Parsed: Validation<'t,string> }

module Validated =
    let createEmpty (): Validated<_> = { Raw = ""; Parsed = Error [] }
    let success raw value: Validated<_> = { Raw = raw; Parsed = Ok value }
    let failure raw errors: Validated<_> = { Raw = raw; Parsed = Error errors }

module Customer =
    type CustomerInput =
        { IdNumber: Validated<uint>
          VatId: Validated<VatId>
          Name: Validated<string>
          Address: Validated<string>
          Note: Validated<string option> }

    let toCustomerInput (customer: Customer) =
        { IdNumber = Validated.success (customer.IdNumber.ToString()) customer.IdNumber
          VatId = Validated.success (getVatIdStr customer.VatId) customer.VatId
          Name = Validated.success customer.Name customer.Name
          Address = Validated.success customer.Address customer.Address
          Note =
              Validated.success
                  (Option.map id customer.Note
                   |> Option.defaultValue "")
                  customer.Note }

    let fromCustomerInput custInput =
        validation {
            let! idNumber = custInput.IdNumber.Parsed
            let! vatId = custInput.VatId.Parsed
            let! name = custInput.Name.Parsed
            let! address = custInput.Address.Parsed
            let! note = custInput.Note.Parsed

            return { Customer.IdNumber = idNumber
                     VatId = vatId
                     Name = name
                     Address = address
                     Note = note }
        }

    let defaultInput =
        { IdNumber = Validated.createEmpty ()
          VatId = Validated.createEmpty ()
          Name = Validated.createEmpty ()
          Address = Validated.createEmpty ()
          Note = Validated.createEmpty () }

    let isValid input =
        match fromCustomerInput input with
        | Ok _ -> true
        | Error _ -> false

module Invoice =
    type InvoiceInput =
        { ManDays: Validated<uint8>
          Rate: Validated<uint16>
          OrderNumber: Validated<string option>
          AccountingPeriod: DateTime }

    let isValid input =
        let { InvoiceInput.ManDays = mandays; Rate = rate; OrderNumber = orderNumber } = input
        match (mandays.Parsed, rate.Parsed,orderNumber.Parsed) with
        | Ok _, Ok _,Ok _ -> true
        | _, _, _ -> false



type Model =
    { InvoiceInput: Invoice.InvoiceInput
      CustomerInput: Customer.CustomerInput
      Title: string
      Result: Result<string, string list> option
      IsLoading: bool
      CreatingCustomer: bool
      Customers: Deferred<Customer list>
      SelectedCustomer: Customer option
      Invoices: Deferred<Invoice list>
      RemovingInvoice: Invoice option }

let isModelValid model =
    Invoice.isValid model.InvoiceInput
    && Option.isSome model.SelectedCustomer
