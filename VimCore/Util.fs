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

type internal DisposableBag() = 
    let mutable _toDispose : System.IDisposable list = List.empty
    member x.DisposeAll () = 
        _toDispose |> List.iter (fun x -> x.Dispose()) 
        _toDispose <- List.empty
    member x.Add d = _toDispose <- d :: _toDispose 

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

    let rec skipWhile predicate l = 
        match l with
        | h::t -> 
            if predicate h then skipWhile predicate t
            else l
        | [] -> l

    let rec contentsEqual left right = 
        if List.length left <> List.length right then false
        else
            let leftData = tryHead left
            let rightData = tryHead right
            match leftData,rightData with
            | None,None -> true
            | Some(_),None -> false
            | None,Some(_) -> false
            | Some(leftHead,leftRest),Some(rightHead,rightRest) -> 
                if leftHead = rightHead then contentsEqual leftRest rightRest
                else false

    let rec contains value l =
        match l with
        | h::t -> 
            if h = value then true
            else contains value t
        | [] -> false

        
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

    /// Filter the list removing all None's 
    let filterToSome sequence =
        seq {
            for cur in sequence do
                match cur with
                | Some(value) -> yield value
                | None -> ()
        }

    /// Filters the list removing all of the first tuple arguments which are None
    let filterToSome2 (sequence : ('a option * 'b) seq) =
        seq { 
            for cur in sequence do
                let first,second = cur
                match first with
                | Some(value) -> yield (value,second)
                | None -> ()
        }

    let contentsEqual (left:'a seq) (right:'a seq) = 
        use leftEnumerator = left.GetEnumerator()        
        use rightEnumerator = right.GetEnumerator()

        let mutable areEqual = false
        let mutable isDone = false
        while not isDone do
            let leftMove = leftEnumerator.MoveNext()
            let rightMove = rightEnumerator.MoveNext()
            isDone <- 
                if not leftMove && not rightMove then
                    areEqual <- true
                    true
                elif leftMove <> rightMove then true
                elif leftEnumerator.Current <> rightEnumerator.Current then true
                else false

        areEqual

    let takeMax count (sequence:'a seq) = 
        let i = ref 0
        sequence |> Seq.takeWhile (fun _ -> 
            i := !i + 1
            if !i <= count then true
            else false ) |> List.ofSeq
            
module internal MapUtil =

    /// Get the set of keys in the Map
    let keys (map:Map<'TKey,'TValue>) = map |> Seq.map (fun pair -> pair.Key)

module internal CharUtil =
    let IsDigit x = System.Char.IsDigit(x)
    let IsWhiteSpace x = System.Char.IsWhiteSpace(x)
    let IsNotWhiteSpace x = not (System.Char.IsWhiteSpace(x))
    let IsLetter x = System.Char.IsLetter(x)
    let IsUpper x = System.Char.IsUpper(x)
    let IsLower x = System.Char.IsLower(x)
    let IsLetterOrDigit x = System.Char.IsLetterOrDigit(x)
    let ToLower x = System.Char.ToLower(x)
    let ToUpper x = System.Char.ToUpper(x)
    let ChangeCase x = if IsUpper x then ToLower x else ToUpper x
    let LettersLower = ['a'..'z']
    let LettersUpper = ['A'..'Z']
    let Letters = Seq.append LettersLower LettersUpper 
    let Digits = ['0'..'9']
    let IsEqual left right = left = right
    let IsEqualIgnoreCase left right = 
        let func c = if IsLetter c then ToLower c else c
        let left = func left
        let right  = func right
        left = right

    let (|WhiteSpace|NonWhiteSpace|) char =
        if IsWhiteSpace char then
            WhiteSpace
        else
            NonWhiteSpace

module internal NullableUtil = 

    let (|HasValue|Null|) (x:System.Nullable<_>) =
        if x.HasValue then
            HasValue (x.Value)
        else
            Null 

    let toOption (x:System.Nullable<_>) =
        if x.HasValue then
            Some x.Value
        else
            None

module internal OptionUtil =
    
    /// Combine an option with another value.  If the option has no value then the result
    /// is None.  If the option has a value the result is an Option of a tuple of the original
    /// value and the passed in one
    let combine opt value =
        match opt with
        | Some(optValue) -> Some (optValue,value)
        | None -> None

    /// Combine an option with another value.  Same as combine but takes a tuple'd argument
    let combine2 (opt,value) = combine opt value

    /// Combine an option with another value.  If the option has no value then the result
    /// is None.  If the option has a value the result is an Option of a tuple of the original
    /// value and the passed in one
    let combineRev value opt =
        match opt with
        | Some(optValue) -> Some (value, optValue)
        | None -> None

    /// Combine an option with another value.  Same as combine but takes a tuple'd argument
    let combineRev2 (value,opt) = combine opt value


