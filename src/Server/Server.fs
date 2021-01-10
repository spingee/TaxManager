module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open System
open Shared
open Invoice
open InvoiceExcel
open GemBox.Spreadsheet
open LiteDB
open FsToolkit.ErrorHandling
open System.IO
open MassTransit
//open System.Linq

let connectionString =
    @"FileName=./db/taxmanager.db;Connection=shared"
#if DEBUG
let documentOutputDir = @"C:\Users\SpinGee\Desktop"
#else
let documentOutputDir = "./output"
#endif

let invoiceApi =
    { addInvoice =
          fun invoice ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)


                      let invoices =
                          db.GetCollection<Dto.Invoice>("invoices")

                      let dateYear = invoice.AccountingPeriod.Date.Year
                      let dateMonth = invoice.AccountingPeriod.Date.Month

                      let samePeriodCount =
                          invoices
                              .Query()
                              .Where(fun f ->
                                  f.AccountingPeriod.Year = dateYear
                                  && f.AccountingPeriod.Month = dateMonth)
                              .Count()

                      let indexNumber = samePeriodCount + 1

                      let outputFile =
                          Path.Combine(
                              documentOutputDir,
                              sprintf
                                  "Faktura - %i-%02i-%02i"
                                  invoice.AccountingPeriod.Year
                                  invoice.AccountingPeriod.Month
                                  indexNumber
                          )

                      createExcelAndPdfInvoice outputFile invoice indexNumber


                      use db = new LiteDatabase(connectionString)

                      let invoices =
                          db.GetCollection<Dto.Invoice>("invoices")

                      let invoice =
                          { invoice with Id = NewId.NextGuid() }
                          |> Dto.toInvoiceDto DateTime.Now

                      invoices.Insert(invoice) |> ignore

                      return Ok invoice.Id
                  with ex -> return Error <| sprintf "%s" ex.Message
              }
      getCustomers =
          fun () ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)

                      let invoices =
                          db.GetCollection<Dto.Invoice>("invoices")
                      //let customers = invoices.FindAll().Select(fun f->f.Customer)
                      // BsonExpression.Create("group by") // todo
                      //   let lol = BsonMapper.Global.GetExpression(fun i -> i.Customer)
                      //   let troll = invoices.Query().GroupBy(lol).Select(lol).ToArray();
                      return
                          invoices
                              .Query()
                              .OrderByDescending(fun i -> i.Inserted)
                              .Select(fun i -> i.Customer)
                              .ToArray()
                          |> List.ofSeq
                          |> List.groupBy id
                          |> List.map fst
                          |> List.traverseResultA Dto.fromCustomerDto
                          |> Result.mapError (fun e -> String.concat ", " e)

                  with e -> return Error e.Message
              }
      getInvoices =
          fun () ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)

                      return
                          db
                              .GetCollection<Dto.Invoice>("invoices")
                              .FindAll()
                          |> List.ofSeq
                          |> List.sortByDescending
                              (fun x -> x.AccountingPeriod.Year, x.AccountingPeriod.Month, x.Inserted)
                          |> List.traverseResultM Dto.fromInvoiceDto
                  with e -> return Error e.Message
              }
      removeInvoice =
          fun id ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)

                      let isDeleted =
                          db
                              .GetCollection<Dto.Invoice>("invoices")
                              .Delete(BsonValue(id))

                      if (isDeleted) then
                          return Ok()
                      else
                          return
                              Error
                              <| sprintf "Invoice with id %A was not removed." id
                  with e -> return Error e.Message
              }
      searchOrderNumber =
          fun s ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)

                      return
                          db
                              .GetCollection<Dto.Invoice>("invoices")
                              .Query()
                              .Where(fun i -> i.OrderNumber.StartsWith(s))
                              .Select(fun i -> i.OrderNumber)
                              .ToList()
                          |> List.ofSeq
                          |> List.distinct
                          |> List.sortByDescending id
                          |> Ok
                  with e -> return Error e.Message
              }
      getTotals =
          fun s ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)
                      let y = DateTime.Now.AddYears(-1)
                      let s = DateTime(y.Year,1,1)
                      let e = s.AddYears(1)

                      let lastYear =
                          db
                              .GetCollection<Dto.Invoice>("invoices")
                              .Query()
                              .Where(fun i -> i.AccountingPeriod >= s && i.AccountingPeriod < e)
                              .ToArray()
                          |> Array.sumBy
                              (fun a ->
                                  let total = a.Rate * uint32 a.ManDays

                                  match Option.ofNullable a.Vat with
                                  | None -> total
                                  | Some v -> total + (total / uint32 100 * uint32 v))

                      let quarterStart, quarterEnd =
                          match DateTime.Now.Month with
                          | 1
                          | 2
                          | 3 -> 10, 12, DateTime.Now.Year - 1
                          | 4
                          | 5
                          | 6 -> 1, 3,DateTime.Now.Year
                          | 7
                          | 8
                          | 9 -> 4, 6,DateTime.Now.Year
                          | 10
                          | 11
                          | 12 -> 7, 9,DateTime.Now.Year
                          | _ -> failwith "nonsense"
                          |> fun (s,e,y) -> DateTime(y,s,1),DateTime(y,e,1).AddMonths(1)


                      let lastQuarterBase =
                          db
                              .GetCollection<Dto.Invoice>("invoices")
                              .Query()
                              .Where(fun i -> i.AccountingPeriod >= quarterStart && i.AccountingPeriod < quarterEnd)
                              .ToArray()
                      let lastQuarter =
                          lastQuarterBase
                          |> Array.sumBy
                              (fun a ->
                                  a.Rate * uint32 a.ManDays)
                      let lastQuarterVat =
                          lastQuarterBase
                          |> Array.sumBy
                              (fun a ->
                                  let total = a.Rate * uint32 a.ManDays
                                  match Option.ofNullable a.Vat with
                                  | None -> total
                                  | Some v -> (total / uint32 100 * uint32 v))

                      return
                          { LastYearTotal = lastYear
                            LastQuarterTotal = lastQuarter
                            LastQuarterTotalVat = lastQuarterVat}
                          |> Ok
                  with e -> return Error e.Message
              } }

let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue invoiceApi
    |> Remoting.buildHttpHandler

let app =
    application {
        url "http://0.0.0.0:8085"
        use_router webApp
        memory_cache
        use_static "public"
        use_gzip
    }

SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")
//FontSettings.FontsBaseDirectory = "./fonts" |> ignore
run app
