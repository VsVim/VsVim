#light

namespace Vim.Interpreter
open Vim

[<RequireQualifiedAccess>]
type ParseResult<'T> = 
    | Succeeded of 'T
    | Failed of string

[<Sealed>]
type Parser
    (
        _text : string
    ) = 

    /// Current index into the expression text
    let mutable _index = 0

    member x.CurrentChar =
        if _index >= _text.Length then
            None
        else
            Some _text.[_index]

    member x.IsCurrentChar predicate = 
        match x.CurrentChar with
        | None -> false
        | Some c -> predicate c

    member x.IsCurrentCharValue value =
        match x.CurrentChar with
        | None -> false
        | Some c -> c = value

    member x.RemainingText =
        _text.Substring(_index)

    member x.IncrementIndex() =
        if _index < _text.Length then
            _index <- _index + 1

    /// Move past the white space in the expression text
    member x.SkipBlanks () = 
        if x.IsCurrentChar CharUtil.IsBlank then
            x.IncrementIndex()
            x.SkipBlanks()
        else
            ()

    /// Skip the ! operator if it's the current character.  Return true if the ! was
    /// actually skipped
    member x.SkipBang () = 
        if x.IsCurrentChar (fun c -> c = '!') then
            x.IncrementIndex()
            true
        else
            false

    /// Parse out a single word from the text.  This will simply take the current cursor
    /// position and move while IsLetter is true.  This will return None if the resulting
    /// string is blank.  This will not skip any blanks
    member x.ParseWord () = 
        if x.IsCurrentChar CharUtil.IsLetter then
            let startIndex = _index
            x.IncrementIndex()
            let length = 
                let rec inner () = 
                    if x.IsCurrentChar CharUtil.IsLetter then
                        x.IncrementIndex()
                        inner ()
                inner()
                _index - startIndex
            let text = _text.Substring(startIndex, length)
            Some text
        else
            None

    /// Parse out a number from the current text
    member x.ParseNumber () = 

        // If c is a digit char then return back the digit
        let toDigit c = 
            if CharUtil.IsDigit c then
                (int c) - (int '0') |> Some
            else
                None

        // Get the current char as a digit if it is one
        let currentAsChar () = 
            match x.CurrentChar with
            | None -> None
            | Some c -> toDigit c

        let rec inner value = 
            match currentAsChar() with
            | None -> 
                value
            | Some number ->
                let value = (value * 10) + number
                x.IncrementIndex()
                inner value

        match currentAsChar() with
        | None -> 
            None
        | Some number -> 
            x.IncrementIndex()
            inner number |> Some

    /// Parse out the :close command
    member x.ParseClose() = 
        let isBang = x.SkipBang()
        LineCommand.Close isBang |> ParseResult.Succeeded

    /// Parse out the :[digit] command
    member x.ParseJumpToLine () =
        match x.ParseNumber() with
        | None -> ParseResult.Failed Resources.Parser_Error
        | Some number -> ParseResult.Succeeded (LineCommand.JumpToLine number)

    /// Parse out the :$ command
    member x.ParseJumpToLastLine() =
        ParseResult.Succeeded (LineCommand.JumpToLastLine)

    /// Parse out a single char from the text
    member x.ParseChar() = 
        match x.CurrentChar with
        | None -> 
            None
        | Some c -> 
            x.IncrementIndex()
            Some c

    /// Parse a {pattern} out of the text.  The text will be consumed until the unescaped value 
    /// 'delimiter' is provided
    member x.ParsePattern delimiter = 
        let mark = _index
        let rec inner () = 
            match x.CurrentChar with
            | None -> 
                // Hit the end without finding 'delimiter' so there is no pattern
                _index <- mark
                None 
            | Some c -> 
                if c = delimiter then 
                    let text = _text.Substring(mark, _index - mark)
                    x.IncrementIndex()
                    Some text
                elif c = '\\' then
                    x.IncrementIndex()
                    x.IncrementIndex()
                    inner()
                else
                    x.IncrementIndex()
                    inner()

        inner ()

    /// Parse out a LineSpecifier from the text.
    ///
    /// If there is no valid line specifier at the given place in the text then the 
    /// index should not be adjusted
    member x.ParseLineSpecifier () =

        let lineSpecifier = 
            if x.IsCurrentCharValue '.' then
                x.IncrementIndex()
                Some LineSpecifier.CurrentLine
            elif x.IsCurrentCharValue '\'' then
                x.IncrementIndex()
                x.ParseChar() 
                |> OptionUtil.map2 Mark.OfChar
                |> Option.map LineSpecifier.MarkLine
            elif x.IsCurrentCharValue '$' || x.IsCurrentCharValue '%' then
                x.IncrementIndex()
                Some LineSpecifier.LastLine
            elif x.IsCurrentCharValue '/' then

                // It's one of the forward pattern specifiers
                x.IncrementIndex()
                if x.IsCurrentCharValue '/' then
                    Some LineSpecifier.NextLineWithPreviousPattern
                elif x.IsCurrentCharValue '?' then
                    Some LineSpecifier.PreviousLineWithPreviousPattern
                elif x.IsCurrentCharValue '&' then
                    Some LineSpecifier.NextLineWithPreviousSubstitutePattern
                else
                    match x.ParsePattern '/' with
                    | None ->
                        None
                    | Some pattern -> 
                        Some (LineSpecifier.NextLineWithPattern pattern)

            elif x.IsCurrentCharValue '?' then
                // It's the ? previous search pattern
                x.IncrementIndex()
                match x.ParsePattern '?' with
                | None -> 
                    None
                | Some pattern ->
                    Some (LineSpecifier.PreviousLineWithPattern pattern)

            elif x.IsCurrentCharValue '+' then
                x.IncrementIndex()
                x.ParseNumber() |> Option.map LineSpecifier.AdjustmentOnCurrent
            elif x.IsCurrentCharValue '-' then
                x.IncrementIndex()
                x.ParseNumber() |> Option.map (fun number -> LineSpecifier.AdjustmentOnCurrent -number)
            else 
                match x.ParseNumber() with
                | None -> None
                | Some number -> Some (LineSpecifier.Number number)

        // Need to check for a trailing + or - 
        match lineSpecifier with
        | None ->
            None
        | Some lineSpecifier ->
            let parseAdjustment isNegative = 
                x.IncrementIndex()

                // If no number is specified then 1 is used instead
                let number = x.ParseNumber() |> OptionUtil.getOrDefault 1
                let number = 
                    if isNegative then
                        -number
                    else
                        number

                Some (LineSpecifier.LineSpecifierWithAdjustment (lineSpecifier, number))
            if x.IsCurrentCharValue '+' then
                parseAdjustment false
            elif x.IsCurrentCharValue '-' then
                parseAdjustment true
            else
                Some lineSpecifier

    /// Parse out any valid range node.  This will consider % and any other 
    /// range expression
    member x.ParseLineRange () =
        if x.IsCurrentCharValue '%' then
            x.IncrementIndex()
            LineRange.EntireBuffer |> Some
        else
            match x.ParseLineSpecifier() with
            | None ->
                None
            | Some left ->
                if x.IsCurrentCharValue ',' then
                    x.IncrementIndex()
                    x.ParseLineSpecifier()
                    |> Option.map (fun right -> LineRange.Range (left, right, false))
                elif x.IsCurrentCharValue ';' then
                    x.IncrementIndex()
                    x.ParseLineSpecifier()
                    |> Option.map (fun right -> LineRange.Range (left, right, true))
                else
                    LineRange.SingleLine left |> Some

    /// Parse out a single expression
    member x.ParseSingleCommand () = 

        if x.IsCurrentChar CharUtil.IsDigit then
            x.ParseJumpToLine()
        elif x.IsCurrentCharValue '$' then
            x.ParseJumpToLastLine()
        else
            let lineRange = x.ParseLineRange()

            let noRange parseFunc = 
                match lineRange with
                | None -> x.ParseClose()
                | Some _ -> ParseResult.Failed Resources.Parser_NoRangeAllowed

            match x.ParseWord() |> OptionUtil.getOrDefault "" with
            | "close" -> noRange x.ParseClose
            | _ -> ParseResult.Failed Resources.Parser_Error

    // TODO: Delete.  This is just a transition hack to allow us to use the new interpreter and parser
    // to replace RangeUtil.ParseRange
    static member ParseRange rangeText = 
        let parser = Parser(rangeText)
        let lineRange = parser.ParseLineRange()
        match lineRange with 
        | None -> ParseResult.Failed Resources.Parser_Error
        | Some lineRange -> ParseResult.Succeeded (lineRange, parser.RemainingText) 

    static member ParseExpression (expressionText : string) : ParseResult<Expression> = 
        ParseResult.Failed Resources.Parser_Error

    static member ParseLineCommand (commandText : string) = 
        let parser = Parser(commandText)
        parser.ParseSingleCommand()

