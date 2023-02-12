open System
open System.IO
open Report
open FsToolkit.ErrorHandling
open Shared
open LiteDB
open System.Collections
open Shared.Invoice

let fff<'t> = System.Collections.Generic.List<'t>()

let connectionString =
    @"FileName=/Users/janstrnad/OneDrive/Dokumenty/Faktury/TaxManagerDb/taxmanager.db;Connection=shared"

let connectionString2 =
    @"FileName=/Users/janstrnad/OneDrive/Dokumenty/Faktury/TaxManagerDb/taxmanager_new.db;Connection=shared"


type Paint =
    private
    | Paint' of volume: float * pigment: int

    member me.Volume = let (Paint'(volume = value)) = me in value

    member me.Pigment = let (Paint'(pigment = value)) = me in value

[<CLIMutable>]
type Customer =
    { IdNumber: uint
      VatId: string
      Name: string
      Address: string
      Note: string }

type InvoiceItemUnionCase =
    | ManDay = 1
    | Additional = 2

[<CLIMutable>]
type InvoiceItem =
    { ManDays: Nullable<uint8>
      Rate: Nullable<uint32>
      Total: Nullable<decimal>
      Label: string
      Separate: bool
      InvoiceItemUnionCase: InvoiceItemUnionCase }

[<CLIMutable>]
type Invoice =
    { Id: Guid
      InvoiceNumber: string
      Items: InvoiceItem array
      AccountingPeriod: DateTime
      DateOfTaxableSupply: DateTime
      OrderNumber: string
      Vat: Nullable<int>
      Customer: Customer
      Created: DateTime }


[<EntryPoint>]
let main argv =
    let cultureInfo = System.Globalization.CultureInfo("cs-CZ")

    do System.Threading.Thread.CurrentThread.CurrentCulture <- cultureInfo
    do System.Threading.Thread.CurrentThread.CurrentUICulture <- cultureInfo

    use db = new LiteDatabase(connectionString)
    use db2 = new LiteDatabase(connectionString2)
    let coll = db.GetCollection<Dto.Invoice>("invoices")
    //let coll2 = db2.GetCollection<Dto.Invoice>("invoices")

    //let lol = coll2.Query().ToArray();

    let olds: Dto.Invoice seq =
        coll.Query().ToArray()
        |> Seq.map (fun old ->
            let dueDate =
                match
                    (old.AccountingPeriod.Month > 5 && old.AccountingPeriod.Year = 2022)
                    || old.AccountingPeriod.Year >= 2023
                with
                | true -> DateTime(old.AccountingPeriod.Year, old.AccountingPeriod.Month, 15)
                | false -> getLastDayOfMonth old.AccountingPeriod

            { old with DueDate = dueDate })

    let inserted = coll.Update olds

    //a.Add(1)
    Console.ReadLine() |> ignore
    0