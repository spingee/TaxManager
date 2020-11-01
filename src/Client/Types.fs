module Types

open Shared.Invoice
open System
open Shared.Option
open Utils

type Validated<'t> = { Raw: string; Parsed: Option<'t> }

module Validated =
    let createEmpty (): Validated<_> = { Raw = ""; Parsed = None }
    let success raw value: Validated<_> = { Raw = raw; Parsed = Some value }
    let failure raw: Validated<_> = { Raw = raw; Parsed = None }

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
        optional {
            let! idNumber = custInput.IdNumber.Parsed
            let! vatId = custInput.VatId.Parsed
            let! name = custInput.Name.Parsed
            let! address = custInput.Address.Parsed
            let! note =  custInput.Note.Parsed

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
        | Some _ -> true
        | None -> false

module Invoice =
    type InvoiceInput =
        { ManDays: Validated<uint8>
          Rate: Validated<uint16>
          AccountingPeriod: DateTime }

    let isValid input =
        let { InvoiceInput.ManDays = mandays; Rate = rate } = input
        match (mandays.Parsed, rate.Parsed) with
        | Some _, Some _ -> true
        | _, _ -> false



type Model =
    { InvoiceInput: Invoice.InvoiceInput
      CustomerInput: Customer.CustomerInput
      Title: string
      Result: Result<string, string> option
      IsLoading: bool
      CreatingCustomer: bool
      Customers: Customer list
      SelectedCustomer: Customer option }

let isModelValid model =
    Invoice.isValid model.InvoiceInput
    && Option.isSome model.SelectedCustomer
