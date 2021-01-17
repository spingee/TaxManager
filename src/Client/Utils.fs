module Utils
open System
open Fable.Core
open FsToolkit.ErrorHandling

type Validated<'t> = { Raw: string; Parsed: Validation<'t,string> }

module Validated =
    let createEmpty (): Validated<_> = { Raw = ""; Parsed = Error [] }
    let success raw value: Validated<_> = { Raw = raw; Parsed = Ok value }
    let failure raw errors: Validated<_> = { Raw = raw; Parsed = Error errors }
type Deferred<'t> =
    | HasNotStartedYet
    | InProgress of Option<'t> //ability to add previous value so when reissuing request it wont cause flicking in ui
    | Resolved of 't

type AsyncOperationStatus<'t> =
    | Started
    | Finished of 't

let exceptionToResult (ex:#Exception) =
    Error ex.Message

[<Emit("new Intl.NumberFormat({ style: 'decimal', maximumFractionDigits: $0, minimumFractionDigits: $0 }).format($1)")>]
let formatDecimal (fractionDigits: int) (value: decimal) = jsNative


[<RequireQualifiedAccess>]
module Deferred =

    let resolved =
        function
        | HasNotStartedYet _ -> false
        | InProgress _ -> false
        | Resolved _ -> true

    /// Returns whether the `Deferred<'T>` value is in progress or not.
    let inProgress =
        function
        | HasNotStartedYet _ -> false
        | InProgress _ -> true
        | Resolved _ -> false

    /// Transforms the underlying value of the input deferred value when it exists from type to another
    let map (transform: 'T -> 'U) (deferred: Deferred<'T>): Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress value -> InProgress (Option.map transform value)
        | Resolved value -> Resolved(transform value)

    /// Verifies that a `Deferred<'T>` value is resolved and the resolved data satisfies a given requirement.
    let exists (predicate: 'T -> bool) =
        function
        | HasNotStartedYet _-> false
        | InProgress _ -> false
        | Resolved value -> predicate value

    let defaultResolved defaultValue (deferred: Deferred<'T>): 'T =
        match deferred with
        | HasNotStartedYet -> defaultValue
        | InProgress value -> value |> Option.defaultValue defaultValue
        | Resolved value -> value

    let toOption (deferred: Deferred<'T>): Option<'T> =
        match deferred with
        | HasNotStartedYet -> None
        | InProgress value -> value
        | Resolved value -> Some value
