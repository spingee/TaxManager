module Service

open InvoiceExcel
open LiteDB
open System.IO
open System
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Core
open Shared.Invoice
open Report

type DownloadFile = { FileName: string; ContentType: string; Stream: Stream }

let generateInvoiceNumber (coll: ILiteCollection<Dto.Invoice>) (period: DateTime) =
    let y = period.Year
    let m = period.Month

    let invoicesOfPeriod =
        coll
            .Query()
            .Where(fun f -> f.AccountingPeriod.Year = y && f.AccountingPeriod.Month = m)
            .ToArray()

    let rec getInvoiceNumber (invoices: Dto.Invoice seq) sequenceNumber =
        let invNumber = formatInvoiceNumber period sequenceNumber

        let result =
            invoices
            |> Seq.tryFind (fun i ->
                (String.IsNullOrEmpty(i.InvoiceNumber) = false)
                && i.InvoiceNumber.Equals(invNumber, StringComparison.OrdinalIgnoreCase))

        match result with
        | None -> invNumber, sequenceNumber
        | Some _ -> getInvoiceNumber invoices (sequenceNumber + 1)

    getInvoiceNumber invoicesOfPeriod 1


let getInvoiceFileName (period: DateTime) index =
    sprintf "Faktura - %i-%02i-%02i" period.Year period.Month index

let getTotal (coll: ILiteCollection<Dto.Invoice>) year =
    let s = DateTime(year, 1, 1)
    let e = s.AddYears(1)

    coll
        .Query()
        .Where(fun i -> i.AccountingPeriod >= s && i.AccountingPeriod < e)
        .ToArray()
    |> Seq.map Dto.fromInvoiceDto
    |> List.ofSeq
    |> List.sequenceResultM
    |> Result.map (Seq.sumBy getTotal)



let getQuarterVatTotals (coll: ILiteCollection<Dto.Invoice>) quarterStart quarterEnd =
    let invs =
        coll
            .Query()
            .Where(fun i -> i.AccountingPeriod >= quarterStart && i.AccountingPeriod < quarterEnd)
            .ToArray()


    invs
    |> Seq.map Dto.fromInvoiceDto
    |> List.ofSeq
    |> List.sequenceResultM
    |> Result.map (
        Seq.filter (fun i ->
            //only invoices with vat are needed for vat processing by government
            match i.Vat with
            | Some _ -> true
            | _ -> false)
        >> Seq.collect (fun i ->
            i.Items
            |> Seq.map (fun item -> (item |> extractInvoiceItem), decimal i.Vat.Value))
        >> Seq.fold
            (fun (total, totalVat) (item, vat) ->
                let total' =
                    match item with
                    | ManDay(manDay, rate) -> (decimal manDay) * (decimal rate)
                    | Additional(Total = total) -> total

                (total' + total, (total' * (vat / 100M)) + totalVat))
            (0M, 0M)
        >> (fun (q, v) -> {| Quarter = q; QuarterVat = v |})
    )

let generateInvoiceExcel (connectionString: string) (invoiceId: Guid) =
    use db = new LiteDatabase(connectionString)

    let invoices =
        db.GetCollection<Dto.Invoice>("invoices")

    let invoice =
        invoices.FindById(BsonValue(invoiceId))
        |> Dto.fromInvoiceDto
        |> Result.valueOr (String.concat Environment.NewLine >> failwith)

    let fileName = invoice.InvoiceNumber

    let stream = generateExcelData invoice

    { FileName = fileName
      ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
      Stream = stream }


let generateSummaryReport (coll: ILiteCollection<Dto.Invoice>) (reportType: SummaryReportType) =
    let getVatInput =
        let quarter = getPreviousQuarter DateTime.Now

        let year = quarter.Start.Year
        let startMonth = quarter.Start.Month
        let endMonth = quarter.End.AddMonths(-1).Month
        validation {
            let! invs =
                coll
                    .Query()
                    .Where(fun f ->
                        f.AccountingPeriod.Year = year
                        && f.AccountingPeriod.Month >= startMonth
                        && f.AccountingPeriod.Month <= endMonth)
                    .ToArray()
                |> List.ofSeq
                |> List.map Dto.fromInvoiceDto
                |> List.sequenceResultM
            return
                { Period = Quarter quarter
                  DateOfFill = DateTime.Now
                  Invoices = invs }
        }


    validation {
        return!
            match reportType with
            | AnnualTax ->
                let year = DateTime.Now.AddYears(-1).Year
                validation {
                    let! total = getTotal coll year
                    return! generateAnnualTaxReport
                        { Year = year |> uint16
                          ExpensesType = Virtual 60uy
                          DateOfFill = DateTime.Now
                          TotalEarnings = total
                          PenzijkoAttachment = None }
                }
            | QuarterVatAnnounce ->
                validation {
                    let! vatInput = getVatInput
                    return! vatInput |> generateVatAnnouncementReport
                }
            | QuarterVat ->
                validation {
                    let! vatInput = getVatInput
                    return! vatInput |> generateVatReport
                }
    }



let generateSummaryReportFile (coll: ILiteCollection<Dto.Invoice>) (reportType: SummaryReportType) =
    generateSummaryReport coll reportType
    |> Result.bind (fun s ->
        validation {
            let fileName =
                match reportType with
                | AnnualTax -> "dpfdp5_epo2.xml"
                | QuarterVatAnnounce -> "dphkh1_epo2.xml"
                | _ -> failwith $"Not implemented {reportType}"

            return
                { FileName = fileName
                  ContentType = "application/xml"
                  Stream = s }
        })

let getTotals coll =
    validation {
        let lastYear = DateTime.Now.AddYears(-1).Year
        let currentYear = DateTime.Now.Year

        let { Start = lastQuarterStart
              End = lastQuarterEnd
              Number = lastQuarterNumber } =
            getPreviousQuarter DateTime.Now

        let { Start = currentQuarterStart
              End = currentQuarterEnd
              Number = currentQuarterNumber } =
            getQuarter DateTime.Now


        let! lastYearTotal = getTotal coll lastYear
        and! currentYearTotal = getTotal coll currentYear

        and! lastQuarter = getQuarterVatTotals coll lastQuarterStart lastQuarterEnd

        and! currentQuarter = getQuarterVatTotals coll currentQuarterStart currentQuarterEnd

        return
            { LastYear =
                { Value = decimal lastYearTotal
                  Currency = "CZK"
                  TimeRange = lastYear.ToString() }
              LastQuarter =
                { Value = decimal lastQuarter.Quarter
                  Currency = "CZK"
                  TimeRange = $"Q{lastQuarterNumber}" }
              LastQuarterVat =
                { Value = decimal lastQuarter.QuarterVat
                  Currency = "CZK"
                  TimeRange = $"Q{lastQuarterNumber}" }
              CurrentYear =
                { Value = decimal currentYearTotal
                  Currency = "CZK"
                  TimeRange = currentYear.ToString() }
              CurrentQuarter =
                { Value = decimal currentQuarter.Quarter
                  Currency = "CZK"
                  TimeRange = $"Q{currentQuarterNumber}" }
              CurrentQuarterVat =
                { Value = decimal currentQuarter.QuarterVat
                  Currency = "CZK"
                  TimeRange = $"Q{currentQuarterNumber}" } }
    }