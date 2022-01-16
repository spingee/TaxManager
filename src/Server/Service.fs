module Service

open InvoiceExcel
open LiteDB
open System.IO
open System
open FsToolkit.ErrorHandling
open Shared.Invoice
open Report

type DownloadFile =
    { FileName: string
      ContentType: string
      Stream: Stream }

let generateInvoiceNumber (coll:ILiteCollection<Dto.Invoice>) (period:DateTime) =
      let y = period.Year
      let m = period.Month
      let invoicesOfPeriod =
          coll
              .Query()
              .Where(fun f ->
                  f.AccountingPeriod.Year = y
                  && f.AccountingPeriod.Month = m)
              .ToArray()

      let rec getInvoiceNumber (invoices: Dto.Invoice seq) sequenceNumber =
          let invNumber =
              sprintf "%i%02i%02i" period.Year period.Month sequenceNumber

          let result =
              invoices
              |> Seq.tryFind
                  (fun i -> (String.IsNullOrEmpty(i.InvoiceNumber) = false)
                            && i.InvoiceNumber.Equals(invNumber, StringComparison.OrdinalIgnoreCase))

          match result with
          | None -> invNumber, sequenceNumber
          | Some _ -> getInvoiceNumber invoices (sequenceNumber + 1)

      getInvoiceNumber invoicesOfPeriod 1


let getInvoiceFileName (period:DateTime) index =
    sprintf "Faktura - %i-%02i-%02i" period.Year period.Month index

let getTotal (connectionString: string) year =
    use db = new LiteDatabase(connectionString)

    let s = DateTime(year, 1, 1)
    let e = s.AddYears(1)

    db
        .GetCollection<Dto.Invoice>("invoices")
        .Query()
        .Where(fun i -> i.AccountingPeriod >= s && i.AccountingPeriod < e)
        .ToArray()
    |> Array.sumBy
        (fun a ->
            let total = a.Rate * uint32 a.ManDays
            total)

let generateInvoiceExcel (connectionString: string) (invoiceId: Guid) =
    use db = new LiteDatabase(connectionString)

    let invoices =
        db.GetCollection<Dto.Invoice>("invoices")
    let invoiceDto = invoices.FindById(BsonValue(invoiceId))

    let invoice =
        Dto.fromInvoiceDto invoiceDto
        |> Result.valueOr failwith

    let fileName = invoice.InvoiceNumber


    let stream = generateExcelData invoice

    { FileName = fileName
      ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
      Stream = stream }


let generateSummaryReport (connectionString: string) (reportType: SummaryReportType) =
    match reportType with
    | AnnualTax ->
        let year = DateTime.Now.AddYears(-1).Year

        generateAnnualTaxReport
            { Year = year |> uint16
              ExpensesType = Virtual 60uy
              DateOfFill = DateTime.Now
              TotalEarnings = getTotal connectionString year
              PenzijkoAttachment = None }
    | QuartalVatAnnounce ->
        let quarter = getPreviousQuarter DateTime.Now
        use db = new LiteDatabase(connectionString)
        let year = quarter.Start.Year
        let startMonth = quarter.Start.Month
        let endMonth = quarter.End.AddMonths(-1).Month

        db
            .GetCollection<Dto.Invoice>("invoices")
            .Query()
            .Where(fun f ->
                f.AccountingPeriod.Year = year
                && f.AccountingPeriod.Month >= startMonth
                && f.AccountingPeriod.Month <= endMonth)
            .ToArray()
        |> List.ofSeq
        |> List.traverseResultA Dto.fromInvoiceDto
        |> Result.bind
            (fun is ->
                generateTaxAnnouncementReport
                    { Period = Quarter quarter
                      DateOfFill = DateTime.Now
                      Invoices = is })
    | _ -> failwith $"Not implemented {reportType}"


let generateSummaryReportFile (connectionString: string) (reportType: SummaryReportType) =


    generateSummaryReport connectionString reportType
    |> Result.map
        (fun s ->
            let fileName =
                match reportType with
                | AnnualTax -> "dpfdp5_epo2.xml"
                | QuartalVatAnnounce -> "dphkh1_epo2.xml"
                | _ -> failwith $"Not implemented {reportType}"

            { FileName = fileName
              ContentType = "application/xml"
              Stream = s })
