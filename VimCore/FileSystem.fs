#light

namespace Vim
open System.IO
open System.ComponentModel.Composition

[<Export(typeof<IFileSystem>)>]
type internal FileSystem() =

    /// The environment variables considered when loading a .vimrc
    let _environmentVariables = ["HOME";"VIM";"USERPROFILE"]

    let _fileNames = [".vsvimrc"; ".vimrc"; "_vimrc" ]

    /// Read all of the lines from the file at the given path.  If this fails None
    /// will be returned
    member x.ReadAllLines path =
        try
            if System.String.IsNullOrEmpty path then None
            else
                System.IO.File.ReadAllLines(path) |> Some
        with
            _ -> None

    member x.GetVimRcDirectories() = 
        let getEnvVarValue var = 
            match System.Environment.GetEnvironmentVariable(var) with
            | null -> None
            | value -> Some(value)

        _environmentVariables
        |> Seq.map getEnvVarValue
        |> SeqUtil.filterToSome

    member x.GetVimRcFilePaths() =
        x.GetVimRcDirectories()
        |> Seq.map (fun path -> _fileNames |> Seq.map (fun name -> Path.Combine(path,name)))
        |> Seq.concat

    member x.LoadVimRc () = 
        let readLines path = 
            match x.ReadAllLines path with
            | None -> None
            | Some(lines) -> Some(path,lines)
        x.GetVimRcFilePaths()  |> Seq.tryPick readLines

    interface IFileSystem with
        member x.EnvironmentVariables = _environmentVariables 
        member x.VimRcFileNames = _fileNames
        member x.GetVimRcDirectories () = x.GetVimRcDirectories()
        member x.GetVimRcFilePaths() = x.GetVimRcFilePaths()
        member x.LoadVimRc () = x.LoadVimRc()
        member x.ReadAllLines path = x.ReadAllLines path

