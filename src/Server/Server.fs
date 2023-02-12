module Server

open System.Net.Http
open System.Xml.Linq
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Core
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
open Service
open System.Linq


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
        fun invoiceReq ->
            async {
                try
                    use db = new LiteDatabase(connectionString)
                    let coll = db.GetCollection<Dto.Invoice>("invoices")

                    let invoiceNumber, seqNumber =
                        generateInvoiceNumber coll invoiceReq.AccountingPeriod

                    let invoice =
                        { Id = NewId.NextGuid()
                          InvoiceNumber = invoiceNumber
                          Items =
                            [ InvoiceItemInfo(ManDay(invoiceReq.Rate, invoiceReq.ManDays), None, false)
                              match invoiceReq.AdditionalItem with
                              | Some a -> InvoiceItemInfo(Additional(a), None, false)
                              | None -> () ]
                          AccountingPeriod = invoiceReq.AccountingPeriod.Date
                          DateOfTaxableSupply = invoiceReq.DateOfTaxableSupply
                          DueDate = invoiceReq.DueDate
                          OrderNumber = invoiceReq.OrderNumber
                          Vat = invoiceReq.Vat
                          Customer = invoiceReq.Customer
                          Created = DateTime.Now }

                    invoice |> Dto.toInvoiceDto |> coll.Insert |> ignore

                    let outputFile =
                        Path.Combine(documentOutputDir, getInvoiceFileName invoiceReq.AccountingPeriod seqNumber)

                    createExcelAndPdfInvoice outputFile invoice

                    return Ok invoice
                with ex ->
                    return Error <| sprintf "%s" ex.Message
            }
      getCustomers =
        fun () ->
            async {
                try
                    use db = new LiteDatabase(connectionString)

                    let invoices = db.GetCollection<Dto.Invoice>("invoices")
                    //let customers = invoices.FindAll().Select(fun f->f.Customer)
                    // BsonExpression.Create("group by") // todo
                    //   let lol = BsonMapper.Global.GetExpression(fun i -> i.Customer)
                    //   let troll = invoices.Query().GroupBy(lol).Select(lol).ToArray();
                    return
                        invoices
                            .Query()
                            .OrderByDescending(fun i -> i.Created)
                            .Select(fun i -> i.Customer)
                            .ToArray()
                        |> List.ofSeq
                        |> List.groupBy id
                        |> List.map fst
                        |> List.traverseResultA Dto.fromCustomerDto
                        |> Result.mapError (String.concat ", ")

                with e ->
                    return Error e.Message
            }
      getInvoices =
        fun pageNumber pageSize ->
            async {
                try
                    use db = new LiteDatabase(connectionString)

                    let total =
                        db.GetCollection<Dto.Invoice>("invoices").Query().Count()

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
                        |> Result.mapError (String.concat Environment.NewLine)
                with e ->
                    return Error e.Message
            }
      getInvoiceDefaults =
        fun () ->
            async {
                try
                    let lastMonth = DateTime.Now.AddMonths(-1)

                    let manDays =
                        [ 1 .. DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month) ]
                        |> List.filter (fun d ->
                            not (
                                [ DayOfWeek.Saturday; DayOfWeek.Sunday ]
                                |> List.contains (DateOnly(lastMonth.Year, lastMonth.Month, d).DayOfWeek)
                            ))
                        |> List.length
                        |> uint8


                    use db = new LiteDatabase(connectionString)

                    let coll = db.GetCollection<Dto.Invoice>("invoices")

                    let invoiceNumber, _ = generateInvoiceNumber coll lastMonth

                    let dateOfTaxSupply = getLastDayOfMonth lastMonth

                    let defaults: InvoiceDefaults =
                        { InvoiceDefaults.Default with
                            InvoiceNumber = invoiceNumber
                            DateOfTaxableSupply = dateOfTaxSupply
                            ManDays = manDays }

                    let lastInvoice =
                        coll
                            .Query()
                            .OrderByDescending(fun x -> x.AccountingPeriod)
                            .Limit(10)
                            .ToArray()
                            .OrderByDescending(fun x -> x.AccountingPeriod)
                            .ThenByDescending(fun x -> x.Created)
                            .FirstOrDefault()
                        |> (fun x -> if ((box x) = null) then None else Some x)
                        |> Option.map Dto.fromInvoiceDto

                    return
                        match lastInvoice with
                        | Some i ->
                            i
                            |> Result.map (fun x ->
                                let rate = x |> getManDayAndRate |> Option.map fst

                                match rate with
                                | Some r -> { defaults with Customer = Some x.Customer; Rate = r }
                                | None -> { defaults with Customer = Some x.Customer })
                            |> Result.mapError (String.concat Environment.NewLine)
                        | _ -> Ok defaults

                with e ->
                    return Error e.Message
            }
      removeInvoice =
        fun id ->
            async {
                try
                    use db = new LiteDatabase(connectionString)

                    let isDeleted =
                        db.GetCollection<Dto.Invoice>("invoices").Delete(BsonValue(id))

                    if (isDeleted) then
                        return Ok()
                    else
                        return Error <| sprintf "Invoice with id %A was not removed." id
                with e ->
                    return Error e.Message
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
                with e ->
                    return Error e.Message
            }
      getTotals =
        fun () ->
            async {
                try
                    use db = new LiteDatabase(connectionString)
                    let coll = db.GetCollection<Dto.Invoice>("invoices")

                    return getTotals coll |> Result.mapError (String.concat Environment.NewLine)
                with e ->
                    return Error e.Message
            }

      prepareSummaryReportUrl =
          fun type' ->
              async {
                  try
                      use db = new LiteDatabase(connectionString)
                      let coll = db.GetCollection<Dto.Invoice>("invoices")
                      let stream = generateSummaryReport coll type'

                      match stream with
                      | Error errs -> return Error(errs |> String.concat Environment.NewLine)
                      | Ok stream ->

                          use stream = stream
                          do stream.Position <- 0L

                          use content = new StreamContent(stream)
                          content.Headers.Add("Content-Type", "application/xml")
                          let httpClientHandler = new HttpClientHandler()
                          use httpClient = new HttpClient(httpClientHandler)


                          let url =
                              "https://adisspr.mfcr.cz/dpr/epo_podani?otevriFormular=1"

                          use requestMessage =
                              new HttpRequestMessage(HttpMethod.Post, url)

                          requestMessage.Content <- content

                          use! response = httpClient.PostAsync(url, content) |> Async.AwaitTask

                          let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                          if (not response.IsSuccessStatusCode) then
                              return Error $"Remote api error {body}"
                          else
                              let url = (XDocument.Parse body).Root.Value
                              return Ok url
                  with e ->
                      return Error(e.Message + e.StackTrace)
              } }

