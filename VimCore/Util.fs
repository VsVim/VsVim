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
    /// or after the point depending on the direction
    let GetSearchPoint kind point = 
        if SearchKindUtil.IsForward kind then 
            match SnapshotPointUtil.TryAddOne point with 
            | Some(point) -> point
            | None -> SnapshotPoint(point.Snapshot, 0)
        else 
            match SnapshotPointUtil.TrySubtractOne point with
            | Some(point) -> point
            | None -> SnapshotUtil.GetEndPoint point.Snapshot

