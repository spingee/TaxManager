open System
open System.IO
open Azure.AI.OpenAI
open Report
open FsToolkit.ErrorHandling
open Shared
open LiteDB
open System.Collections
open Shared.Invoice
open UglyToad.PdfPig
open System.Text.Json

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


let convertDb =
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
    ()

[<CLIMutable>]
type InvoicePdf =
    { DateOfTaxableSupply: DateTime
      TaxBase: decimal
      VatPercent: decimal
      Vat: decimal }


[<EntryPoint>]
let main argv =
    let cultureInfo = System.Globalization.CultureInfo("cs-CZ")

    do System.Threading.Thread.CurrentThread.CurrentCulture <- cultureInfo
    do System.Threading.Thread.CurrentThread.CurrentUICulture <- cultureInfo


    let openAiKey = "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"
    let deploymentId = "text-davinci-003"
    let openAiClient = OpenAIClient(openAiKey)
    let pdfFilePath = "/Users/janstrnad/Library/CloudStorage/OneDrive-Personal/Dokumenty/Faktury/DPH 2023-1/Vstup/"
    let sum =
        Directory.GetFiles(pdfFilePath, "*.pdf")
        |> Seq.map (fun pdfFile ->
            printfn $"Processing %s{pdfFile}"
            let pdfDoc = PdfDocument.Open(pdfFile)
            let pdfText =
                pdfDoc.GetPages()
                |> Seq.map (fun page -> page.GetWords())
                |> Seq.map (fun words -> words |> Seq.map (fun word -> word.Text) |> String.concat " ")
                |> String.concat Environment.NewLine


            let prompt = $"""
    Extrahuj ve formátu json daň (Vat jako number), daň procentuálě (VatPercent jako number), základ daně (TaxBase jako number) a datum uskutečnění plnění (DateOfTaxableSupply).
    Bez jednotek hodnot, datum v iso formátu:
    {pdfText}
    """

            let options = CompletionsOptions()
            options.Prompts.Add prompt
            options.MaxTokens <- 100
            options.Temperature <- 0.5f
            options.FrequencyPenalty <- 0.0f
            options.PresencePenalty <- 0.0f
            options.NucleusSamplingFactor <- 1f

            let resp = openAiClient.GetCompletions(deploymentId, options)
            let resultStr = resp.Value.Choices
                         |> Seq.map (fun x -> x.Text)
                         |> Seq.head


            Console.WriteLine(resultStr)
            let result = JsonSerializer.Deserialize<InvoicePdf>(resultStr)
            Console.WriteLine(result)
            result
        ) |> Seq.fold (fun (t,v) x -> (x.TaxBase + t, x.Vat + v )) (0m,0m)

    sum |> printfn "Total: %A"

    match sum = (3121.71M, 655.58M) with
    | true -> printfn "OK"
    | false -> printfn "FAIL"

    //a.Add(1)
    Console.ReadLine() |> ignore
    0