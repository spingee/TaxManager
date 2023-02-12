module InvoiceExcel

open Shared.Invoice
open System
open System.Globalization
open GemBox.Spreadsheet
open System.IO
open System.Text.RegularExpressions




let private parseAccPeriodFromInvNumber invoiceNumber =
    let pattern = Regex(@"(\d{4})(\d{2}).*", RegexOptions.Compiled)

    let result = pattern.Match(invoiceNumber)
    DateTime(int result.Groups.[1].Value, int result.Groups.[2].Value, 1)

let private parseRate rate =
    let pattern = Regex(@"Sazba MD: (\d+)Kč", RegexOptions.Compiled)

    let result = pattern.Match(rate)
    Convert.ToUInt32 result.Groups.[1].Value

let private parseManDays md =
    let pattern = Regex(@"Počet MD: (\d+)", RegexOptions.Compiled)

    let result = pattern.Match(md)
    Convert.ToByte result.Groups.[1].Value

let private parseVat vat =
    let pattern = Regex(@"DPH (\d+)%", RegexOptions.Compiled)

    let result = pattern.Match(vat)
    Convert.ToByte result.Groups.[1].Value

let private parseOrderNumber md =
    let pattern =
        Regex(@"(Číslo obj.:|Na základě objednávky) (.*)", RegexOptions.Compiled)

    let result = pattern.Match(md)

    match result.Success with
    | true -> Some(result.Groups.[2].Value.ToString())
    | _ -> None

let private parseTotal total =
    let pattern = Regex(@"[D]*(\d+.*),", RegexOptions.Compiled)

    let result = pattern.Match(total)

    Convert.ToUInt32(
        result.Groups.[1]
            .Value.ToString()
            .Replace(".", "")
    )

let private formatCurrency (m:decimal) =
    m.ToString("C2")


type ExcelVariant =
    | New
    | Adcon
    | Old
    | LogicPoint20091201
    | LogicPoint20110602
    | LogicPoint20111001

let determineExcelVariant (ws: ExcelWorksheet) =
    match String.IsNullOrWhiteSpace(ws.Cells.["A22"].Value :?> String) with
    | false when
        ws.Cells.["D2"].Value.ToString() |> parseAccPeriodFromInvNumber |> (=)
        <| DateTime(2009, 12, 1)
        ->
        LogicPoint20091201
    | true when ws.Cells.["D2"].Value.ToString() |> (=) <| "20110602" -> LogicPoint20110602
    | true when ws.Cells.["D2"].Value.ToString() |> (=) <| "20111001" -> LogicPoint20111001
    | true -> New
    | _ ->
        match ws.Cells.["C25"].Value with
        | obj when obj.Equals("Fakturovaná částka celkem") -> Old
        | _ -> Adcon

let generateWorkBook (invoice:Invoice) =
    let path = Path.GetFullPath("InvoiceTemplate.xlsx")
    let workbook = ExcelFile.Load(path)


    let ws = workbook.Worksheets.[0]

    let total = getTotal invoice |> formatCurrency
    let vat =  getVatAmount invoice |> formatCurrency
    let totalVat = getTotalWithVat invoice |> formatCurrency


    let shortDatePattern =
        CultureInfo(
            "cs-CZ"
        )
            .DateTimeFormat
            .ShortDatePattern

    ws.Cells.["D2"].Value <- invoice.InvoiceNumber
    ws.Cells.["D5"].Value <- invoice.InvoiceNumber
    ws.Cells.["C15"].Value <- invoice.DateOfTaxableSupply.ToString(shortDatePattern)
    ws.Cells.["C16"].Value <-  invoice.DueDate.ToShortDateString()
    ws.Cells.["C18"].Value <- invoice.DateOfTaxableSupply.ToString(shortDatePattern)

    ws.Cells.["A20"].Value <- sprintf
                                  "Programové práce za měsíc %i/%i"
                                  invoice.AccountingPeriod.Month
                                  invoice.AccountingPeriod.Year

    //temporary pick just first ManDay item
    invoice.Items
    |> Seq.choose (extractInvoiceItem >> function | ManDay(rate, manDays) -> Some (rate, manDays) | _ -> None)
    |> Seq.tryHead
    |> Option.iter (fun (rate, manDays) ->
             ws.Cells.["A23"].Value <- sprintf "Počet MD: %i" manDays
             ws.Cells.["A24"].Value <- sprintf "Sazba MD: %iKč" rate
        )



    match invoice.Vat with
    | Some v ->
        ws.Cells.["D25"].Value <- total
        ws.Cells.["D26"].Value <- vat
        ws.Cells.["C26"].Value <- sprintf "DPH %i%%" <| v
        ws.Cells.["A44"].Value <- "Poznámka: Dodavatel JE plátcem DPH."
    | None ->
        ws.Cells.["D25"].Value <- null
        ws.Cells.["D26"].Value <- null
        ws.Cells.["C26"].Value <- null
        ws.Cells.["B25"].Value <- null
        ws.Cells.["A44"].Value <- "Poznámka: Dodavatel NENÍ plátcem DPH."


    ws.Cells.["D27"].Value <- totalVat
    ws.Cells.["C11"].Value <- invoice.Customer.Address
    ws.Cells.["C10"].Value <- invoice.Customer.Name
    ws.Cells.["D7"].Value <- invoice.Customer.IdNumber
    ws.Cells.["D8"].Value <- getVatIdStr invoice.Customer.VatId

    ws.Cells.["B23"].Value <- match invoice.OrderNumber with
                              | Some s -> sprintf "Číslo obj.: %s" s
                              | None -> null



    match invoice.Customer.Note with
    | Some v -> ws.Cells.["C13"].Value <- v
    | None -> ws.Cells.["C13"].Value <- null

    ws.PrintOptions.FitWorksheetWidthToPages <- 1
    ws.PrintOptions.FitWorksheetHeightToPages <- 1
    workbook

let createExcelAndPdfInvoice (destFileWithoutExtension: string) invoice =
    let workbook = generateWorkBook invoice
    workbook.Save(sprintf "%s.xlsx" destFileWithoutExtension)
    //workbook.Save(sprintf "%s.pdf" destFileWithoutExtension)

let generateExcelData invoice =
    let workbook = generateWorkBook invoice
    let stream = new MemoryStream()
    workbook.Save(stream, SaveOptions.XlsxDefault)
    stream