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
                              "C:\\Users\\SpinGee\\Desktop\\Faktura - %i-%02i-01"
                              invoice.AccountingPeriod.Year
                              invoice.AccountingPeriod.Month

                      createExcelAndPdfInvoice outputFile invoice


                      use db =
                          new LiteDatabase("FileName=simple.db;Connection=shared")

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
                      use db =
                          new LiteDatabase("FileName=simple.db;Connection=shared")

                      let invoices =
                          db.GetCollection<Dto.Invoice>("invoices")
                      //let customers = invoices.FindAll().Select(fun f->f.Customer)
                      // BsonExpression.Create("group by") // todo
                      let custs =
                          invoices.Query().Select(fun i -> i.Customer).ToArray()
                          |> Array.groupBy id
                          |> Array.map fst
                          |> Array.map Dto.fromCustomerDto
                          |> Array.map (function
                              | Ok c -> Some c
                              | _ -> None)
                          |> Array.choose id
                          |> Array.rev

                      return Ok [ yield! custs ]
                  with e -> return Error e.Message

              }
      getInvoices =
          fun () ->
              async {
                  try
                      use db =
                          new LiteDatabase("FileName=simple.db;Connection=shared")

                      let invoices =
                          db.GetCollection<Dto.Invoice>("invoices").FindAll()
                          |> Seq.map Dto.fromInvoiceDto
                          |> Seq.map (function
                              | Ok c -> Some c
                              | _ -> None)
                          |> Seq.choose id
                          |> List.ofSeq

                      return Ok invoices
                  with e -> return Error e.Message

              }
      removeInvoice =
          fun id ->
              async {
                  try
                      use db =
                          new LiteDatabase("FileName=simple.db;Connection=shared")

                      let isDeleted =
                          db.GetCollection<Dto.Invoice>("invoices").Delete(BsonValue(id))

                      if (isDeleted) then return Ok() else return Error "Invoice was not removed."


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
run app
