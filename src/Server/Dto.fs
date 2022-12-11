[<RequireQualifiedAccess>]
module Dto

open System
open Shared
open FsToolkit.ErrorHandling
open Shared.Invoice

[<CLIMutable>]
type Customer =
    { IdNumber: uint
      VatId: string
      Name: string
      Address: string
      Note: string }

type InvoiceItemUnionCase =
    | ManDay = 1
    | Additional = 2

[<CLIMutable>]
type InvoiceItem =
    { ManDays: Nullable<uint8>
      Rate: Nullable<uint32>
      Total: Nullable<decimal>
      Label: string
      Separate: bool
      InvoiceItemUnionCase: InvoiceItemUnionCase }

[<CLIMutable>]
type Invoice =
    { Id: Guid
      InvoiceNumber: string
      Items: InvoiceItem array
      AccountingPeriod: DateTime
      DateOfTaxableSupply: DateTime
      OrderNumber: string
      Vat: Nullable<int>
      Customer: Customer
      Created: DateTime }

let fromCustomerDto (dto: Customer) =
    result {
        let! vatId = createVatId dto.VatId

        let customer: Invoice.Customer =
            { IdNumber = dto.IdNumber
              VatId = vatId
              Name = dto.Name
              Address = dto.Address
              Note = dto.Note |> Option.ofObj }

        return customer
    }

let toInvoiceDto (invoice: Invoice.Invoice) : Invoice =
    let customer =
        { IdNumber = invoice.Customer.IdNumber
          VatId = getVatIdStr invoice.Customer.VatId
          Name = invoice.Customer.Name
          Address = invoice.Customer.Address
          Note =
            invoice.Customer.Note
            |> function
                | None -> null
                | Some s -> s }

    let items =
        invoice.Items
        |> List.map (fun (InvoiceItemInfo(item, label, separate)) ->
            let label = match  label with | Some l -> l | _ -> null
            match item with
            | ManDay(rate, manDays) ->
                { InvoiceItemUnionCase = InvoiceItemUnionCase.ManDay
                  Label = label
                  Total = Nullable<decimal>()
                  Rate = Nullable<uint32>(rate)
                  ManDays = Nullable<uint8>(manDays)
                  Separate = separate }
            | Additional total ->
                { InvoiceItemUnionCase = InvoiceItemUnionCase.Additional
                  Label = label
                  Total = Nullable<decimal>(total)
                  Rate = Nullable<uint32>()
                  ManDays = Nullable<uint8>()
                  Separate = separate })
        |> Array.ofList

    { Id = invoice.Id
      InvoiceNumber = invoice.InvoiceNumber
      Items = items
      AccountingPeriod = invoice.AccountingPeriod
      DateOfTaxableSupply = invoice.DateOfTaxableSupply
      OrderNumber =
        invoice.OrderNumber
        |> function
            | None -> null
            | Some s -> s
      Vat = invoice.Vat |> Option.map int |> Option.toNullable
      Customer = customer
      Created = invoice.Created }

let fromInvoiceDto (dto: Invoice) =
    validation {
        let! customer = fromCustomerDto dto.Customer

        and! invoiceNumber =
            if String.IsNullOrEmpty(dto.InvoiceNumber) then
                Error $"Invoice number is null {dto}"
            else
                Ok dto.InvoiceNumber
        and! items =
            dto.Items
            |> Array.map (fun i ->
                match i.InvoiceItemUnionCase with
                | InvoiceItemUnionCase.ManDay ->
                    match i.Rate.HasValue, i.ManDays.HasValue with
                    | true, true -> Ok (InvoiceItemInfo (ManDay (i.Rate.Value, i.ManDays.Value), Option.ofNull(i.Label), i.Separate))
                    | _ -> Error $"Item of type manday has unspecified rate or mandays {dto}"
                | InvoiceItemUnionCase.Additional ->
                    match i.Total.HasValue with
                    | true ->Ok (InvoiceItemInfo (Additional i.Total.Value, Option.ofNull(i.Label), i.Separate))
                    | _ -> Error $"Item of type additional has unspecified total {dto}"
                | _ -> Error $"Unknown InvoiceItemUnionCase value {i.InvoiceItemUnionCase} {dto}")
            |> List.ofArray
            |> List.sequenceResultA


        let invoice: Invoice.Invoice =
            { Id = dto.Id
              InvoiceNumber = invoiceNumber
              Items = items
              AccountingPeriod = dto.AccountingPeriod
              DateOfTaxableSupply = dto.DateOfTaxableSupply
              OrderNumber = Option.ofObj dto.OrderNumber
              Vat = dto.Vat |> Option.ofNullable |> Option.map uint8
              Customer = customer
              Created = dto.Created }

        return invoice
    }