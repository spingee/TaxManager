open System
open System.IO
open System.IO
open FsToolkit.ErrorHandling.Operator.Validation
open InvoiceExcel
open GemBox.Spreadsheet
open LiteDB
open SummaryReportGenerator
open FsToolkit.ErrorHandling

let fff<'t> = System.Collections.Generic.List<'t>()

let connectionString =
    @"FileName=./db/taxmanager.db;Connection=shared"

let import () =
    SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")

    let all =
        Directory.EnumerateFiles("C:\Users\SpinGee\SkyDrive\Dokumenty\Faktury", "*.xlsx", SearchOption.AllDirectories)
        |> Seq.filter
            (fun f ->
                f.StartsWith(
                    "C:\\Users\\SpinGee\\SkyDrive\\Dokumenty\\Faktury\\TimeSheets\\",
                    StringComparison.OrdinalIgnoreCase
                )
                |> not)
        |> Seq.map readFromExcel

    Directory.CreateDirectory("./db") |> ignore
    use db = new LiteDatabase(connectionString)

    let invoice =
        db
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


type Paint =
    private | Paint' of volume : float * pigment : int

    member me.Volume =
        let (Paint' (volume=value)) = me in value

    member me.Pigment =
        let (Paint' (pigment=value)) = me in value



[<EntryPoint>]
let main argv =
    let apply f x =
        Result.bind (fun f' ->
          Result.bind (fun x' -> Ok (f' x')) x) f
    let funlol i: Result<unit,string> = Error  $"%i{i}"
    let tlol =
        validation {
            let! s = funlol -1
            and! k = funlol 0
            and! z = validation{
                return!
                    [1..3]
                    |> List.map (funlol >> Validation.ofResult)
                    |> List.reduce (fun a b -> (Validation.zip a b) |> Validation.map(fun x-> ()))
            }
//            for i in [1..3] do
//                and! _ = funlol i
            return()
        }
    let att =
        File.ReadAllBytes("C:\Users\janst\OneDrive\Dokumenty\Faktury\danovy priznani 2017\penzijko.jpg")

    let lol =
        generateAnnualTaxReport
            { Year = 2021us
              ExpensesType = Virtual 60uy
              DateOfFill = DateTime.Now
              TotalEarnings = 1600000u
              PenzijkoAttachment = att
               }
        |> Result.tee (fun x -> x.Save(@"C:\Users\janst\Desktop\test.xml"))
        |> Result.teeError Console.WriteLine


    //a.Add(1)
    Console.ReadLine() |> ignore
    0
