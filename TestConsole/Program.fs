open System
open System.IO
open Report
open FsToolkit.ErrorHandling
open Shared

let fff<'t> = System.Collections.Generic.List<'t>()

let connectionString =
    @"FileName=./db/taxmanager.db;Connection=shared"


type Paint =
    private
    | Paint' of volume: float * pigment: int

    member me.Volume =
        let (Paint' (volume = value)) = me in value

    member me.Pigment =
        let (Paint' (pigment = value)) = me in value



[<EntryPoint>]
let main argv =
    let att =
        File.ReadAllBytes("C:\Users\janst\OneDrive\Dokumenty\Faktury\danovy priznani 2017\penzijko.jpg")


    let vatIdResult =
        (Invoice.createVatId "CZ56464")
        |> Result.defaultValue (Invoice.VatId "XXX")

    let lol2 =
        generateVatAnnouncementReport
            { Period = Quarter (Invoice.getQuarter DateTime.Now)
              DateOfFill = DateTime.Now
              Invoices =
                  [ { Id = Guid.NewGuid()
                      AccountingPeriod = DateTime(2021, 12, 31)
                      ManDays = 22uy
                      Rate = 8000u
                      InvoiceNumber = "sdad"
                      DateOfTaxableSupply = DateTime(2021, 12, 31)
                      OrderNumber = None
                      Vat = Some 21uy
                      Customer =
                          { IdNumber = 564654u
                            VatId = vatIdResult
                            Name = "lol"
                            Address = "sadasd"
                            Note = None } } ] }
        |> Result.tee (fun x -> use x = x in use fileStream = new FileStream(@"C:\Users\janst\Desktop\test.xml", FileMode.Create) in x.CopyTo(fileStream))
        |> Result.teeError Console.WriteLine

    let lol =
        generateAnnualTaxReport
            { Year = 2021us
              ExpensesType = Virtual 60uy
              DateOfFill = DateTime.Now
              TotalEarnings = 1600000u
              PenzijkoAttachment = Some att }
        |> Result.tee (fun x -> use x = x in use fileStream = new FileStream(@"C:\Users\janst\Desktop\test.xml", FileMode.Create) in x.CopyTo(fileStream))
        |> Result.teeError Console.WriteLine


    //a.Add(1)
    Console.ReadLine() |> ignore
    0