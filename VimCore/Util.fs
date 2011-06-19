#light

namespace Vim
open Microsoft.VisualStudio.Text

module internal Util =

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
    let GetEnumValues<'T when 'T : enum<int>>() : 'T seq=
        System.Enum.GetValues(typeof<'T>) |> Seq.cast<'T>

    /// Type safe helper method for creating a WeakReference<'T>
    let CreateWeakReference<'T when 'T : not struct> (value : 'T) = 
        let weakRef = System.WeakReference(value)
        WeakReference<'T>(weakRef)

    /// Get the point from which an incremental search should begin given
    /// a context point.  They don't begin at the point but rather before
    /// or after the point depending on the direction.  Return true if 
    /// a wrap was needed to get the start point
    let GetSearchPointAndWrap path point = 
        match path with
        | Path.Forward ->
            match SnapshotPointUtil.TryAddOne point with 
            | Some point -> point, false
            | None -> SnapshotPoint(point.Snapshot, 0), true
        | Path.Backward ->
            match SnapshotPointUtil.TrySubtractOne point with
            | Some point -> point, false
            | None -> SnapshotUtil.GetEndPoint point.Snapshot, true

    /// Get the point from which an incremental search should begin given
    /// a context point.  They don't begin at the point but rather before
    /// or after the point depending on the direction
    let GetSearchPoint path point = 
        let point, _ = GetSearchPointAndWrap path point
        point

    /// Vim is fairly odd in that it considers the top line of the file to be both line numbers
    /// 1 and 0.  The next line is 2.  The editor is a zero based index though so we need
    /// to take that into account
    let VimLineToTssLine line = 
        match line with
        | 0 -> 0
        | _ -> line - 1
