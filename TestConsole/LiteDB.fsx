#r "nuget: LiteDB"
#r "../src/Server/bin/Debug/net6.0/Server.dll"

open LiteDB
open System

let getDateOfTaxableSupply (accPeriod: DateTime) =
    DateTime(int accPeriod.Year, int accPeriod.Month, 1)
        .AddMonths(1)
        .AddDays(-1.0)

let formatInvoiceNumber (invoice: Dto.Invoice) indexNumber =
    sprintf "%i%02i%02i" invoice.AccountingPeriod.Year invoice.AccountingPeriod.Month indexNumber

let getInvoiceNumber (invoices: Dto.Invoice seq) (invoice: Dto.Invoice) =
    invoices
    |> Seq.filter
        (fun i ->
            i.AccountingPeriod.Year = invoice.AccountingPeriod.Year
            && i.AccountingPeriod.Month = invoice.AccountingPeriod.Month)
    |> Seq.sortBy (fun i -> i.Inserted)
    |> Seq.mapi (fun n i -> i, formatInvoiceNumber i (n + 1))
    |> Seq.find (fun (i,_) -> i.Id = invoice.Id )
    |> snd

let connectionString = @$"FileName={__SOURCE_DIRECTORY__}/taxmanager.db;Connection=shared"



let db = new LiteDatabase(connectionString)
let liteCollection = db.GetCollection<Dto.Invoice>("invoices")
let invoices = liteCollection.Query().ForUpdate().ToArray()

invoices
|> Seq.map
    (fun i ->
        { i with
              DateOfTaxableSupply = getDateOfTaxableSupply i.AccountingPeriod
              AccountingPeriod = DateTime(i.AccountingPeriod.Year, i.AccountingPeriod.Month, 1)
              InvoiceNumber = getInvoiceNumber invoices i })
|> liteCollection.Update
