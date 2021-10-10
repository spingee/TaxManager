module Service

open InvoiceExcel
open LiteDB
open System.IO
open System
open FsToolkit.ErrorHandling
open Shared.Invoice
open SummaryReportGenerator
open Shared

type DownloadFile =
    { FileName: string
      ContentType: string
      Stream: Stream }

let getInvoiceFileName year month index =
    sprintf "Faktura - %i-%02i-%02i" year month index

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

    let index =
        invoices
            .Query()
            .Where(fun f ->
                f.AccountingPeriod.Year = invoiceDto.AccountingPeriod.Year
                && f.AccountingPeriod.Month = invoiceDto.AccountingPeriod.Month)
            .OrderBy(fun inv -> inv.Inserted)
            .ToArray()
        |> Array.findIndex (fun inv -> inv.Id = invoiceId)

    let invoice =
        Dto.fromInvoiceDto invoiceDto
        |> Result.valueOr failwith

    let fileName =
        getInvoiceFileName invoiceDto.AccountingPeriod.Year invoiceDto.AccountingPeriod.Month (index + 1)

    let stream = generateExcelData invoice (index + 1)

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
    | _ -> failwith $"Not implemented {reportType}"


let generateSummaryReportFile (connectionString: string) (reportType: SummaryReportType) =
    match reportType with
    | AnnualTax ->
        let year = DateTime.Now.AddYears(-1).Year

        generateSummaryReport connectionString reportType
        |> Result.map
            (fun s ->
                { FileName = "dpfdp5_epo2.xml"
                  ContentType = "application/xml"
                  Stream = s })


    | _ -> failwith $"Not implemented {reportType}"
