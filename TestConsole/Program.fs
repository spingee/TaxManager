open System
open System.IO
open InvoiceExcel
open GemBox.Spreadsheet
open LiteDB
open FsToolkit.ErrorHandling

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

    let invoices =
      db.GetCollection<Dto.Invoice>("invoices")

    let invoice =
      all
      |> Seq.map (fun i -> Dto.toInvoiceDto DateTime.Now i)

    invoices.InsertBulk(invoice) |> ignore

[<EntryPoint>]
let main argv =
      //import() |> ignore
      use db = new LiteDatabase(connectionString)
      let y = DateTime.Now.AddYears(-1)
      let s = DateTime(y.Year,1,1)
      let e = s.AddYears(1)

      let lastYear =
          db
              .GetCollection<Dto.Invoice>("invoices")
              .Query()
              .Where(fun i -> i.AccountingPeriod >= s && i.AccountingPeriod < e)
              .ToArray()
          |> Array.sumBy
              (fun a ->
                  let total = a.Rate * uint32 a.ManDays

                  match Option.ofNullable a.Vat with
                  | None -> total
                  | Some v -> total + (total / uint32 100 * uint32 v))

      let quarterStart, quarterEnd =
          match DateTime.Now.Month with
          | 1
          | 2
          | 3 -> 10, 12, DateTime.Now.Year - 1
          | 4
          | 5
          | 6 -> 1, 3,DateTime.Now.Year
          | 7
          | 8
          | 9 -> 4, 6,DateTime.Now.Year
          | 10
          | 11
          | 12 -> 7, 9,DateTime.Now.Year
          | _ -> failwith "nonsense"
          |> fun (s,e,y) -> DateTime(y,s,1),DateTime(y,e,1).AddMonths(1)


      let lastQuarterBase =
          db
              .GetCollection<Dto.Invoice>("invoices")
              .Query()
              .Where(fun i -> i.AccountingPeriod >= quarterStart && i.AccountingPeriod < quarterEnd)
              .ToArray()
      let lastQuarter =
          lastQuarterBase
          |> Array.sumBy
              (fun a ->
                  a.Rate * uint32 a.ManDays)
      let lastQuarterVat =
          lastQuarterBase
          |> Array.sumBy
              (fun a ->
                  let total = a.Rate * uint32 a.ManDays
                  match Option.ofNullable a.Vat with
                  | None -> total
                  | Some v -> total + (total / uint32 100 * uint32 v))

      Console.ReadLine() |> ignore
      0
