#light

namespace Vim
open System.IO
open System.ComponentModel.Composition

[<Export(typeof<IFileSystem>)>]
type internal FileSystem() =

    /// The environment variables considered when loading a .vimrc
    let _environmentVariables = ["%HOME%"; "%HOMEDRIVE%%HOMEPATH%"; "%VIM%"; "%USERPROFILE%"]

    let _fileNames = [".vsvimrc"; "_vsvimrc"; ".vimrc"; "_vimrc" ]

    /// Read all of the lines from the file at the given path.  If this fails None
    /// will be returned
    member x.ReadAllLines path =

        // Yes I realize I wrote an entire blog post on why File.Exists is an evil
        // API to use and I'm using it in this code.  In this particular case though
        // the use is OK because first and foremost we deal with the exceptions 
        // that can be thrown.  Secondly this is only used because it makes debugging
        // significantly easier as the exception thrown breaks. 
        //
        // Additionally I will likely be changing it to avoid the exception break
        // at a future time
        // 
        // http://blogs.msdn.com/b/jaredpar/archive/2009/12/10/the-file-system-is-unpredictable.aspx 
        if System.String.IsNullOrEmpty path then None
        elif System.IO.File.Exists path then 
            try
                System.IO.File.ReadAllLines(path) |> Some
            with
                _ -> None
        else
            None

    member x.GetVimRcDirectories() = 
        let getEnvVarValue var = 
            match System.Environment.ExpandEnvironmentVariables(var) with
            | var1 when System.String.Equals(var1,var,System.StringComparison.InvariantCultureIgnoreCase) -> None
            | null -> None
            | value -> Some(value)

        _environmentVariables
        |> Seq.map getEnvVarValue
        |> SeqUtil.filterToSome

    member x.GetVimRcFilePaths() =

        let standard = 
            x.GetVimRcDirectories()
            |> Seq.map (fun path -> _fileNames |> Seq.map (fun name -> Path.Combine(path,name)))
            |> Seq.concat

        // If the MYVIMRC environment variable is set then prefer that path over the standard
        // paths
        match SystemUtil.TryGetEnvironmentVariable "MYVIMRC" with
        | None -> standard
        | Some filePath -> Seq.append [ filePath ] standard

    member x.LoadVimRcContents () = 
        let readLines path = 
            match x.ReadAllLines path with
            | None -> None
            | Some lines -> 
                let contents = {
                    FilePath = path
                    Lines = lines
                } 
                Some contents
        x.GetVimRcFilePaths()  |> Seq.tryPick readLines

    interface IFileSystem with
        member x.EnvironmentVariables = _environmentVariables 
        member x.VimRcFileNames = _fileNames
        member x.GetVimRcDirectories () = x.GetVimRcDirectories()
        member x.GetVimRcFilePaths() = x.GetVimRcFilePaths()
        member x.LoadVimRcContents () = x.LoadVimRcContents()
        member x.ReadAllLines path = x.ReadAllLines path

