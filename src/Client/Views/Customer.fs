[<RequireQualifiedAccess>]
module Customer

open System
open Common
open Shared.Invoice
open Utils
open FsToolkit.ErrorHandling
open Fulma
open Elmish
open Fable.React

type Model =
    { IdNumber: Validated<uint>
      VatId: Validated<VatId>
      Name: Validated<string>
      Address: Validated<string>
      Note: Validated<string option>
      IsActive: bool }

let toCustomerInput (customer: Customer) =
    { IdNumber = Validated.success (customer.IdNumber.ToString()) customer.IdNumber
      VatId = Validated.success (getVatIdStr customer.VatId) customer.VatId
      Name = Validated.success customer.Name customer.Name
      Address = Validated.success customer.Address customer.Address
      Note =
          Validated.success
              (Option.map id customer.Note
               |> Option.defaultValue "")
              customer.Note
      IsActive = true }

let fromCustomerInput (custInput: Model) =
    //{ Customer.IdNumber = 4554u; VatId = VatId "CZ4564";Name = "name"; Address = "address"; Note = Some "note" }
    validation {
        let! idNumber = custInput.IdNumber.Parsed
        and! vatId = custInput.VatId.Parsed
        and! name = custInput.Name.Parsed
        and! address = custInput.Address.Parsed
        and! note = custInput.Note.Parsed

        return
            { Customer.IdNumber = idNumber
              VatId = vatId
              Name = name
              Address = address
              Note = note }
    }

let defaultInput =
    { IdNumber = Validated.createEmpty ()
      VatId = Validated.createEmpty ()
      Name = Validated.createEmpty ()
      Address = Validated.createEmpty ()
      Note = Validated.createEmpty ()
      IsActive = true }

let init () =
    { defaultInput with IsActive = false }, Cmd.none

let isValid input =
    match fromCustomerInput input with
    | Ok _ -> true
    | Error _ -> false

type Msg =
    | SetCustomerIdNumber of string
    | SetCustomerVatId of string
    | SetCustomerName of string
    | SetCustomerAddress of string
    | SetCustomerNote of string
    | Start of Customer option
    | Cancel
    | TryFinish
    | Finish of Customer

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | SetCustomerIdNumber v ->
        let idNumber =
            match UInt32.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v [ "Must be whole positive number less than ~4million." ]

        { model with IdNumber = idNumber }, Cmd.none
    | SetCustomerVatId v ->
        let model =
            { model with

                  VatId =
                      createVatId v
                      |> function
                      | Ok x -> Validated.success v x
                      | Error _ -> Validated.failure v [ "Must be positive number prepended with ISO2 country code." ] }

        model, Cmd.none
    | SetCustomerName v ->
        let name =
            match String.IsNullOrEmpty(v) with
            | false -> Validated.success v v
            | true -> Validated.failure v [ "Required." ]

        { model with Name = name }, Cmd.none
    | SetCustomerAddress v ->
        let address =
            match String.IsNullOrEmpty(v) with
            | false -> Validated.success v v
            | true -> Validated.failure v [ "Required." ]

        { model with Address = address }, Cmd.none
    | SetCustomerNote v ->
        let note =
            match String.IsNullOrEmpty(v) with
            | true -> Validated.success "" None
            | false -> Validated.success v <| Some v

        { model with Note = note }, Cmd.none
    | Start None -> { model with IsActive = true }, Cmd.none
    | Start (Some c) -> toCustomerInput c, Cmd.none
    | Cancel -> { model with IsActive = false }, Cmd.none
    | TryFinish ->
        match fromCustomerInput model with
        | Ok c -> { model with IsActive = false }, Cmd.ofMsg (Finish c)
        | Error e -> model, Cmd.none //todo show error?, but you cannot hit create button when model is invalid anyway
    | Finish _ -> model, Cmd.none

type Props = { Model: Model; Dispatch: Msg -> unit }

let elmishView name render =
    FunctionComponent.Of(render, name, equalsButFunctions)

let view =
    elmishView "CustomerForm"
    <| fun { Model = model; Dispatch = dispatch } ->
        Modal.modal [ Modal.IsActive model.IsActive ] [
            Modal.background [] []
            Modal.Card.card [] [
                Modal.Card.head [] [
                    Modal.Card.title [] [
                        str "Create customer"
                    ]
                    Delete.delete [ Delete.OnClick(fun _ -> dispatch Cancel) ] []
                ]

                Modal.Card.body [] [

                    createTextField
                        "Name"
                        model.Name
                        [ Input.Placeholder "Name of customer"
                          Input.OnChange(fun x -> SetCustomerName x.Value |> dispatch) ]
                    createTextField
                        "Identification number"
                        model.IdNumber
                        [ Input.Placeholder "Identification number"
                          Input.OnChange(fun x -> SetCustomerIdNumber x.Value |> dispatch) ]
                    createTextField
                        "VAT Id"
                        model.VatId
                        [ Input.Placeholder "VAT Id"
                          Input.OnChange(fun x -> SetCustomerVatId x.Value |> dispatch) ]
                    createTextField
                        "Address"
                        model.Address
                        [ Input.Placeholder "Address"
                          Input.OnChange(fun x -> SetCustomerAddress x.Value |> dispatch) ]
                    createTextAreaField
                        "Note"
                        model.Note
                        [ Textarea.Placeholder "Note"
                          Textarea.OnChange(fun x -> SetCustomerNote x.Value |> dispatch) ]
                ]
                Modal.Card.foot [] [
                    Button.button [ Button.Color IsSuccess
                                    Button.Disabled <| not (isValid model)
                                    Button.OnClick(fun _ -> dispatch TryFinish) ] [
                        str "Create"
                    ]
                    Button.button [ Button.OnClick(fun _ -> dispatch <| Cancel) ] [
                        str "Cancel"
                    ]
                ]
            ]
        ]

