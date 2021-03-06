open System
open System.IO
open InvoiceExcel
open GemBox.Spreadsheet
open LiteDB
open FsToolkit.ErrorHandling

let fff<'t> = System.Collections.Generic.List<'t>()

let connectionString =
        @"FileName=./db/taxmanager.db;Connection=shared"

let import() =
    SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")
    let all =
        Directory.EnumerateFiles("C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury","*.xlsx", SearchOption.AllDirectories)
        |> Seq.filter (fun f-> f.StartsWith("C:\\Users\\SpinGee\\SkyDrive\\Dokumenty\\Faktury\\TimeSheets\\",StringComparison.OrdinalIgnoreCase) |> not)
        |> Seq.map readFromExcel

    Directory.CreateDirectory("./db") |> ignore;
    use db = new LiteDatabase(connectionString)
    let invoice = db
                   .GetCollection<Dto.Invoice>("invoices")
                   .Query()
                   .OrderByDescending(fun x -> x.AccountingPeriod)
                   .FirstOrDefault()
    let invoices =
      db.GetCollection<Dto.Invoice>("invoices")

    let invoice =
      all
      |> Seq.map (fun i -> Dto.toInvoiceDto DateTime.Now i)

    invoices.InsertBulk(invoice) |> ignore

[<EntryPoint>]
let main argv =
    Directory.CreateDirectory("./db") |> ignore;
    use db = new LiteDatabase(connectionString)
    let invoice = db
                   .GetCollection<Dto.Invoice>("invoices")
                   .Query()
                   .OrderByDescending(fun x -> x.AccountingPeriod)
                   .FirstOrDefault()
                   |> (fun x -> if ((box x) = null) then None else Some x)

    //a.Add(1)
    Console.ReadLine() |> ignore
    0
