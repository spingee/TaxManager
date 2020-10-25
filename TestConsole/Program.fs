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


    Console.ReadLine() |> ignore
    0
