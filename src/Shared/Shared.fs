namespace Shared

open System
open System.Text.RegularExpressions



module Invoice =
    // for not not internal , becouse it wont serialze trough fable.remoting
    type VatId = VatId of string

    type Quarter = { Number: uint; Start: DateTime; End: DateTime }

    let createVatId str =
        let legit = Regex("^[A-Z]{2}[A-Z0-9]+$").IsMatch(str)

        match legit with
        | true -> Ok(VatId str)
        | false -> Error("Vat id has wrong format.")

    let getVatIdStr (VatId str) = str

    let getQuarter (d: DateTime) =
        match d.Month with
        | 1
        | 2
        | 3 -> 1, 3, d.Year, 1u
        | 4
        | 5
        | 6 -> 4, 6, d.Year, 2u
        | 7
        | 8
        | 9 -> 7, 9, d.Year, 3u
        | 10
        | 11
        | 12 -> 10, 12, d.Year, 4u
        | _ -> failwith "nonsense"
        |> fun (s, e, y, r) ->
            { Number = r
              Start = DateTime(y, s, 1)
              End = DateTime(y, e, 1).AddMonths(1) }

    let rec getPreviousQuarter d =
        let current =
            match (getQuarter d).Number - 1u with
            | 0u -> 4u
            | q -> q

        let previousMonth = d.AddMonths(-1)

        match getQuarter previousMonth with
        | q when q.Number = current -> q
        | _ -> getPreviousQuarter previousMonth

    type Customer =
        { IdNumber: uint
          VatId: VatId
          Name: string
          Address: string
          Note: string option }

    type InvoiceItem =
        | ManDay of Rate: uint32 * ManDays: uint8
        | Additional of Label: string * Total: decimal

    type Invoice =
        { Id: Guid
          InvoiceNumber: string
          Items: InvoiceItem list
          AccountingPeriod: DateTime
          DateOfTaxableSupply: DateTime
          OrderNumber: string option
          Vat: uint8 option
          Customer: Customer
          Created: DateTime }

    type InvoiceDefaults =
        { InvoiceNumber: string
          ManDays: uint8
          Rate: uint32
          AccountingPeriod: DateTime
          DateOfTaxableSupply: DateTime
          Vat: uint8 option
          Customer: Customer }

    type TotalPrice =
        { Value: decimal
          Currency: string
          TimeRange: string }
        static member Default = { Value = 0m; Currency = "CZK"; TimeRange = "" }

    type Totals =
        { LastYear: TotalPrice
          LastQuarter: TotalPrice
          LastQuarterVat: TotalPrice
          CurrentYear: TotalPrice
          CurrentQuarter: TotalPrice
          CurrentQuarterVat: TotalPrice }

    let TotalsDefault =
        { LastYear = TotalPrice.Default
          LastQuarter = TotalPrice.Default
          LastQuarterVat = TotalPrice.Default
          CurrentYear = TotalPrice.Default
          CurrentQuarter = TotalPrice.Default
          CurrentQuarterVat = TotalPrice.Default }


    let getTotal inv =
        inv.Items
        |> Seq.map (function
            | ManDay(rate, manDays) -> (decimal manDays) * (decimal rate)
            | Additional(Total = total) -> total)
        |> Seq.sum

    let getVatAmount (i:Invoice) =
        i.Vat
        |> Option.map (fun v -> (getTotal i / 100M * (decimal v)))
        |> Option.defaultValue 0M

    let getTotalWithVat i =
        let total = getTotal i
        let vat = getVatAmount i

        i.Vat
        |> Option.map (fun v -> total + vat)
        |> Option.defaultValue total

    let getLastDayOfMonth (accPeriod: DateTime) =
        DateTime(int accPeriod.Year, int accPeriod.Month, 1).AddMonths(1).AddDays(-1)



    type SummaryReportType =
        | AnnualTax
        | QuartalVat
        | QuartalVatAnnounce
        static member fromString string =
            match string with
            | nameof (AnnualTax) -> AnnualTax
            | nameof (QuartalVat) -> QuartalVat
            | nameof (QuartalVatAnnounce) -> QuartalVatAnnounce
            | _ -> failwithf "Not implemented: %s" string

    type AddInvoiceRequest =
        { ManDays: uint8
          Rate: uint32
          AccountingPeriod: DateTime
          OrderNumber: string option
          Vat: uint8 option
          Customer: Customer }

    type IInvoiceApi =
        { addInvoice: AddInvoiceRequest -> Async<Result<Invoice, string>>
          getCustomers: unit -> Async<Result<Customer list, string>>
          getInvoices: int -> int -> Async<Result<Invoice list * int, string>>
          getInvoiceDefaults: unit -> Async<Result<InvoiceDefaults, string>>
          removeInvoice: Guid -> Async<Result<unit, string>>
          searchOrderNumber: string -> Async<Result<string list, string>>
          getTotals: unit -> Async<Result<Totals, string>>
          prepareSummaryReportUrl: SummaryReportType -> Async<Result<string, string>> }



module Route =
    let builder typeName methodName = sprintf "/api/%s/%s" typeName methodName