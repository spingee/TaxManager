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
                      //   let lol = BsonMapper.Global.GetExpression(fun i -> i.Customer)
                      //   let troll = invoices.Query().GroupBy(lol).Select(lol).ToArray();
                      return invoices.Query().Select(fun i -> i.Customer).ToArray()
                             |> List.ofSeq
                             |> List.groupBy id
                             |> List.map fst
                             |> List.map Dto.fromCustomerDto
                             |> List.sequenceResultA
                             |> Result.mapError (fun e -> String.concat ", " e)

                  with e -> return Error e.Message

              }
      getInvoices =
          fun () ->
              async {
                  try
                      use db =
                          new LiteDatabase("FileName=simple.db;Connection=shared")

                      return db.GetCollection<Dto.Invoice>("invoices").FindAll()
                             |> Seq.map Dto.fromInvoiceDto
                             |> List.ofSeq
                             |> List.sequenceResultM


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
