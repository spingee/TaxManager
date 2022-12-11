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
    use db2 = new LiteDatabase(connectionString2)
    let coll = db.GetCollection<Invoice>("invoices")
    let coll2 = db2.GetCollection<Dto.Invoice>("invoices")

    let lol = coll2.Query().ToArray();

    // let olds: Dto.Invoice seq =
    //     coll.Query().ToArray()
    //     |> Seq.map (fun old ->
    //         { Dto.Invoice.Id = old.Id
    //           Dto.Invoice.Created = old.Inserted
    //           Dto.Invoice.Vat = old.Vat
    //           Dto.Invoice.OrderNumber = old.OrderNumber
    //           Dto.Invoice.DateOfTaxableSupply = old.DateOfTaxableSupply
    //           Dto.Invoice.AccountingPeriod = old.AccountingPeriod
    //           Dto.Invoice.InvoiceNumber = old.InvoiceNumber
    //           Dto.Invoice.Customer =
    //             { Address = old.Customer.Address
    //               Name = old.Customer.Name
    //               Note = old.Customer.Note
    //               IdNumber = old.Customer.IdNumber
    //               VatId = old.Customer.VatId }
    //           Dto.Invoice.Items =
    //             [| { InvoiceItemUnionCase = Dto.InvoiceItemUnionCase.ManDay
    //                  Total = Nullable<decimal>()
    //                  ManDays = Nullable<uint8>(old.ManDays)
    //                  Rate = Nullable<uint32>(old.Rate)
    //                  Separate = false
    //                  Label = null } |] })
    //
    // let inserted = coll2.InsertBulk olds

    //a.Add(1)
    Console.ReadLine() |> ignore
    0