#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Contains motion actions which are generally useful to components
module MotionUtil = 

    let GetParagraphs point direction = 

        // Get the paragraphs for the given span in a forward fashion
        let forSpanForward span = 
            seq { 
                let leadingEdge,lines,trailingEdge = SnapshotSpanUtil.GetLinesAndEdges span
                let startPoint = ref span.Start
                for line in lines do
                    if line.Length = 0 then 
                        // Guard against back to back newlines 
                        if !startPoint <> line.Start then 
                            yield SnapshotSpan(!startPoint, line.Start) |> Paragraph.Content

                        yield Paragraph.Boundary (line.LineNumber, line.ExtentIncludingLineBreak)
                        startPoint := line.EndIncludingLineBreak
                if !startPoint <> span.End then 
                    yield SnapshotSpan(!startPoint, span.End) |> Paragraph.Content
            }

        // Get the sentences for the given span in a backward fashion
        let forSpanBackward span = 
            seq { 
                let leadingEdge,lines,trailingEdge = SnapshotSpanUtil.GetLinesAndEdges span
                let endPoint = ref span.End
                for line in lines |> List.ofSeq |> List.rev do
                    if line.Length = 0 then 
                        // Guard against back to back newlines 
                        if !endPoint <> line.EndIncludingLineBreak then
                            yield SnapshotSpan(line.EndIncludingLineBreak, !endPoint) |> Paragraph.Content

                        yield Paragraph.Boundary (line.LineNumber, line.ExtentIncludingLineBreak)
                        endPoint := line.Start
                if !endPoint <> span.Start then 
                    yield SnapshotSpan(span.Start, !endPoint) |> Paragraph.Content
            }

        let mapSpan = 
            match direction with 
            | Direction.Forward -> forSpanForward
            | Direction.Backward -> forSpanBackward
        SnapshotPointUtil.GetDividedSnapshotSpans point (SearchKindUtil.OfDirection direction)
        |> Seq.map mapSpan
        |> Seq.concat

    let GetParagraphsInSpan span direction = 
        match direction with
        | Direction.Forward -> 
            seq {
                let start = SnapshotSpanUtil.GetStartPoint span
                let spans = 
                    GetParagraphs start direction
                    |> Seq.takeWhile (fun p -> p.Span.Start.Position < span.End.Position)
                for item in spans do
                    if item.Span.End.Position > span.End.Position then 
                        yield item.ReduceToSpan (SnapshotSpan(item.Span.Start, span.End))
                    else yield item
            }
        | Direction.Backward ->
            seq {
                let endPoint = SnapshotSpanUtil.GetEndPoint span
                let spans = 
                    GetParagraphs endPoint direction
                    |> Seq.takeWhile (fun p -> p.Span.End.Position > span.Start.Position)
                for item in spans do
                    if item.Span.Start.Position < span.Start.Position then
                        yield item.ReduceToSpan (SnapshotSpan(span.Start, item.Span.End))
                    else yield item
            }

    let GetFullParagraph (point:SnapshotPoint) =

        let isContent p =
            match p with 
            | Paragraph.Content(_) -> true
            | Paragraph.Boundary(_) -> false

        let isBoundary p =
            match p with 
            | Paragraph.Content(_) -> false
            | Paragraph.Boundary(_) -> true

        // Get the span when the point is not inside or at the end
        // of a content portion.  It is instead inside of a boundary.  The
        // 'start' param points to the end of the previous content boundary 
        let findFromBoundary start = 
            let endPoint = 
                GetParagraphs start Direction.Forward
                |> Seq.skipWhile isBoundary
                |> Seq.map ( fun p -> p.Span.End )
                |> SeqUtil.tryHeadOnly
                |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint point.Snapshot)
            SnapshotSpan(start,endPoint)

        // Get the span when the point is at the end of a Content portion 
        // of a paragraph. 
        let findFromContentEnd () = findFromBoundary point

        // Get the span when the point is within a Content portion of a 
        // paragraph.  Take the content regino and all of the trailing Bonudary
        // until the next Content portion.  The 'start' param points to the 
        // start of the Content span
        let findFromContentBody start =
            let endPoint =
                GetParagraphs start Direction.Forward
                |> Seq.skip 1 // The Content 
                |> Seq.skipWhile isBoundary
                |> Seq.map (fun p -> p.Span.Start )
                |> SeqUtil.tryHeadOnly
                |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint point.Snapshot)
            SnapshotSpan(start, endPoint)

        // Deteremine where the point is so we can do the proper calculation
        match GetParagraphs point Direction.Backward |> SeqUtil.tryHeadOnly with
        | None -> findFromBoundary (SnapshotUtil.GetStartPoint point.Snapshot)
        | Some(p)->
            match p with
            | Paragraph.Boundary(_,span) -> 
                let start = 
                    GetParagraphs point Direction.Backward
                    |> Seq.skipWhile isBoundary
                    |> Seq.map (fun p -> p.Span.End)
                    |> SeqUtil.tryHeadOnly
                    |> OptionUtil.getOrDefault (SnapshotUtil.GetStartPoint point.Snapshot)
                findFromBoundary start
            | Paragraph.Content(span) -> 
                let fullSpan = 
                    GetParagraphs span.Start Direction.Forward
                    |> Seq.map (fun p -> p.Span)
                    |> SeqUtil.tryHeadOnly
                    |> Option.get
                if fullSpan.End = point then findFromContentEnd()
                else findFromContentBody fullSpan.Start

    /// Set of characters which represent the end of a sentence. 
    let SentenceEndChars = ['.'; '!'; '?']

    /// Set of characters which can validly follow a sentence 
    let SentenceTrailingChars = [')';']'; '"'; '\'']

    /// Set of characters which must be after the End to truly (and finally) end
    /// the sentence. 
    let SentenceAfterEndChars = [' '; '\t'; '\r'; '\n']

    let GetSentences point direction = 

        // Get the sentences for the given span in a forward fashion
        let forSpanForward span = 
            seq {
                let startPoint = ref (SnapshotSpanUtil.GetStartPoint span)
                let inEnd = ref false
                for point in SnapshotSpanUtil.GetPoints span do
                    let char = SnapshotPointUtil.GetChar point
                    if not !inEnd then 
                        if ListUtil.contains char SentenceEndChars then inEnd := true
                        else ()
                    else 
                        if ListUtil.contains char SentenceTrailingChars then ()
                        elif ListUtil.contains char SentenceAfterEndChars then 
                            yield SnapshotSpan(!startPoint,point)
                            startPoint := point
                            inEnd := false
                        else
                            inEnd := false

                // Catch the remainder of the span which may not end in a sentence delimeter
                if !startPoint <> span.End then yield SnapshotSpan(!startPoint,span.End)
            }

        // Get the sentences for the given span in a backward fashion
        let forSpanBackward span = 
            seq {
                // Start of the previous sentence point.  Should be the end point
                // of the next returned SnapshotSpan
                let endPoint = ref (SnapshotSpanUtil.GetEndPoint span)

                // If we see a tail character while enumerating backwards this will
                // note the point before the first tail character
                let tailPoint : SnapshotPoint option ref = ref None

                for point in SnapshotSpanUtil.GetPointsBackward span do
                    let char = SnapshotPointUtil.GetChar point

                    // Is the character to the right a char which truly terminates the 
                    // sentence
                    let isRightAfterEndChar = 
                        let right = SnapshotPointUtil.AddOne point
                        if SnapshotPointUtil.IsEndPoint right then false
                        else 
                            let rightChar = SnapshotPointUtil.GetChar right
                            ListUtil.contains rightChar SentenceAfterEndChars

                    // If this is an end character and the character to the right is a terminating
                    // character or we have tail point then it's a sentence end
                    if ListUtil.contains char SentenceEndChars && (isRightAfterEndChar || Option.isSome !tailPoint) then
                        let startPoint = 
                            match !tailPoint with
                            | None -> point.Add(1) // don't include the end char
                            | Some(t) -> t
                        if startPoint <> !endPoint then
                            yield SnapshotSpan(startPoint, !endPoint)
                            endPoint := startPoint
                            tailPoint := None
                    elif ListUtil.contains char SentenceTrailingChars && Option.isNone !tailPoint && isRightAfterEndChar then
                        tailPoint := Some (point.Add(1))
                    else 
                        tailPoint := None

                // Get the begining of the span
                if !endPoint <> span.Start then yield SnapshotSpan(span.Start, !endPoint)
            }

        seq {
            let mapSpan = 
                match direction with
                | Direction.Forward -> forSpanForward
                | Direction.Backward -> forSpanBackward

            let items = 
                SnapshotPointUtil.GetDividedSnapshotSpans point (SearchKindUtil.OfDirection direction)
                |> Seq.map (fun span -> GetParagraphsInSpan span direction)
                |> Seq.concat

            for item in items do
                match item with
                | Paragraph.Boundary(_,span) -> yield span
                | Paragraph.Content(span) -> yield! mapSpan span 
        }

    let GetSentenceFull point = 
        GetSentences point Direction.Backward
        |> Seq.tryFind (fun x -> x.Contains(point))
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateFromStartToProvidedEnd point)

