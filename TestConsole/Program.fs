open System
open System.IO
open InvoiceExcel
open GemBox.Spreadsheet
open LiteDB



[<EntryPoint>]
let main argv =
    SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")
    // let lol = readFromExcel @"C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury\DPH 2020-4\Výstup\Faktura - 2020-12-01.xlsx"
    // let lol1 = readFromExcel @"C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury\DPH 2020-1\Výstup\Faktura - 2020-03-01_Principal.xlsx"
    // let lol2 = readFromExcel @"C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury\DPH 2016-4\výstup\Faktura  - 2016-10-01_Adcon.xlsx"
    // let lol3 = readFromExcel @"C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury\DPH 2011-3\Vystup\Faktura  - 2011-09-01_Adcon.xlsx"
    // let lol4 = readFromExcel @"C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury\Faktura  - 2009-03-01_NESS.xlsx"
    let all =
        Directory.EnumerateFiles("C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury","*.xlsx", SearchOption.AllDirectories)
        |> Seq.filter (fun f-> f.StartsWith("C:\\Users\\SpinGee\\SkyDrive\\Dokumenty\\Faktury\\TimeSheets\\",StringComparison.OrdinalIgnoreCase) |> not)
        |> Seq.map readFromExcel
    let connectionString =
        @"FileName=./db/taxmanager.db;Connection=shared"
    Directory.CreateDirectory("./db") |> ignore;
    use db = new LiteDatabase(connectionString)

    let invoices =
      db.GetCollection<Dto.Invoice>("invoices")

    let invoice =
      all
      |> Seq.map (fun i -> Dto.toInvoiceDto DateTime.Now i)

    invoices.InsertBulk(invoice) |> ignore

    Console.ReadLine() |> ignore
    0
