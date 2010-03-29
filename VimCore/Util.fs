#light

namespace Vim

[<AbstractClass>]
type internal ToggleHandler() =
    abstract Add : unit -> unit
    abstract Remove : unit -> unit
   
    static member Create<'T> (source:System.IObservable<'T>) (func: 'T -> unit) = ToggleHandler<'T>(source,func)
    static member Empty = 
        { new ToggleHandler() with 
            member x.Add() = ()
            member x.Remove() = () }

and internal ToggleHandler<'T> 
    ( 
        _source : System.IObservable<'T>,
        _func : 'T -> unit) =  
    inherit ToggleHandler()
    let mutable _handler : System.IDisposable option = None
    override x.Add() = 
        match _handler with
        | Some(_) -> failwith "Already subcribed"
        | None -> _handler <- _source |> Observable.subscribe _func |> Option.Some
    override x.Remove() =
        match _handler with
        | Some(actual) -> 
            actual.Dispose()
            _handler <- None
        | None -> ()

/// F# friendly typed wrapper around the WeakReference class 
type internal WeakReference<'T>( _weak : System.WeakReference ) =
    member x.Target = 
        let v = _weak.Target
        if v = null then None
        else 
            let v = v :?> 'T 
            Some v

    
module internal Utils =

    let IsFlagSet value flag = 
        let intValue = LanguagePrimitives.EnumToValue value
        let flagValue = LanguagePrimitives.EnumToValue flag
        0 <> (intValue &&& flagValue)

    let UnsetFlag value flag =
        let intValue = LanguagePrimitives.EnumToValue value
        let flagValue = LanguagePrimitives.EnumToValue flag
        let value = intValue &&& (~~~flagValue)
        LanguagePrimitives.EnumOfValue value

    /// Get the declared values of the specified enumeration
    let GetEnumValues<'T when 'T : enum<'T>>() : 'T seq=
        System.Enum.GetValues(typeof<'T>) |> Seq.cast<'T>

    /// Create a regex.  Returns None if the regex has invalid characters
    let TryCreateRegex pattern options =
        try
            let r = new System.Text.RegularExpressions.Regex(pattern, options)
            Some r
        with 
            | :? System.ArgumentException -> None

    /// Type safe helper method for creating a WeakReference<'T>
    let CreateWeakReference<'T when 'T : not struct> (value : 'T) = 
        let weakRef = System.WeakReference(value)
        WeakReference<'T>(weakRef)

    /// Read all of the lines from the file at the given path.  If this fails None
    /// will be returned
    let ReadAllLines path =
        try
            if System.String.IsNullOrEmpty path then None
            else
                let lines = System.IO.File.ReadAllLines(path)
                Some(path,lines)
        with
            _ -> None

module internal ListUtil =

    let divide l = (l |> List.head), (l |> List.tail)

    /// Try and get the head of the list.  Will return the head of list and tail 
    /// as separate elements.  Returns None if the list is empty
    let tryHead l = 
        if List.isEmpty l then None
        else Some (divide l)

    let tryHeadOnly l = 
        if List.isEmpty l then None
        else Some (List.head l)

    let tryProcessHead l ifNonEmpty ifEmpty =
        if List.isEmpty l then 
            ifEmpty()
        else
            let head,tail = divide l
            ifNonEmpty head tail

    let rec skip count l =
        if count <= 0 then l
        else 
            let _,tail = l |> divide
            skip (count-1) tail

module internal SeqUtil =
    
    /// Try and get the head of the Sequence.  Will return the head of list and tail 
    /// as separate elements.  Returns None if the list is empty
    let tryHead l = 
        if Seq.isEmpty l then None
        else 
            let head = Seq.head l
            let tail = l |> Seq.skip 1 
            Some (head,tail)

    let tryHeadOnly l = 
        if Seq.isEmpty l then None
        else Some (Seq.head l)

    /// Get the last element in the sequence.  Throws an ArgumentException if 
    /// the sequence is empty
    let last (s:'a seq) = 
        use e = s.GetEnumerator()
        if not (e.MoveNext()) then invalidArg "s" "Sequence must not be empty"

        let mutable value = e.Current
        while e.MoveNext() do
            value <- e.Current
        value

    /// Return if the sequence is not empty
    let isNotEmpty s = not (s |> Seq.isEmpty)

    /// Maps a seq of options to an option of list where None indicates at least one 
    /// entry was None and Some indicates all entries had values
    let allOrNone s =
        let rec inner s withNext =
            if s |> Seq.isEmpty then withNext [] |> Some
            else
                match s |> Seq.head with
                | None -> None
                | Some(cur) ->
                    let rest = s |> Seq.skip 1
                    inner rest (fun next -> withNext (cur :: next))
        inner s (fun all -> all)

    /// Append a single element to the end of a sequence
    let appendSingle element sequence = 
        let right = element |> Seq.singleton
        Seq.append sequence right

    /// Try and find the first value which meets the specified filter.  If it does not exist then
    /// return the specified default value
    let tryFindOrDefault filter defaultValue sequence =
        match Seq.tryFind filter sequence with
        | Some(value) -> value
        | None -> defaultValue 

module internal MapUtil =

    /// Get the set of keys in the Map
    let keys (map:Map<'TKey,'TValue>) = map |> Seq.map (fun pair -> pair.Key)

module internal CharUtil =
    let IsDigit x = System.Char.IsDigit(x)
    let IsWhiteSpace x = System.Char.IsWhiteSpace(x)
    let IsLetter x = System.Char.IsLetter(x)
    let IsLetterOrDigit x = System.Char.IsLetterOrDigit(x)

module internal NullableUtil = 

    let (|HasValue|Null|) (x:System.Nullable<_>) =
        if x.HasValue then
            HasValue (x.Value)
        else
            Null 