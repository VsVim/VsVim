#light

namespace Vim
open Microsoft.VisualStudio.Text

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

    /// Is the head of the list the specified value
    let isHead value list = 
        match list with
        | [] -> false
        | head :: _ -> head = value

module internal SeqUtil =
    
    /// Try and get the head of the Sequence.  Will return the head of list and tail 
    /// as separate elements.  Returns None if the list is empty
    let tryHead l = 
        if Seq.isEmpty l then None
        else 
            let head = Seq.head l
            let tail = l |> Seq.skip 1 
            Some (head,tail)

    /// Try and get the head of the sequence
    let tryHeadOnly (sequence : 'a seq) = 
        use e = sequence.GetEnumerator()
        if e.MoveNext() then
            Some e.Current
        else
            None

    /// Get the head of the sequence or the default value if the sequence is empty
    let headOrDefault defaultValue l =
        match tryHeadOnly l with
        | Some h -> h
        | None -> defaultValue

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

    /// Returns if any of the elements in the sequence match the provided filter
    let any filter s = s |> Seq.filter filter |> isNotEmpty

    /// Returns if there exits an element in the collection which matches the specified 
    /// filter.  Identical to exists except it passes an index
    let existsi filter s = 
        s
        |> Seq.mapi (fun i e -> (i, e))
        |> Seq.exists (fun (i, e) -> filter i e)

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

    /// Skip's a maximum of count elements.  If there are more than
    /// count elements in the sequence then an empty sequence will be 
    /// returned
    let skipMax count (sequence:'a seq) = 
        let inner count = 
            seq {
                let count = ref count
                use e = sequence.GetEnumerator()
                while !count > 0 && e.MoveNext() do
                    count := !count - 1
                while e.MoveNext() do
                    yield e.Current }
        inner count

module internal MapUtil =

    /// Get the set of keys in the Map
    let keys (map:Map<'TKey,'TValue>) = map |> Seq.map (fun pair -> pair.Key)

module internal CharUtil =
    let MinValue = System.Char.MinValue
    let IsDigit x = System.Char.IsDigit(x)

    /// TODO: Consider uses of this.  Should be using IsBlank
    let IsWhiteSpace x = System.Char.IsWhiteSpace(x)

    /// TODO: Consider uses of this.  Should be using IsNotBlank
    let IsNotWhiteSpace x = not (System.Char.IsWhiteSpace(x))

    /// Is this the Vim definition of a blank character.  That is it a space
    /// or tab
    let IsBlank x = x = ' ' || x = '\t'

    /// Is this a non-blank character in Vim
    let IsNotBlank x = not (IsBlank x)
    let IsLetter x = System.Char.IsLetter(x)
    let IsUpper x = System.Char.IsUpper(x)
    let IsUpperLetter x = IsUpper x && IsLetter x
    let IsLower x = System.Char.IsLower(x)
    let IsLowerLetter x = IsLower x && IsLetter x
    let IsLetterOrDigit x = System.Char.IsLetterOrDigit(x)
    let ToLower x = System.Char.ToLower(x)
    let ToUpper x = System.Char.ToUpper(x)
    let ChangeCase x = if IsUpper x then ToLower x else ToUpper x
    let ChangeRot13 (x : char) = 
        let isUpper = IsUpper x 
        let x = ToLower x
        let index = int x - int 'a'
        let index = (index + 13 ) % 26
        let c = char (index + int 'a')
        if isUpper then ToUpper c else c 
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

    /// Get the Char value for the given ASCII code
    let OfAsciiValue (value : byte) =
        let asciiArray = [| value |]
        let charArray = System.Text.Encoding.ASCII.GetChars(asciiArray)
        if charArray.Length > 0 then
            charArray.[0]
        else
            MinValue

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

    /// Collapse an option of an option to just an option
    let collapse<'a> (opt : 'a option option) =
        match opt with
        | None -> None
        | Some opt -> opt

    /// Combine an option with another value.  If the option has no value then the result
    /// is None.  If the option has a value the result is an Option of a tuple of the original
    /// value and the passed in one
    let combine opt value =
        match opt with
        | Some(optValue) -> Some (optValue,value)
        | None -> None

    /// Combine an option with another value.  Same as combine but takes a tupled argument
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

    /// Combine two options into a single option.  Only some if both are some
    let combineBoth left right =
        match left,right with
        | Some(left),Some(right) -> Some(left,right)
        | Some(_),None -> None
        | None,Some(_) -> None
        | None,None -> None

    /// Combine two options into a single option.  Only some if both are some
    let combineBoth2 (left,right) = combineBoth left right

    /// Get the value or the provided default
    let getOrDefault defaultValue opt =
        match opt with 
        | Some(value) -> value
        | None -> defaultValue

    /// Convert the Nullable<T> to an Option<T>
    let ofNullable (value : System.Nullable<'T>) =
        if value.HasValue then
            Some value.Value
        else
            None

/// Represents a collection which is guarantee to have at least a single element.  This
/// is very useful when dealing with discriminated unions of values where one is an element
/// and another is a collection where the collection has the constraint that it must 
/// have at least a single element.  This collection type allows the developer to avoid
/// the use of unsafe operations like List.head or SeqUtil.headOnly in favor of guaranteed 
/// operations
type NonEmptyCollection<'T> 
    (
        _head : 'T,
        _rest : 'T list
    ) = 

    /// Number of items in the collection
    member x.Count = 1 + _rest.Length

    /// Head of the collection
    member x.Head = _head

    /// The remainder of the collection after the 'Head' element
    member x.Rest = _rest

    /// All of the items in the collection
    member x.All = 
        seq {
            yield _head
            for cur in _rest do
                yield cur
        }

    interface System.Collections.IEnumerable with
        member x.GetEnumerator () = x.All.GetEnumerator() :> System.Collections.IEnumerator

    interface System.Collections.Generic.IEnumerable<'T> with
        member x.GetEnumerator () = x.All.GetEnumerator()

module NonEmptyCollectionUtil =

    /// Appends a list to the NonEmptyCollection
    let Append values (col : NonEmptyCollection<'T>) =
        let rest = col.Rest @ values
        NonEmptyCollection(col.Head, rest)

    /// Attempts to create a NonEmptyCollection from a raw sequence
    let OfSeq seq = 
        match SeqUtil.tryHead seq with
        | None -> None
        | Some (head, rest) -> NonEmptyCollection(head, rest |> List.ofSeq) |> Some

    /// Maps the elements in the NonEmptyCollection using the specified function
    let Map mapFunc (col : NonEmptyCollection<'T>) = 
        let head = mapFunc col.Head
        let rest = List.map mapFunc col.Rest
        NonEmptyCollection<_>(head, rest)

type Contract = 

    static member Requires test = 
        if not test then
            raise (System.Exception("Contract failed"))

    [<System.Diagnostics.Conditional("DEBUG")>]
    static member Assert test = 
        if not test then
            raise (System.Exception("Contract failed"))

