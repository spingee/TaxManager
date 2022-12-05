module Service

open InvoiceExcel
open LiteDB
open System.IO
open System
open FsToolkit.ErrorHandling
open Shared.Invoice
open Report

type DownloadFile = { FileName: string; ContentType: string; Stream: Stream }

let generateInvoiceNumber (coll: ILiteCollection<Invoice>) (period: DateTime) =
    let y = period.Year
    let m = period.Month

    let invoicesOfPeriod =
        coll
            .Query()
            .Where(fun f -> f.AccountingPeriod.Year = y && f.AccountingPeriod.Month = m)
            .ToArray()

    let rec getInvoiceNumber (invoices: Invoice seq) sequenceNumber =
        let invNumber =
            sprintf "%i%02i%02i" period.Year period.Month sequenceNumber

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

let getTotal (coll: ILiteCollection<Invoice>) year =
    let s = DateTime(year, 1, 1)
    let e = s.AddYears(1)

    coll
        .Query()
        .Where(fun i -> i.AccountingPeriod >= s && i.AccountingPeriod < e)
        .ToArray()
    |> Seq.collect (fun i -> i.Items)
    |> Seq.sumBy (function
        | ManDay(manDay, rate) -> (decimal manDay) * (decimal rate)
        | Additional(Total = total) -> total)

let getQuarterVatTotals (coll: ILiteCollection<Invoice>) quarterStart quarterEnd =
    let invs =
        coll
            .Query()
            .Where(fun i -> i.AccountingPeriod >= quarterStart && i.AccountingPeriod < quarterEnd)
            .ToArray()

    let total, totalVat =
        invs
        //only invoices with vat are needed for vat processing by government
        |> Seq.filter (fun i ->
            match i.Vat with
            | Some _ -> true
            | _ -> false)
        |> Seq.collect (fun i -> i.Items |> Seq.map (fun item -> item, decimal i.Vat.Value))
        |> Seq.fold
            (fun (total, totalVat) (item, vat) ->
                let total =
                    match item with
                    | ManDay(manDay, rate) -> (decimal manDay) * (decimal rate)
                    | Additional(Total = total) -> total

                (total, total * (vat / 100M)))
            (0M, 0M)

    {| Quarter = total; QuarterVat = totalVat |}

let generateInvoiceExcel (connectionString: string) (invoiceId: Guid) =
    use db = new LiteDatabase(connectionString)

    let invoices = db.GetCollection<Dto.Invoice>("invoices")

    let invoiceDto = invoices.FindById(BsonValue(invoiceId))

    let invoice =
        Dto.fromInvoiceDto invoiceDto |> Result.valueOr failwith

    let fileName = invoice.InvoiceNumber


    let stream = generateExcelData invoice

    { FileName = fileName
      ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
      Stream = stream }


let generateSummaryReport (coll: ILiteCollection<Invoice>) (reportType: SummaryReportType) =
    let getVatInput =
        let quarter = getPreviousQuarter DateTime.Now

        let year = quarter.Start.Year
        let startMonth = quarter.Start.Month
        let endMonth = quarter.End.AddMonths(-1).Month

        let invs = coll
                    .Query()
                    .Where(fun f ->
                        f.AccountingPeriod.Year = year
                        && f.AccountingPeriod.Month >= startMonth
                        && f.AccountingPeriod.Month <= endMonth)
                    .ToArray()
                    |> List.ofSeq

        { Period = Quarter quarter
          DateOfFill = DateTime.Now
          Invoices = invs }


    match reportType with
    | AnnualTax ->
        let year = DateTime.Now.AddYears(-1).Year

        generateAnnualTaxReport
            { Year = year |> uint16
              ExpensesType = Virtual 60uy
              DateOfFill = DateTime.Now
              TotalEarnings = getTotal coll year
              PenzijkoAttachment = None }
    | QuartalVatAnnounce -> getVatInput |> generateVatAnnouncementReport
    | QuartalVat -> getVatInput |> generateVatReport

let generateSummaryReportFile (coll: ILiteCollection<Invoice>) (reportType: SummaryReportType) =
    generateSummaryReport coll reportType
    |> Result.map (fun s ->
        let fileName =
            match reportType with
            | AnnualTax -> "dpfdp5_epo2.xml"
            | QuartalVatAnnounce -> "dphkh1_epo2.xml"
            | _ -> failwith $"Not implemented {reportType}"

        { FileName = fileName
          ContentType = "application/xml"
          Stream = s })