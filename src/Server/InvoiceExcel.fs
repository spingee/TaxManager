module InvoiceExcel
open ClosedXML.Excel
open Shared
open System
open System.Globalization

let private getInvoiceNumber invoice =
    sprintf "%i%i01" invoice.AccountingPeriod.Year invoice.AccountingPeriod.Month

let createExcelInvoice (destFile:string) invoice =
    use wb = new XLWorkbook("InvoiceTemplate.xlsx")
    let ws = wb.Worksheet(1);
    let invoiceNumber = getInvoiceNumber invoice
    let date = (new DateTime(int invoice.AccountingPeriod.Year,int invoice.AccountingPeriod.Month,1,(new CultureInfo("cs-CZ")).Calendar))
    let dateOfTaxableSupply = date.AddMonths(1).AddDays(-1.0)
    let dueDate = date.AddMonths(1).AddDays(3.0)
    let sumWithoutTax = int invoice.Rate * int invoice.ManDays
    let tax = Math.Round(((float sumWithoutTax) * 0.21),0)
    let total = float sumWithoutTax + tax
    ws.Cell(2,"D").Value <- invoiceNumber
    ws.Cell(5,"D").Value <- invoiceNumber
    ws.Cell(15,"C").Value <- dateOfTaxableSupply
    ws.Cell(16,"C").Value <- dueDate
    ws.Cell(18,"C").Value <- dateOfTaxableSupply
    ws.Cell(20,"A").Value <- sprintf "Programové práce za měsíc %i/%i" invoice.AccountingPeriod.Month invoice.AccountingPeriod.Year
    ws.Cell(23,"A").Value <- sprintf "Počet MD: %i" invoice.ManDays
    ws.Cell(24,"A").Value <- sprintf "Sazba MD: %iKč" invoice.Rate
    ws.Cell(25,"D").Value <- sprintf "%i,-Kč" sumWithoutTax
    ws.Cell(26,"D").Value <- sprintf "%.0f,-Kč" tax
    ws.Cell(27,"D").Value <- sprintf "%.0f,-Kč" total
    wb.SaveAs(destFile)
    wb.SaveAs(destFile,new Save)
