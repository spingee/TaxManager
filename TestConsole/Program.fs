open System
open System.Collections.Generic
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
open FsToolkit.ErrorHandling

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
      Vat: decimal
      VatNumber: string
      InvoiceNumber: string
      FileName: string }



//todo musi byt mimo kod, push na github zrusi ten api key v openapi
let [<Literal>]openAiKey = "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"

let [<Literal>]deploymentId = "text-davinci-003"
//let [<Literal>]deploymentId = "gpt-4"

let getPrompt pdfText = $"""
Extrahuj ve formátu json (názvy propert v uvozovkách): daň respektive 'výše dph' (Vat jako number), daň %% (VatPercent jako number),
základ daně (TaxBase jako number) a datum uskutečnění plnění (DateOfTaxableSupply, případně datum vystavení), čislo dokladu/vyúčtování/faktury (InvoiceNumber jako string),
DIČ dodavatele (VatNumber jako string, nesmí být DIČ odběratele!).
Bez jednotek hodnot, datum v iso-8601 formátu, preferuj číselné hodnoty co jsou nejblíže u sebe:
{pdfText}
"""

let extractPdfInvoice pdfFilePath =
    printfn $"Processing %s{pdfFilePath}"
    let pdfDoc = PdfDocument.Open(pdfFilePath)
    let pdfText =
        pdfDoc.GetPages()
        |> Seq.chunkBySize 2
        |> Seq.take 1//only first two pages
        |> Seq.concat
        |> Seq.map (fun page -> page.GetWords())
        |> Seq.map (fun words -> words |> Seq.map (fun word -> word.Text) |> String.concat " ")
        |> String.concat Environment.NewLine

    let prompt = getPrompt pdfText
    let prompt = prompt.Substring(0, Math.Min(prompt.Length, 4096))
    let openAiClient = OpenAIClient(openAiKey)
    let options = CompletionsOptions()
    options.Prompts.Add prompt
    options.MaxTokens <- 250
    options.Temperature <- 0.5f
    options.FrequencyPenalty <- 0.0f
    options.PresencePenalty <- 0.0f
    options.NucleusSamplingFactor <- 1f

    let resp = openAiClient.GetCompletions(deploymentId, options)
    printfn "Usage: %i" resp.Value.Usage.TotalTokens

    let resultStr = resp.Value.Choices
                 |> Seq.map (fun x -> x.Text)
                 |> Seq.head


    Console.WriteLine(resultStr)

    try
        Result.Ok (JsonSerializer.Deserialize<InvoicePdf>(resultStr))
    with ex ->
        Result.Error $"Cannot deserialize result:{Environment.NewLine}{resultStr}{Environment.NewLine}{ex.Message}{Environment.NewLine}{pdfFilePath}"
    |> Result.map (fun result ->
        let result' = { result with FileName = pdfFilePath }
        Console.WriteLine(result')
        result')
    |> Result.bind (fun result ->
          let computedVatPercent = (100m / result.TaxBase) * result.Vat
          match (Math.Abs(computedVatPercent - result.VatPercent) < 0.2m) with
            | true -> Result.Ok result
            | false -> Result.Error $"Values are not correct {result}")






[<EntryPoint>]
let main argv =
    let cultureInfo = System.Globalization.CultureInfo("cs-CZ")

    do System.Threading.Thread.CurrentThread.CurrentCulture <- cultureInfo
    do System.Threading.Thread.CurrentThread.CurrentUICulture <- cultureInfo



    use db = new LiteDatabase(@"FileName=/Users/janstrnad/OneDrive/Dokumenty/Faktury/TaxManagerDb/taxreturns.db;Connection=shared")
    let coll = db.GetCollection<InvoicePdf>("tax_return_invoices")

    let pdfFilePath = "/Users/janstrnad/Library/CloudStorage/OneDrive-Personal/Dokumenty/Faktury/DPH 2023-2/"
    let extracted =
        Directory.EnumerateDirectories(pdfFilePath, "Vstup", SearchOption.AllDirectories)
        |> Seq.map (fun x -> Directory.EnumerateFiles(x, "*.pdf"))
        |> Seq.concat
        |> Seq.map extractPdfInvoice
        |> Seq.fold (fun (o,e) x  ->
            match x with
            | Ok x ->
                //coll.Insert x |> ignore
                (x::o,e)
            | Error x -> (o,x::e)
            ) ([],[])



    printfn "Total %i, Succeeded %i, Failed %i" ((fst extracted).Length + (snd extracted).Length) (fst extracted).Length (snd extracted).Length

    // fst extracted
    //     |> coll.InsertBulk
    //     |> ignore

    fst extracted
        |> List.filter (fun x -> x.TaxBase < 10000m)
        |> List.fold (fun (t,v) x -> (x.TaxBase + t, x.Vat + v )) (0m,0m)
        |> Printf.printfn "Total under 10000CZK: %A"

    fst extracted
        |> List.filter (fun x -> x.TaxBase >= 10000m)
        |> List.fold (fun (t,v) x -> (x.TaxBase + t, x.Vat + v )) (0m,0m)
        |> Printf.printfn "Total over 10000CZK: %A"

    fst extracted
        |> List.fold (fun (t,v) x -> (x.TaxBase + t, x.Vat + v )) (0m,0m)
        |> Printf.printfn "Grand Total: %A"

    snd extracted
        |> List.iter (fun x ->
            Console.ForegroundColor <- ConsoleColor.Red
            printfn $"Error: %A{x}"
            Console.ResetColor())


    Console.ReadLine() |> ignore
    0