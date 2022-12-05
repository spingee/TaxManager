open System
open System.IO
open Report
open FsToolkit.ErrorHandling
open Shared
open LiteDB
open System.Collections

let fff<'t> = System.Collections.Generic.List<'t>()

let connectionString =
    @"FileName=/Users/janstrnad/OneDrive/Dokumenty/Faktury/TaxManagerDb/taxmanager.db;Connection=shared"


type Paint =
    private
    | Paint' of volume: float * pigment: int

    member me.Volume =
        let (Paint' (volume = value)) = me in value

    member me.Pigment =
        let (Paint' (pigment = value)) = me in value
[<CLIMutable>]
type Customer =
    { IdNumber: uint
      VatId: string
      Name: string
      Address: string
      Note: string }

[<CLIMutable>]
type Invoice =
    { Id: Guid
      InvoiceNumber: string
      ManDays: uint8
      Rate: uint32
      AccountingPeriod: DateTime
      DateOfTaxableSupply: DateTime
      OrderNumber: string
      Vat: Nullable<int>
      Customer: Customer
      Inserted: DateTime }


[<EntryPoint>]
let main argv =

    use db = new LiteDatabase(connectionString)
    let coll = db.GetCollection<Dto.Invoice>("invoices")
    let col = coll.FindAll() |> Seq.toArray
    let inv =  coll.FindById("00020000-ac11-0242-229a-08dad46a49f7")
    inv.
    //a.Add(1)
    Console.ReadLine() |> ignore
    0