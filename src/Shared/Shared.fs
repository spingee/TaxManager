namespace Shared

open System
open System.Text.RegularExpressions


module Invoice =
    // for not not internal , becouse it wont serialze trough fable.remoting
    type VatId = VatId of string

    let createVatId str =
        let legit =
            Regex("^[A-Z]{2}[A-Z0-9]+$").IsMatch(str)

        match legit with
        | true -> Ok(VatId str)
        | false -> Error("Vat id has wrong format.")

    let getVatIdStr (VatId str) = str



    type Customer =
        { IdNumber: uint
          VatId: VatId
          Name: string
          Address: string
          Note: string option }

    type Invoice =
        { Id: Guid
          ManDays: uint8
          Rate: uint32
          AccountingPeriod: DateTime
          OrderNumber: string option
          Vat: uint8 option
          Customer: Customer }

    type TotalPrice =
        { Value: decimal
          Currency: string
          TimeRange: string }

    type Totals =
        { LastYear: TotalPrice
          LastQuarter: TotalPrice
          LastQuarterVat: TotalPrice }

    let TotalsDefault =
        { LastYear =
              { Value = 0m
                Currency = "CZK"
                TimeRange = "" }
          LastQuarter =
              { Value = 0m
                Currency = "CZK"
                TimeRange = "" }
          LastQuarterVat =
              { Value = 0m
                Currency = "CZK"
                TimeRange = "" } }

    type IInvoiceApi =
        { addInvoice: Invoice -> Async<Result<Guid, string>>
          getCustomers: unit -> Async<Result<Customer list, string>>
          getInvoices: unit -> Async<Result<Invoice list, string>>
          removeInvoice: Guid -> Async<Result<unit, string>>
          searchOrderNumber: string -> Async<Result<string list, string>>
          getTotals: unit -> Async<Result<Totals, string>> }

    let getInvoiceNumber invoice indexNumber =
        sprintf "%i%02i%02i" invoice.AccountingPeriod.Year invoice.AccountingPeriod.Month indexNumber

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName
