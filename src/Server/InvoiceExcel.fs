module InvoiceExcel
open ClosedXML.Excel
open Shared
open System
open System.Globalization
open GemBox.Spreadsheet
open System.IO


let private getInvoiceNumber invoice =
    sprintf "%i%i01" invoice.AccountingPeriod.Year invoice.AccountingPeriod.Month

let createExcelAndPdfInvoice (destFileWithoutExtension:string) invoice =
    let path = Path.GetFullPath("InvoiceTemplate.xlsx")
    let workbook = ExcelFile.Load(path)


    let ws = workbook.Worksheets.[1]
    let invoiceNumber = getInvoiceNumber invoice
    let date = System.DateTime(int invoice.AccountingPeriod.Year,int invoice.AccountingPeriod.Month,1,CultureInfo("cs-CZ").Calendar)
    let dateOfTaxableSupply = date.AddMonths(1).AddDays(-1.0)
    let dueDate = date.AddMonths(1).AddDays(3.0)
    let sumWithoutTax = int invoice.Rate * int invoice.ManDays
    let tax = Math.Round(((float sumWithoutTax) * 0.21),0)
    let total = float sumWithoutTax + tax
    ws.Cells.["D2"].Value <- invoiceNumber
    ws.Cells.["D5"].Value <- invoiceNumber
    ws.Cells.["C15"].Value <- dateOfTaxableSupply
    ws.Cells.["C16"].Value <- dueDate
    ws.Cells.["C18"].Value <- dateOfTaxableSupply
    ws.Cells.["A20"].Value <- sprintf "Programové práce za měsíc %i/%i" invoice.AccountingPeriod.Month invoice.AccountingPeriod.Year
    ws.Cells.["A23"].Value <- sprintf "Počet MD: %i" invoice.ManDays
    ws.Cells.["A24"].Value <- sprintf "Sazba MD: %iKč" invoice.Rate
    ws.Cells.["D25"].Value <- sprintf "%i,-Kč" sumWithoutTax
    ws.Cells.["D26"].Value <- sprintf "%.0f,-Kč" tax
    ws.Cells.["D27"].Value <- sprintf "%.0f,-Kč" total
    workbook.Save(sprintf "%s.xlsx" destFileWithoutExtension)
    workbook.Save(sprintf "%s.pdf" destFileWithoutExtension)

