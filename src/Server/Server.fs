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
//open System.Linq

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
                    
                     
                      use db = new LiteDatabase("simple.db")
                      let invoices = db.GetCollection<Dto.Invoice>("invoices")
                      let invoice = {invoice with Id = Guid.NewGuid()}
                                    |> Dto.toInvoiceDto
                      invoices.Insert(invoice) |> ignore

                      return Ok "Success"
                  with ex -> return Error <| sprintf "%s" ex.Message

              }
      getCustomers =
          fun () ->              
              use db = new LiteDatabase("simple.db")
              let invoices = db.GetCollection<Dto.Invoice>("invoices")
              //let customers = invoices.FindAll().Select(fun f->f.Customer)
              // BsonExpression.Create("group by") // todo
              let custs = invoices.Query().Select(fun i -> i.Customer).ToArray()
                            |> Array.groupBy id
                            |> Array.map (fun x -> fst x)
                            |> Array.map Dto.fromCustomerDto                           
                            |> Array.filter (function | Ok _ -> true | _ -> false )
                            |> Array.map (fun (Ok c) -> c)
              async {                 
                   return Ok [yield! custs]
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
