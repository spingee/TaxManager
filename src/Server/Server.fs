module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open System
open Shared
open Invoice
open InvoiceExcel
open GemBox.Spreadsheet

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

                      return Ok "Success"
                  with ex -> return Error <| sprintf "%s" ex.Message

              }
      getCustomers =
          fun () ->
              let cust =
                  result {
                      let! vatId = createVatId "CZ26775794"

                      return { Id = Guid.NewGuid()
                               Name = "Principal engineering s.r.o."
                               IdNumber = uint 26775794
                               VatId = vatId }
                  }

              async {
                  match cust with
                  | Error e -> return Error e
                  | Ok c -> return Ok [ c ]
              } }

let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    //|> Remoting.fromValue todosApi
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
