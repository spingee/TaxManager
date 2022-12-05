[<RequireQualifiedAccess>]
module Dto

open System
open Shared
open FsToolkit.ErrorHandling

[<CLIMutable>]
type Customer =
    { IdNumber: uint
      VatId: string
      Name: string
      Address: string
      Note: string }

[<CLIMutable>]
type Invoice =
    { Id: Guid
      InvoiceNumber: string
      ManDays: uint8 option
      Rate: uint32 option
      Total: decimal
      AccountingPeriod: DateTime
      DateOfTaxableSupply: DateTime
      OrderNumber: string
      Vat: Nullable<int>
      Customer: Customer
      Inserted: DateTime }

let fromCustomerDto (dto: Customer) =
    result {
        let! vatId = Invoice.createVatId dto.VatId

        let customer: Invoice.Customer =
            { IdNumber = dto.IdNumber
              VatId = vatId
              Name = dto.Name
              Address = dto.Address
              Note = dto.Note |> Option.ofObj }

        return customer
    }

let toInvoiceDto inserted (invoice: Invoice.Invoice) : Invoice =
    let customer =
        { IdNumber = invoice.Customer.IdNumber
          VatId = Invoice.getVatIdStr invoice.Customer.VatId
          Name = invoice.Customer.Name
          Address = invoice.Customer.Address
          Note =
            invoice.Customer.Note
            |> function
                | None -> null
                | Some s -> s }

    let rate, manDays =
        invoice.Items
        |> Seq.map (function
            | Invoice.ManDay(rate, manDays) -> Some(rate, manDays)
            | _ -> None)
        //suppose we have only one ManDay item max
        |> Seq.tryPick id
        |> Option.map (fun (rate, manDays) -> Some rate, Some manDays)
        |> Option.defaultValue (None, None)

    { Id = invoice.Id
      InvoiceNumber = invoice.InvoiceNumber
      ManDays = manDays
      Rate = rate
      Total = Invoice.getTotal invoice
      AccountingPeriod = invoice.AccountingPeriod
      DateOfTaxableSupply = invoice.DateOfTaxableSupply
      OrderNumber =
        invoice.OrderNumber
        |> function
            | None -> null
            | Some s -> s
      Vat = invoice.Vat |> Option.map int |> Option.toNullable
      Customer = customer
      Inserted = inserted }

let fromInvoiceDto (dto: Invoice) =
    result {
        let! customer = fromCustomerDto dto.Customer

        let! invoiceNumber =
            if String.IsNullOrEmpty(dto.InvoiceNumber) then
                Error $"Invoice number is null {dto}"
            else
                Ok dto.InvoiceNumber

        let invoice: Invoice.Invoice =
            { Id = dto.Id
              InvoiceNumber = invoiceNumber
              Items =
                [ match dto.ManDays, dto.Rate with
                          | Some manDays, Some rate -> Invoice.ManDay(rate, manDays)
                          | _ -> () ]
              AccountingPeriod = dto.AccountingPeriod
              DateOfTaxableSupply = dto.DateOfTaxableSupply
              OrderNumber = Option.ofObj dto.OrderNumber
              Vat = dto.Vat |> Option.ofNullable |> Option.map uint8
              Customer = customer
              Created = dto.Inserted}

        return invoice
    }