// Learn more about F# at http://fsharp.org

open System
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions

type Genre = Rock | Pop | Metal of int

type Album = {
    Id: int
    Name: string
    DateReleased: DateTime
    Type: int
    Style: Genre
}



[<EntryPoint>]
let main argv =
    let mapper = FSharpBsonMapper()
    use db = new LiteDatabase("simple.db", mapper)
    let albums = db.GetCollection<Album>("albums")
    let metallica =
        { Id = 3;
          Name = "Metallica";
          Type = 2;
          DateReleased = DateTime(1991, 8, 12)
          Style = Metal 1}

    albums.Insert(metallica) |> ignore
    let resultss = albums.findOne <@ fun album -> album.Id = 1 @>

    let aa: Result<string, MyErr> =
        result {
            let! (a: string) = Ok "a string"
            printfn "A: %A" a
            //   let! b = Error Err2
            //   printfn "B: %A" b
            let! c = (Some "c string", Err1)
            //   let! c = (None, Err1)
            printfn "C: %A" c

            let d = if true then a else c

            printfn "D: %A" d

            return d
        }
    Console.ReadLine() |> ignore
    0
