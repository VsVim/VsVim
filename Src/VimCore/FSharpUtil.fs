#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.ObjectModel
open System.Text
open System.Text.RegularExpressions

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
        | Some(_) -> failwith "Already subscribed"
        | None -> _handler <- _source |> Observable.subscribe _func |> Option.Some
    override x.Remove() =
        match _handler with
        | Some(actual) -> 
            actual.Dispose()
            _handler <- None
        | None -> ()

type internal StandardEvent<'T when 'T :> System.EventArgs>() =

    let _event = new DelegateEvent<System.EventHandler<'T>>()

    member x.Publish = _event.Publish

    member x.Trigger (sender : obj) (args : 'T) = 
        let argsArray = [| sender; args :> obj |]
        _event.Trigger(argsArray)

type internal StandardEvent() =

    let _event = new DelegateEvent<System.EventHandler>()

    member x.Publish = _event.Publish

    member x.Trigger (sender : obj) =
        let argsArray = [| sender; System.EventArgs.Empty :> obj |]
        _event.Trigger(argsArray)

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
        if v = null then
            None
        else 
            let v = v :?> 'T 
            Some v

module internal WeakReferenceUtil =

    let Create<'T> (value : 'T) = 
        let weakReference = System.WeakReference(value)
        WeakReference<'T>(weakReference)

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
            skip (count - 1) tail

    let rec skipMax count l = 
        if count <= 0 then 
            l
        else
            match l with 
            | [] -> []
            | _ :: tail -> skipMax (count - 1) tail

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

    let filter2 filter sequence = 
        seq {
            let index = ref 0
            for cur in sequence do
                if filter index.Value cur then
                    yield cur
                index.Value <- index.Value + 1
        }

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

    /// Same functionality as Seq.tryFind except it allows you to pass along a 
    /// state value along 
    let tryFind initialState predicate (sequence : 'a seq) =
        use e = sequence.GetEnumerator()
        let rec inner state = 
            match predicate e.Current state with
            | true, _ -> Some e.Current
            | false, state -> 
                if e.MoveNext() then
                    inner state
                else
                    None
        if e.MoveNext() then
            inner initialState
        else
            None

module internal MapUtil =

    /// Get the set of keys in the Map
    let keys (map:Map<'TKey,'TValue>) = map |> Seq.map (fun pair -> pair.Key)


[<RequireQualifiedAccess>]
type internal CharComparer =
    | Exact
    | IgnoreCase

    with

    member x.IsEqual left right = 
        if left = right then
            true
        else
            match x with
            | Exact -> false
            | IgnoreCase ->
                let left = System.Char.ToLower left
                let right = System.Char.ToLower right
                left = right

    member x.Compare (left : char) (right : char) = 
        if left = right then
            0
        else
            match x with
            | Exact -> 
                left.CompareTo(right)
            | IgnoreCase ->
                let left = System.Char.ToLower left
                let right = System.Char.ToLower right
                left.CompareTo(right)

    member x.GetHashCode (c : char) = 
        let c = 
            match x with
            | Exact -> c
            | IgnoreCase -> System.Char.ToLower c
        int c

/// Thin wrapper around System.String which allows us to compare values in a 
/// case insensitive + allocation free manner.  
[<Struct>]
[<CustomComparison>]
[<CustomEquality>]
type internal CharSpan 
    (
        _value : string, 
        _index : int,
        _length : int,
        _comparer : CharComparer
    ) = 

    new (value : string, comparer : CharComparer) = 
        CharSpan(value, 0, value.Length, comparer)

    member x.Length = _length

    member x.CharAt index = 
        let index = _index + index
        _value.[index]

    member x.StringComparison = 
        match _comparer with
        | CharComparer.Exact -> System.StringComparison.Ordinal
        | CharComparer.IgnoreCase -> System.StringComparison.OrdinalIgnoreCase

    member x.StringComparer = 
        match _comparer with
        | CharComparer.Exact -> System.StringComparer.Ordinal
        | CharComparer.IgnoreCase -> System.StringComparer.OrdinalIgnoreCase

    member x.CompareTo (other : CharSpan) = 
        let diff = x.Length - other.Length
        if diff <> 0 then
            diff
        else
            let mutable index = 0 
            let mutable value = 0 
            while index < x.Length && value = 0 do
                let comp = _comparer.Compare (x.CharAt index) (other.CharAt index)
                if comp <> 0 then
                    value <- comp

                index <- index + 1
            value

    member x.GetSubSpan index length = 
        CharSpan(_value, index + _index, length, _comparer)

    member x.IndexOf (c : char) = 
        let mutable index = 0
        let mutable found = -1
        while index < x.Length && found < 0 do
            if _comparer.IsEqual c (x.CharAt index) then
                found <- index
            index <- index + 1
        found

    member x.LastIndexOf c = 
        let mutable index = x.Length - 1
        let mutable found = -1
        while index >= 0 && found < 0 do
            if _comparer.IsEqual c (x.CharAt index) then
                found <- index
            index <- index - 1
        found

    member x.EqualsString str = 
        let other = CharSpan(str, _comparer)
        0 = x.CompareTo other

    override x.Equals(obj) =
        match obj with
        | :? CharSpan as other -> 0 = x.CompareTo other
        | _ -> false

    override x.ToString() = _value.Substring(_index, _length)

    override x.GetHashCode() = 
        let mutable hashCode = 0
        for i = 0 to _length - 1 do
            let current = _comparer.GetHashCode (x.CharAt i)
            hashCode <- hashCode ||| current
        hashCode

    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? CharSpan as other -> x.CompareTo other
            | _ -> invalidArg "yObj" "Cannot compare values of different types"

    interface System.IEquatable<CharSpan> with
        member x.Equals other = 0 = x.CompareTo other

module internal CharUtil =

    let MinValue = System.Char.MinValue
    let IsDigit x = System.Char.IsDigit(x)
    let IsWhiteSpace x = System.Char.IsWhiteSpace(x)
    let IsNotWhiteSpace x = not (System.Char.IsWhiteSpace(x))
    let IsControl x = System.Char.IsControl x

    /// Is this the Vim definition of a blank character.  That is it a space
    /// or tab
    let IsBlank x = x = ' ' || x = '\t'

    /// Is this a non-blank character in Vim
    let IsNotBlank x = not (IsBlank x)
    let IsAlpha x = (x >= 'a' && x <= 'z') || (x >= 'A' && x <= 'Z')
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

    /// Add 'count' to the given alpha value (a-z) and preserve case.  If the count goes
    /// past the a-z range then a or z will be returned
    let AlphaAdd count c =
        let isUpper = IsUpper c
        let number = 
            let c = ToLower c
            let index = (int c) - (int 'a')
            index + count 

        let lowerBound, upperBound = 
            if isUpper then 
                'A', 'Z'
            else 
                'a', 'z'
        if number < 0 then 
            lowerBound
        elif number >= 26 then 
            upperBound
        else
            char ((int lowerBound) + number) 

    /// Determines whether the given character occupies space on screen when displayed.
    /// For instance, combining diacritics occupy the space of the previous character,
    /// while control characters are simply not displayed.
    let IsNonSpacingCharacter c =
        // based on http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
        // TODO: this should be checked for consistency with
        //       Visual studio handling of each character.

        match System.Char.GetUnicodeCategory(c) with
        //Visual studio does not render control characters
        | System.Globalization.UnicodeCategory.Control
        | System.Globalization.UnicodeCategory.NonSpacingMark
        | System.Globalization.UnicodeCategory.Format
        | System.Globalization.UnicodeCategory.EnclosingMark ->
            /// Contrarily to http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
            /// the Soft hyphen (\u00ad) is invisible in VS.
            true
        | _ -> (c = '\u200b') || ('\u1160' <= c && c <= '\u11ff')

    /// Determines if the given character occupies a single or two cells on screen.
    let IsWideCharacter c =
        // based on http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
        // TODO: this should be checked for consistency with
        //       Visual studio handling of each character.
        (c >= '\u1100' &&
            (
                // Hangul Jamo init. consonants
                c <= '\u115f' || c = '\u2329' || c = '\u232a' ||
                // CJK ... Yi
                (c >= '\u2e80' && c <= '\ua4cf' && c <> '\u303f') ||
                // Hangul Syllables */
                (c >= '\uac00' && c <= '\ud7a3') ||
                // CJK Compatibility Ideographs
                (c >= '\uf900' && c <= '\ufaff') ||
                // Vertical forms
                (c >= '\ufe10' && c <= '\ufe19') ||
                // CJK Compatibility Forms
                (c >= '\ufe30' && c <= '\ufe6f') ||
                // Fullwidth Forms
                (c >= '\uff00' && c <= '\uff60') ||
                (c >= '\uffe0' && c <= '\uffe6')));

                // The following can only be detected with pairs of characters
                //   (surrogate characters)
                // Supplementary ideographic plane
                //(ucs >= 0x20000 && ucs <= 0x2fffd) ||
                // Tertiary ideographic plane
                //(ucs >= 0x30000 && ucs <= 0x3fffd)));

    /// Determines the character width when displayed, computed according to the various local settings.
    let GetCharacterWidth c tabStop =
        // TODO: for surrogate pairs, we need to be able to match characters specified as strings.
        // E.g. if System.Char.IsHighSurrogate(c) then
        //    let fullchar = point.Snapshot.GetSpan(point.Position, 1).GetText()
        //    CommontUtil.GetSurrogatePairWidth fullchar

        match c with
        | '\u0000' -> 1
        | '\t' -> tabStop
        | _ when IsNonSpacingCharacter c -> 0
        | _ when IsWideCharacter c -> 2
        | _ -> 1

    let GetDigitValue c = 
        match c with
        | '0' -> Some 0
        | '1' -> Some 1
        | '2' -> Some 2
        | '3' -> Some 3
        | '4' -> Some 4
        | '5' -> Some 5
        | '6' -> Some 6
        | '7' -> Some 7
        | '8' -> Some 8
        | '9' -> Some 9
        | _ -> None

module internal StringBuilderExtensions =

    type StringBuilder with
        member x.AppendChar (c : char) = 
            x.Append(c) |> ignore

        member x.AppendCharCount (c : char) (count : int) = 
            x.Append(c, count) |> ignore

        member x.AppendString (str : string) =
            x.Append(str) |> ignore
            
        member x.AppendStringCount (str : string) (count : int) =
            for i = 0 to count - 1 do
                x.Append(str) |> ignore

        member x.AppendNumber (number : int) =
            x.Append(number) |> ignore

        member x.AppendSubstring (str : string) (start : int) (length : int) =
            let mutable i = 0
            while i < length do 
                let c = str.[start + i]
                x.AppendChar c
                i <- i + 1

module internal CollectionExtensions = 

    type System.Collections.Generic.Stack<'T> with
        member x.PushRange (col : 'T seq) = 
            col |> Seq.iter (fun item -> x.Push(item))

    type System.Collections.Generic.Queue<'T> with
        member x.EnqueueRange (col : 'T seq) = 
            col |> Seq.iter (fun item -> x.Enqueue(item))

    type System.Collections.Generic.Dictionary<'TKey, 'TValue> with
        member x.TryGetValueEx (key : 'TKey) = 
            let found, value = x.TryGetValue key
            if found then
                Some value
            else
                None

module internal NullableUtil = 

    let (|HasValue|Null|) (x : System.Nullable<_>) =
        if x.HasValue then
            HasValue (x.Value)
        else
            Null 

    let Create (x : 'T) =
        System.Nullable<'T>(x)

    let ToOption (x : System.Nullable<_>) =
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

    /// Map an option ta a value which produces an option and then collapse the result
    let map2 mapFunc value =
        match value with
        | None -> None
        | Some value -> mapFunc value

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

type internal ReadOnlyCollectionUtil<'T>() = 

    static let s_empty = 
        let list = System.Collections.Generic.List<'T>()
        ReadOnlyCollection<'T>(list)

    static member Empty = s_empty

    static member Single (item : 'T) = 
        let list = System.Collections.Generic.List<'T>()
        list.Add(item)
        ReadOnlyCollection<'T>(list)

    static member OfSeq (collection : 'T seq) = 
        let list = System.Collections.Generic.List<'T>(collection)
        ReadOnlyCollection<'T>(list)

type Contract = 

    static member Requires test = 
        if not test then
            raise (System.Exception("Contract failed"))

    [<System.Diagnostics.Conditional("DEBUG")>]
    static member Assert test = 
        if not test then
            raise (System.Exception("Contract failed"))

    static member GetInvalidEnumException<'T> (value : 'T) : System.Exception =
        let msg = sprintf "The value %O is not a valid member of type %O" value typedefof<'T>
        System.Exception(msg)

    static member FailEnumValue<'T> (value : 'T) : unit = 
        raise (Contract.GetInvalidEnumException value)

module internal SystemUtil =

    let TryGetEnvironmentVariable name = 
        try
            let value = System.Environment.GetEnvironmentVariable(name) 
            if value = null then
                None
            else
                Some value
        with
            | _ -> None

    let GetEnvironmentVariable name = 
        match TryGetEnvironmentVariable name with
        | Some name -> name
        | None -> ""

    /// The IO.Path.Combine API has a lot of "features" which basically prevents it
    /// from being a reliable API.  The most notable is that if you pass it c:
    /// instead of c:\ it will silently fail.
    let CombinePath (path1 : string) (path2 : string) = 

        // Work around the c: problem by adding a trailing slash to a drive specification
        let path1 = 
            if System.String.IsNullOrEmpty(path1) then
                ""
            elif path1.Length = 2 && CharUtil.IsLetter path1.[0] && path1.[1] = ':' then
                path1 + @"\"
            else
                path1

        // Remove the begining slash from the second path so that it will combine properly
        let path2 =
            if System.String.IsNullOrEmpty(path2) then
                ""
            elif path2.[0] = '\\' then 
                path2.Substring(1)
            else
                path2

        System.IO.Path.Combine(path1, path2)

    /// Get the value of $HOME.  There is no explicit documentation that I could find 
    /// for how this value is calculated.  However experimentation shows that gVim 7.1
    /// calculates it in the following order 
    ///     %HOME%
    ///     %HOMEDRIVE%%HOMEPATH%
    ///     c:\
    let GetHome () = 
        match TryGetEnvironmentVariable "HOME" with
        | Some path -> path
        | None -> 
            match TryGetEnvironmentVariable "HOMEDRIVE", TryGetEnvironmentVariable "HOMEPATH" with
            | Some drive, Some path -> CombinePath drive path
            | _ -> @"c:\"

    /// Whether text starts with a leading tilde and is followed by either
    // nothing else or a directory separator
    let StartsWithTilde (text : string) =
        if text.StartsWith("~") then
            if text.Length = 1 then
                true
            else
                let separator = text.Chars(1)
                if separator = System.IO.Path.DirectorySeparatorChar then
                    true
                elif separator = System.IO.Path.AltDirectorySeparatorChar then
                    true
                else
                    false
        else
            false
                
    /// Expand environment variables leaving undefined variables "as is"
    ///
    /// This function considers a leading tilde as equivalent to "$HOME" or
    /// "$HOMEDRIVE$HOMEPATH" if those variables are defined
    let ResolvePath text =

        let processMatch (regexMatch : Match) =
            let token = regexMatch.Value
            if token.StartsWith("$") then
                let variable = token.Substring(1)
                match TryGetEnvironmentVariable variable with
                | Some value ->
                    value
                | None ->
                    token
            else
                token

        // According to Shell and Utilities volume of IEEE Std 1003.1-2001
        // standard environment variable names should consist solely of
        // uppercase letters, digits, and the '_'. However variables with
        // lowercasesee lowercase letters are also popular, thus the regex
        // below supports them as well.
        // http://pubs.opengroup.org/onlinepubs/009695399/basedefs/xbd_chap08.html
        let text =
            Regex.Matches(text, "(\$[\w_][\w\d_]*)|([^$]+)")
            |> Seq.cast
            |> Seq.map processMatch
            |> String.concat ""

        if StartsWithTilde text then
            GetHome() + text.Substring(1)
        else
            text

    /// Try to expand all the referenced environment variables
    let TryResolvePath text =
        let text = ResolvePath text
        if StartsWithTilde text || text.Contains("$") then
            None
        else
            Some text

    let EnsureRooted currentDirectory text = 
        if System.IO.Path.IsPathRooted text || not (System.IO.Path.IsPathRooted currentDirectory) then
            text
        else
            CombinePath currentDirectory text

    /// Like ResolvePath except it will always return a rooted path.  If the provided path
    /// isn't rooted it will be rooted inside of 'currentDirectory'
    let ResolveVimPath currentDirectory text = 
        match text with
        | "." -> currentDirectory
        | ".." -> System.IO.Path.GetPathRoot currentDirectory
        | _ -> 
            let text = ResolvePath text
            EnsureRooted currentDirectory text

    let TryResolveVimPath currentDirectory text =
        TryResolvePath text |> Option.map (fun text -> EnsureRooted currentDirectory text)
