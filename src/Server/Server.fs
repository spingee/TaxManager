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
                      let samePeriodCount =
                          invoices.Query()
                            .Where(fun f -> f.AccountingPeriod = invoice.AccountingPeriod)
                            .Count();
                      let indexNumber = samePeriodCount + 1;

                      let outputFile =
                          Path.Combine
                              (documentOutputDir,
                               sprintf
                                   "Faktura - %i-%02i-%02i"
                                   invoice.AccountingPeriod.Year
                                   invoice.AccountingPeriod.Month
                                   indexNumber)

                      createExcelAndPdfInvoice outputFile invoice indexNumber


                      use db = new LiteDatabase(connectionString)

                      let invoices =
                          db.GetCollection<Dto.Invoice>("invoices")

                      let invoice =
                          { invoice with Id = Guid.NewGuid() }
                          |> Dto.toInvoiceDto

                      invoices.Insert(invoice) |> ignore

                      return Ok "Success"
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
                      return invoices.Query().Select(fun i -> i.Customer).ToArray()
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

                      return db.GetCollection<Dto.Invoice>("invoices").FindAll()
                             |> List.ofSeq
                             |> List.traverseResultM Dto.fromInvoiceDto
                  with e -> return Error e.Message

              }
      removeInvoice =
          fun id ->
              async {
                  try
                      use db =
                          new LiteDatabase(connectionString)

                      let isDeleted =
                          db.GetCollection<Dto.Invoice>("invoices").Delete(BsonValue(id))

                      if (isDeleted) then return Ok() else return Error <| sprintf "Invoice with id %A was not removed." id
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
