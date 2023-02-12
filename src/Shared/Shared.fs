namespace Shared

open System
open System.Text.RegularExpressions



module Invoice =
    type VatId = private VatId of string

    type Quarter = { Number: uint; Start: DateTime; End: DateTime }

    let createVatId str =
        let legit = Regex("^[A-Z]{2}[A-Z0-9]+$").IsMatch(str)

        match legit with
        | true -> Ok(VatId str)
        | false -> Error("Vat id has wrong format.")

    let getVatIdStr (VatId str) = str

    let formatInvoiceNumber (month: DateTime) =
        sprintf "%i%02i%02i" month.Year month.Month

    let getLastDayOfMonth (month: DateTime) =
        DateTime(int month.Year, int month.Month, 1).AddMonths(1).AddDays(-1)

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
        | Additional of Total: decimal

    type InvoiceItemInfo = InvoiceItemInfo of InvoiceItem: InvoiceItem * Label: string option * Separate: bool
    let extractInvoiceItem (InvoiceItemInfo(InvoiceItem = invoiceItem)) = invoiceItem

    type Invoice =
        { Id: Guid
          InvoiceNumber: string
          Items: InvoiceItemInfo list
          AccountingPeriod: DateTime
          DateOfTaxableSupply: DateTime
          DueDate: DateOnly
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
          DueDate: DateTime
          Vat: uint8 option
          Customer: Customer option
          OrderNumber: string option }

        static member Default =
            let lastMonth = DateTime.Now.AddMonths(-1)
            { Rate = 6000u
              Vat = Some 21uy
              ManDays = 20uy
              DateOfTaxableSupply = getLastDayOfMonth lastMonth
              DueDate= getLastDayOfMonth DateTime.Now
              InvoiceNumber = formatInvoiceNumber lastMonth 1
              AccountingPeriod = lastMonth.Date
              Customer = None
              OrderNumber = None }

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
        |> Seq.map (
            extractInvoiceItem
            >> function
                | ManDay(rate, manDays) -> (decimal manDays) * (decimal rate)
                | Additional(Total = total) -> total
        )
        |> Seq.sum

    let getVatAmount (i: Invoice) =
        i.Vat
        |> Option.map (fun v -> (getTotal i / 100M * (decimal v)))
        |> Option.defaultValue 0M

    let getTotalWithVat i =
        let total = getTotal i
        let vat = getVatAmount i

        i.Vat
        |> Option.map (fun v -> total + vat)
        |> Option.defaultValue total



    let getManDayAndRate i =
        i.Items
        |> Seq.tryPick (extractInvoiceItem >> function | ManDay (m,r)  -> Some (m,r) | _ -> None)



    type SummaryReportType =
        | AnnualTax
        | QuarterVat
        | QuarterVatAnnounce

        static member fromString string =
            match string with
            | nameof AnnualTax -> AnnualTax
            | nameof QuarterVat -> QuarterVat
            | nameof QuarterVatAnnounce -> QuarterVatAnnounce
            | _ -> failwithf "Not implemented: %s" string

    type AddInvoiceRequest =
        { ManDays: uint8
          Rate: uint32
          AdditionalItem: decimal option
          AccountingPeriod: DateTime
          DateOfTaxableSupply: DateTime
          DueDate: DateOnly
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