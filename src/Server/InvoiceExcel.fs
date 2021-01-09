module InvoiceExcel

open Shared.Invoice
open System
open System.Globalization
open GemBox.Spreadsheet
open System.IO
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling


let private getInvoiceNumber invoice indexNumber =
    sprintf "%i%02i%02i" invoice.AccountingPeriod.Year invoice.AccountingPeriod.Month indexNumber

let private parseAccPeriodFromInvNumber invoiceNumber =
    let pattern =
        Regex(@"(\d{4})(\d{2}).*", RegexOptions.Compiled)

    let result = pattern.Match(invoiceNumber)
    DateTime(int result.Groups.[1].Value, int result.Groups.[2].Value, 1)

let private parseRate rate =
    let pattern =
        Regex(@"Sazba MD: (\d+)Kč", RegexOptions.Compiled)

    let result = pattern.Match(rate)
    Convert.ToUInt32 result.Groups.[1].Value

let private parseManDays md =
    let pattern =
        Regex(@"Počet MD: (\d+)", RegexOptions.Compiled)

    let result = pattern.Match(md)
    Convert.ToByte result.Groups.[1].Value

let private parseVat vat =
    let pattern =
        Regex(@"DPH (\d+)%", RegexOptions.Compiled)

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
    let pattern =
        Regex(@"[D]*(\d+.*),", RegexOptions.Compiled)

    let result = pattern.Match(total)
    Convert.ToUInt32 (result.Groups.[1].Value.ToString().Replace(".",""))


type ExcelVariant =
    | New
    | Adcon
    | Old
    | LogicPoint20091201
    | LogicPoint20110602
    | LogicPoint20111001

let determineExcelVariant (ws: ExcelWorksheet) =
    match String.IsNullOrWhiteSpace(ws.Cells.["A22"].Value :?> String) with
    | false when ws.Cells.["D2"].Value.ToString() |> parseAccPeriodFromInvNumber |> (=) <| DateTime(2009,12,1)
        -> LogicPoint20091201
    | true when ws.Cells.["D2"].Value.ToString() |>  (=) <| "20110602"
        -> LogicPoint20110602
    | true when ws.Cells.["D2"].Value.ToString() |>  (=) <| "20111001"
        -> LogicPoint20111001
    | true -> New
    | _ ->
        match ws.Cells.["C25"].Value with
        | obj when obj.Equals("Fakturovaná částka celkem") -> Old
        | _-> Adcon


let createExcelAndPdfInvoice (destFileWithoutExtension: string) invoice indexNumber =
    let path = Path.GetFullPath("InvoiceTemplate.xlsx")
    let workbook = ExcelFile.Load(path)


    let ws = workbook.Worksheets.[0]
    let invoiceNumber = getInvoiceNumber invoice indexNumber

    let date =
        System.DateTime(
            int invoice.AccountingPeriod.Year,
            int invoice.AccountingPeriod.Month,
            1,
            CultureInfo("cs-CZ").Calendar
        )

    let dateOfTaxableSupply = date.AddMonths(1).AddDays(-1.0)
    let dueDate = date.AddMonths(1).AddDays(3.0)
    let sumWithoutTax = int invoice.Rate * int invoice.ManDays

    let tax =
        Math.Round(((float sumWithoutTax) * 0.21), 0)

    let total = float sumWithoutTax + tax
    ws.Cells.["D2"].Value <- invoiceNumber
    ws.Cells.["D5"].Value <- invoiceNumber
    ws.Cells.["C15"].Value <- dateOfTaxableSupply
    ws.Cells.["C16"].Value <- dueDate
    ws.Cells.["C18"].Value <- dateOfTaxableSupply
    ws.Cells.["A20"].Value <- sprintf
                                  "Programové práce za měsíc %i/%i"
                                  invoice.AccountingPeriod.Month
                                  invoice.AccountingPeriod.Year
    ws.Cells.["A23"].Value <- sprintf "Počet MD: %i" invoice.ManDays
    ws.Cells.["A24"].Value <- sprintf "Sazba MD: %iKč" invoice.Rate
    ws.Cells.["D25"].Value <- sprintf "%i,-Kč" sumWithoutTax
    ws.Cells.["D26"].Value <- sprintf "%.0f,-Kč" tax
    ws.Cells.["D27"].Value <- sprintf "%.0f,-Kč" total
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
    workbook.Save(sprintf "%s.xlsx" destFileWithoutExtension)
    workbook.Save(sprintf "%s.pdf" destFileWithoutExtension)

let readFromExcel file =
    let workbook = ExcelFile.Load(path = file)
    let ws = workbook.Worksheets.[0]
    let excelVariant = determineExcelVariant ws

    let accPeriod =
        parseAccPeriodFromInvNumber (ws.Cells.["D2"].Value.ToString())

    let manDays =
        match excelVariant with
        | New -> parseManDays (ws.Cells.["A23"].Value.ToString())
        | Adcon
        | Old -> parseManDays (ws.Cells.["A22"].Value.ToString())
        | LogicPoint20091201 ->  parseManDays (ws.Cells.["A22"].Value.ToString()) + parseManDays (ws.Cells.["A26"].Value.ToString())
        | LogicPoint20110602  | LogicPoint20111001-> 1uy


    let rate =
        match excelVariant with
        | New -> parseRate (ws.Cells.["A24"].Value.ToString())
        | Adcon-> parseRate (ws.Cells.["A23"].Value.ToString())
        | Old -> uint32 (parseTotal(ws.Cells.["D25"].Value.ToString()) / uint32 manDays)
        | LogicPoint20091201 ->uint32 (parseTotal(ws.Cells.["D28"].Value.ToString()) / uint32 manDays)
        | LogicPoint20110602 -> uint32 50000
        | LogicPoint20111001-> uint32 140000

    let orderNumber =
        match ws.Cells.["B23"].Value with
        | null -> match ws.Cells.["A21"].Value with
                  | null ->  None
                  | obj -> parseOrderNumber (obj.ToString())
        | obj -> parseOrderNumber (obj.ToString())

    let vat =
        match excelVariant with
        | New -> parseVat (ws.Cells.["B26"].Value.ToString()) |> Some
        | Adcon -> parseVat (ws.Cells.["B25"].Value.ToString()) |> Some
        | Old | LogicPoint20091201 | LogicPoint20110602 -> None
        | LogicPoint20111001-> Some 20uy
    let custNote =
        match ws.Cells.["C13"].Value with
        | null -> None
        | obj -> Some(obj.ToString())

    let custId = uint (ws.Cells.["D7"].Value.ToString())

    let vatId =
        createVatId (ws.Cells.["D8"].Value.ToString())
        |> Result.valueOr failwith

    let address =
        match excelVariant with
        | New ->
            let main = ws.Cells.["C11"].Value.ToString()
            let postal = ws.Cells.["C12"].Value :?> String
            if ( not (String.IsNullOrEmpty(postal)) && main <> postal) then
                $"{main} {postal}"
            else
                main
        | Adcon | Old | LogicPoint20091201 | LogicPoint20110602  | LogicPoint20111001
             -> sprintf "%s %s" (ws.Cells.["C11"].Value.ToString()) (ws.Cells.["C12"].Value.ToString())

    let cust =
        { Name = ws.Cells.["C10"].Value.ToString()
          IdNumber = custId
          Address = address
          VatId = vatId
          Note = custNote }

    { Id = Guid.NewGuid()
      AccountingPeriod = accPeriod
      ManDays = manDays
      Rate = rate
      OrderNumber = orderNumber
      Vat = vat
      Customer = cust }
