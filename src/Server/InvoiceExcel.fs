module InvoiceExcel

open Shared.Invoice
open System
open System.Globalization
open GemBox.Spreadsheet
open System.IO



let private getInvoiceNumber invoice indexNumber =
    sprintf "%i%i%02i" invoice.AccountingPeriod.Year invoice.AccountingPeriod.Month indexNumber

let createExcelAndPdfInvoice (destFileWithoutExtension: string) invoice indexNumber =
    let path = Path.GetFullPath("InvoiceTemplate.xlsx")
    let workbook = ExcelFile.Load(path)


    let ws = workbook.Worksheets.[0]
    let invoiceNumber = getInvoiceNumber invoice indexNumber

    let date =
        System.DateTime
            (int invoice.AccountingPeriod.Year, int invoice.AccountingPeriod.Month, 1, CultureInfo("cs-CZ").Calendar)

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
    ws.Cells.["B23"].Value <-
        match invoice.OrderNumber with
        | Some s -> sprintf "Číslo obj.: %s" s
        | None -> null

    match invoice.Customer.Note with
    | Some v -> ws.Cells.["C13"].Value <- v
    | None ->ws.Cells.["C13"].Value <- null

    ws.PrintOptions.FitWorksheetWidthToPages <- 1;
    ws.PrintOptions.FitWorksheetHeightToPages <- 1;
    workbook.Save(sprintf "%s.xlsx" destFileWithoutExtension)
    workbook.Save(sprintf "%s.pdf" destFileWithoutExtension)
