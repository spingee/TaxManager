module SummaryReportGenerator

open System
open System.IO
open FSharp.Data
open System.Xml.Linq
open FsToolkit.ErrorHandling

type AnnualTaxReport = XmlProvider<Schema="./Xsd/dpfdp5_epo2.xsd", ResolutionFolder=const(__SOURCE_DIRECTORY__),Encoding ="UTF-8">

type ExpensesType =
    | Virtual of uint8
    | Real of uint32

type Attachment = PenzijniPripojisteni of byte []

type AnnualTaxReportInput =
    { Year: uint16
      ExpensesType: ExpensesType
      DateOfFill: DateTime
      TotalEarnings: uint32
      PenzijkoAttachment: byte [] option }

let inline ensureAttrAndSet (xElem: XElement) name value =
    xElem.Attributes()
    |> Seq.filter (fun x -> x.Name = (XName.Get name))
    |> Seq.tryExactlyOne
    |> Option.map
        (fun x ->
            x.SetValue value
            x)
    |> function
        | Some _ -> Ok()
        | Option.None -> Error $"Missing attribute '{name}' on element {xElem.Name}."

let inline removeAttr name (xElem: XElement) =
    xElem.Attributes()
    |> Seq.filter (fun x -> x.Name = (XName.Get name))
    |> Seq.tryExactlyOne
    |> Option.iter (fun x -> x.Remove())

    Ok()

let ensureAttrAndSetMultiple (xElem: XElement) names value =
    validation {
        return!
            names
            |> List.map (fun name -> ensureAttrAndSet xElem name value)
            |> List.map Validation.ofResult
            |> List.reduce
                (fun a b ->
                    (Validation.zip a b)
                    |> Validation.map (fun _ -> ()))

    }

let generateAnnualTaxReport input =

    let stream = File.OpenRead("./Xsd/dpfdp5_epo2_sample.xml")

    let xml =
        AnnualTaxReport.Load(stream)

    let yearStart =
        DateTime(input.Year |> int, 1, 1)
            .ToString("dd.MM.yyyy")

    let yearEnd =
        DateTime(input.Year |> int, 12, 31)
            .ToString("dd.MM.yyyy")

    //let verzePisemnosti = input.DateOfFill.ToString("dd.MM")

    let vetaDEnsureAndSet name value =
        ensureAttrAndSet xml.Dpfdp5.VetaD.XElement name value

    let vetaTEnsureAndSet name value =
        ensureAttrAndSet xml.Dpfdp5.VetaT.Value.XElement name value

    let penzijkoData =
        input.PenzijkoAttachment
        |> Option.map Convert.ToBase64String

    let penzijkoAttachment =
        AnnualTaxReport.PredepsanaPriloha(
            cislo = 2.m,
            nazev =
                Some
                    "Potvrzení o zaplacených příspěvcích na penzijní připojištění, penzijní pojištění, nebo doplňkové penzijní spoření",
            jmSouboru = Some "penzijko.jpg",
            kodovani = Some "base64",
            kod = "PP_POTPENZ",
            value = ""
        )

    penzijkoData |> Option.map (XCData >> penzijkoAttachment.XElement.Add) |> ignore
    do xml.Dpfdp5.Prilohy.Value.XElement.Remove()

    do
        AnnualTaxReport
            .Prilohy(
                [||],
                [| penzijkoAttachment |]
            )
            .XElement
        |> xml.Dpfdp5.XElement.Add




    let expenses =
        input.ExpensesType
        |> function
            | Virtual v -> (input.TotalEarnings / 100u) * (v |> uint32)
            | Real v -> v

    validation {
        //let! _ = ensureAttrAndSet xml.Dpfdp5.XElement "verzePis" verzePisemnosti
        let! _ = vetaDEnsureAndSet "rok" input.Year
        and! _ = vetaDEnsureAndSet "zdobd_od" yearStart
        and! _ = vetaDEnsureAndSet "zdobd_do" yearEnd

        and! _ =
            (input.ExpensesType, "pr_sazba")
            |> function
                | Virtual v, name -> vetaTEnsureAndSet name v
                | _, name -> removeAttr name xml.Dpfdp5.VetaT.Value.XElement

        and! _ =
            ensureAttrAndSetMultiple
                xml.Dpfdp5.VetaT.Value.XElement
                [ "kc_prij7"
                  "pr_prij7"
                  "celk_pr_vyd7" ]
                input.TotalEarnings

        and! _ =
            ensureAttrAndSetMultiple
                xml.Dpfdp5.VetaT.Value.XElement
                [ "celk_pr_prij7"
                  "pr_vyd7"
                  "kc_vyd7" ]
                expenses



        return ()
    }
    |> Result.map (fun _ ->
                    let stream = new MemoryStream()
                    xml.XElement.Document.Save(stream)
                    stream.Position <- 0L
                    stream)


