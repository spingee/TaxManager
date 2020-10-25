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
open LiteDB.FSharp
open LiteDB.FSharp.Extensions
open System.Linq

let invoiceApi =
    { addInvoice =
          fun invoice ->
              async {
                  try
                      let outputFile =
                          sprintf
                              "C:\\Users\\SpinGee\\Desktop\\Faktura - %i-%02i-01.xlsx"
                              invoice.AccountingPeriod.Year
                              invoice.AccountingPeriod.Month

                      createExcelAndPdfInvoice outputFile invoice

                      let mapper = FSharpBsonMapper()
                      use db = new LiteDatabase("simple.db", mapper)
                      let invoices = db.GetCollection<Invoice>("invoices")
                      let invoice = {invoice with Id = Guid.NewGuid()}

                      invoices.Insert(invoice) |> ignore

                      return Ok "Success"
                  with ex -> return Error <| sprintf "%s" ex.Message

              }
      getCustomers =
          fun () ->
              let mapper = FSharpBsonMapper() 
              use db = new LiteDatabase("simple.db", mapper)
              let invoices = db.GetCollection<Invoice>("invoices")
              let customers = invoices.FindAll().Select(fun f->f.Customer)
              

              async {                 
                   return Ok [ yield! customers ]
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
run app