let downloadHandler file : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            ctx.SetHttpHeader("Content-Disposition", $"inline; filename=\"{file.FileName}\"")
            ctx.SetHttpHeader("Content-Type", file.ContentType)
            use stream = file.Stream
            return! streamData false stream None None next ctx
        }



let summaryReportHandler =
    use db = new LiteDatabase(connectionString)
    let coll = db.GetCollection<Dto.Invoice>("invoices")

    SummaryReportType.fromString
    >> generateSummaryReportFile coll
    >> Result.map downloadHandler
    >> Result.valueOr (fun e -> setStatusCode 500 >=> text (String.concat "\n" e))

let errorHandler (ex: Exception) (routeInfo: RouteInfo<HttpContext>) =
    let contextLogger = routeInfo.httpContext.GetLogger()
    contextLogger.Log(LogLevel.Error, ex.Message)
    Ignore


let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue invoiceApi
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

let router =
    choose
        [ GET
          >=> routef "/api/generateInvoiceDocument/%O" (generateInvoiceExcel connectionString >> downloadHandler)
          GET >=> routef "/api/generateSummaryReport/%s" summaryReportHandler
          routeStartsWith "/api" >=> webApp
          setStatusCode 404 >=> text "Not found" ]


let app =
    application {
        //url "http://localhost:8085"
        use_router router
        memory_cache
        use_static "public"
        use_gzip
    //use_client_certificate
    }


run app