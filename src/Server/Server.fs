module Server

open System.Net
open System.Net.Http
open System.Text
open System.Threading
open System.Xml.Linq
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Microsoft.AspNetCore.Http
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
open System.Globalization
open Giraffe
open Auth
open FSharp.Control.Tasks
open SummaryReportGenerator
open Service
//open System.Linq

let connectionString =
    @"FileName=./db/taxmanager.db;Connection=shared"
#if DEBUG
let documentOutputDir =
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
#else
let documentOutputDir = "./output"
#endif
let cultureInfo = CultureInfo("cs-CZ")
do CultureInfo.DefaultThreadCurrentCulture <- cultureInfo
do CultureInfo.DefaultThreadCurrentUICulture <- cultureInfo
do SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")



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
                              getInvoiceFileName
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
                  with
                  | ex -> return Error <| sprintf "%s" ex.Message
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

                  with
                  | e -> return Error e.Message
              }
      getInvoices =
          fun pageNumber pageSize ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)

                      let total =
                          db
                              .GetCollection<Dto.Invoice>("invoices")
                              .Query()
                              .Count()

                      return
                          (db
                              .GetCollection<Dto.Invoice>("invoices")
                               .Query()
                               //jeste bych rad pro jistotu sortoval podle, inserted ale multi column sort prej az litedb 5.1
                               //kazdopadne guid id je sequencni tak snad je to serazeny podle primary indexu tak jak chci
                               .OrderByDescending(fun x -> x.AccountingPeriod)
                               .Offset(pageSize * (pageNumber - 1))
                               .Limit(pageSize)
                               .ToArray()
                           |> List.ofSeq
                           |> List.traverseResultM Dto.fromInvoiceDto)
                           |> Result.map (fun x -> x, total)
                  with
                  | e -> return Error e.Message
              }
      getInvoiceDefaults =
          fun () ->
              async {
                  try
                      let lastMonth= DateTime.Now.AddMonths(-1)
                      let mandays =
                        [1..DateTime.DaysInMonth (lastMonth.Year,lastMonth.Month) ]
                        |> List.filter (fun d -> not ([DayOfWeek.Saturday ; DayOfWeek.Sunday] |> List.contains (DateTime(lastMonth.Year,lastMonth.Month,d).DayOfWeek) ))
                        |> List.length
                        |> uint8

                      use db = new LiteDatabase(connectionString)
                      return
                          (db
                              .GetCollection<Dto.Invoice>("invoices")
                               .Query()
                               .OrderByDescending(fun x -> x.AccountingPeriod)
                               .FirstOrDefault()//
                               |> (fun x -> if ((box x) = null) then None else Some x)
                               |> Option.map (fun x-> {x with ManDays = mandays ; AccountingPeriod = lastMonth})
                               |> Option.traverseResult Dto.fromInvoiceDto
                           )

                  with
                  | e -> return Error e.Message
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
                  with
                  | e -> return Error e.Message
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
                  with
                  | e -> return Error e.Message
              }
      getTotals =
          fun s ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)
                      let y = DateTime.Now.AddYears(-1)
                      let s = DateTime(y.Year, 1, 1)
                      let e = s.AddYears(1)

                      let lastYear = getTotal connectionString y.Year


                      let quarterStart, quarterEnd, timeRange =
                          match DateTime.Now.Month with
                          | 1
                          | 2
                          | 3 -> 10, 12, DateTime.Now.Year - 1, "Q4"
                          | 4
                          | 5
                          | 6 -> 1, 3, DateTime.Now.Year, "Q1"
                          | 7
                          | 8
                          | 9 -> 4, 6, DateTime.Now.Year, "Q2"
                          | 10
                          | 11
                          | 12 -> 7, 9, DateTime.Now.Year, "Q3"
                          | _ -> failwith "nonsense"
                          |> fun (s, e, y, r) -> DateTime(y, s, 1), DateTime(y, e, 1).AddMonths(1), r


                      let lastQuarterBase =
                          db
                              .GetCollection<Dto.Invoice>("invoices")
                              .Query()
                              .Where(fun i ->
                                  i.AccountingPeriod >= quarterStart
                                  && i.AccountingPeriod < quarterEnd)
                              .ToArray()

                      let lastQuarter =
                          lastQuarterBase
                          |> Array.sumBy (fun a -> a.Rate * uint32 a.ManDays)

                      let lastQuarterVat =
                          lastQuarterBase
                          |> Array.sumBy
                              (fun a ->
                                  let total = a.Rate * uint32 a.ManDays

                                  match Option.ofNullable a.Vat with
                                  | None -> total
                                  | Some v -> (total / uint32 100 * uint32 v))

                      return
                          { LastYear =
                                { Value = decimal lastYear
                                  Currency = "CZK"
                                  TimeRange = y.Year.ToString() }
                            LastQuarter =
                                { Value = decimal lastQuarter
                                  Currency = "CZK"
                                  TimeRange = timeRange }
                            LastQuarterVat =
                                { Value = decimal lastQuarterVat
                                  Currency = "CZK"
                                  TimeRange = timeRange } }
                          |> Ok
                  with
                  | e -> return Error e.Message
              }
      prepareAnnualTaxReportUrl =
          fun () ->
              async {
                  try
                      let stream =
                          generateAnnualTaxReport {
                                Year = DateTime.Now.AddYears(-1).Year |> uint16
                                ExpensesType = Virtual 60uy
                                DateOfFill = DateTime.Now
                                TotalEarnings = 1600000u
                                PenzijkoAttachment = None
                          }

                      match stream with
                      | Error errs -> return Error (errs |> String.concat Environment.NewLine)
                      | Ok stream ->

                          use stream = stream
                          do stream.Position <- 0L

                          use content = new StreamContent(stream)
                          content.Headers.Add("Content-Type","application/xml")
                          let httpClientHandler = new HttpClientHandler()
                          use httpClient = new HttpClient(httpClientHandler)


                          let url = "https://adisspr.mfcr.cz/dpr/epo_podani?otevriFormular=1"
                          use requestMessage =
                                new HttpRequestMessage(
                                    HttpMethod.Post,
                                    url
                                )


                          requestMessage.Content <- content



                          use! response = httpClient.PostAsync(url,content) |> Async.AwaitTask
                          let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                          if (not response.IsSuccessStatusCode) then
                             return Error $"Remote api error {body}"
                          else
                            let url =
                                (XDocument.Parse body).Root.Value
                            return Ok url
                  with
                  | e -> return Error (e.Message + e.StackTrace)
              }
          }

let downloadHandler file : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                ctx.SetHttpHeader("Content-Disposition", $"inline; filename=\"{file.FileName}\"")
                ctx.SetHttpHeader("Content-Type", file.ContentType)
                use stream = file.Stream
                return! streamData false stream None None next ctx
            }



let summaryReportHandler =  SummaryReportType.fromString
                            >> generateSummaryReportFile connectionString
                            >> Result.map downloadHandler
                            >> Result.valueOr (fun e -> setStatusCode 500 >=> text (String.concat "\n" e))


let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue invoiceApi
    |> Remoting.buildHttpHandler
let router =
    choose [ GET  >=> routef "/api/generateInvoiceDocument/%O" (generateInvoiceExcel connectionString >> downloadHandler)
             GET  >=> routef "/api/generateSummaryReport/%s" summaryReportHandler
             routeStartsWith "/api" >=> webApp
             setStatusCode 404 >=> text "Not found" ]



let app =
    application {
        url "http://0.0.0.0:8085"
        use_router router
        memory_cache
        use_static "public"
        use_gzip
    //use_client_certificate
    }


//FontSettings.FontsBaseDirectory = "./fonts" |> ignore
run app
