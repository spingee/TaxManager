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
      ManDays: uint8
      Rate: uint32
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

    { Id = invoice.Id
      InvoiceNumber = invoice.InvoiceNumber
      ManDays = invoice.ManDays
      Rate = invoice.Rate
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
              ManDays = dto.ManDays
              Rate = dto.Rate
              AccountingPeriod = dto.AccountingPeriod
              DateOfTaxableSupply = dto.DateOfTaxableSupply
              OrderNumber = Option.ofObj dto.OrderNumber
              Vat = dto.Vat |> Option.ofNullable |> Option.map uint8
              Customer = customer }

        return invoice
    }
