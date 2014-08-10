#light

namespace Vim
open System.IO
open System.Collections.Generic
open System.ComponentModel.Composition
open System.Text

[<Export(typeof<IFileSystem>)>]
type internal FileSystem() =

    /// The environment variables considered when loading a .vimrc
    static let VimRcDirectoryCandidates = ["~"; "$VIM"; "$USERPROFILE"]

    static let FileNames = 
        [
            (".vsvimrc", VimRcKind.VsVimRc)
            ("_vsvimrc", VimRcKind.VsVimRc)
            (".vimrc", VimRcKind.VimRc)
            ("_vimrc", VimRcKind.VimRc)
        ]

    /// Read all of the lines from the given StreamReader.  This will return whether or not 
    /// an exception occurred during processing and if not the lines that were read
    /// Read all of the lines from the file at the given path.  If this fails None
    /// will be returned
    member x.ReadAllLinesCore (streamReader : StreamReader) =
        let list = List<string>()
        let mutable line = streamReader.ReadLine()
        while line <> null do
            list.Add line
            line <- streamReader.ReadLine()

        list

    /// This will attempt to read the path using first the encoding dictated by the BOM and 
    /// if there is no BOM it will try UTF8.  If either encoding encounters errors trying to
    /// process the file then this function will also fail
    member x.ReadAllLinesBomAndUtf8 (path : string) = 
        let encoding = UTF8Encoding(false, true)
        use streamReader = new StreamReader(path, encoding, true)
        x.ReadAllLinesCore streamReader

    /// Read the lines with the Latin1 encoding.  
    member x.ReadAllLinesLatin1 (path : string) = 
        let encoding = Encoding.GetEncoding("Latin1")
        use streamReader = new StreamReader(path, encoding, false)
        x.ReadAllLinesCore streamReader

    /// Forced utf8 encoding
    member x.ReadAllLinesUtf8 (path : string) = 
        let encoding = Encoding.UTF8
        use streamReader = new StreamReader(path, encoding, false)
        x.ReadAllLinesCore streamReader

    /// Now we do the work to support various file encodings.  We prefer the following order
    /// of encodings
    ///
    ///  1. BOM 
    ///  2. UTF8
    ///  3. Latin1
    ///  4. Forced UTF8 and accept decoding errors
    ///
    /// Ideally we would precisely emulate vim here.  However replicating all of their encoding
    /// error detection logic and mixing it with the .Net encoders is quite a bit of work.  This
    /// pattern lets us get the vast majority of cases with a much smaller amount of work
    member x.ReadAllLinesWithEncoding (path: string) =
        let all = 
            [| 
                x.ReadAllLinesBomAndUtf8; 
                x.ReadAllLinesLatin1;
                x.ReadAllLinesUtf8;
            |]

        let mutable lines : List<string> option = None
        let mutable i = 0
        while i < all.Length && Option.isNone lines do
            try
                let current = all.[i]
                lines <- Some (current path)
            with
                | _ -> ()

            i <- i + 1

        lines

    /// Read all of the lines from the file at the given path.  If this fails None
    /// will be returned
    member x.ReadAllLines path =
        match SystemUtil.TryResolvePath path with
        | Some expanded -> x.ReadAllLinesExpanded expanded
        | None -> None

    member x.ReadDirectoryContents path = 
        match SystemUtil.TryResolvePath path with
        | None -> None
        | Some path ->
            try
                // This test just exists to avoid first chance exceptions when debugging.  Stepping through
                // them is distracting
                if not (Directory.Exists path) then
                    None
                else
                    let list = List<string>()
                    list.Add("../")
                    list.Add("./")

                    Directory.GetDirectories(path)
                    |> Seq.map (fun dir -> Path.GetFileName(dir) + "/")
                    |> list.AddRange

                    Directory.GetFiles(path)
                    |> Seq.map Path.GetFileName
                    |> list.AddRange

                    list.ToArray() |> Some
            with
                | _ -> None

    member x.ReadAllLinesExpanded path =

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
        if System.String.IsNullOrEmpty path then 
            None
        elif System.IO.File.Exists path then 

            match x.ReadAllLinesWithEncoding path with
            | None -> None
            | Some list -> list.ToArray() |> Some

        else
            None

    member x.GetVimRcDirectories() = 
        VimRcDirectoryCandidates
        |> Seq.choose SystemUtil.TryResolvePath 
        |> Seq.toArray

    member x.GetVimRcFilePaths() =
        let standard =
            x.GetVimRcDirectories()
            |> Seq.collect (fun path -> FileNames |> Seq.map (fun (name, kind) -> { VimRcKind = kind; FilePath = Path.Combine(path,name) }))

        // If the MYVIMRC environment variable is set then prefer that path over the standard
        // paths
        let all = 
            match SystemUtil.TryGetEnvironmentVariable "MYVIMRC" with
            | None -> standard
            | Some filePath -> Seq.append [ { VimRcKind = VimRcKind.VimRc; FilePath = filePath } ] standard

        Seq.toArray all

    interface IFileSystem with
        member x.GetVimRcDirectories() = x.GetVimRcDirectories()
        member x.GetVimRcFilePaths() = x.GetVimRcFilePaths()
        member x.ReadAllLines path = x.ReadAllLines path
        member x.ReadDirectoryContents directoryPath = x.ReadDirectoryContents directoryPath

