#light

namespace Vim
open Microsoft.VisualStudio.Text
open System

[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Method ||| AttributeTargets.Interface)>]
type internal UsedInBackgroundThread() =
    inherit Attribute()

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
        Vim.WeakReference<'T>(weakRef)

    /// Vim is fairly odd in that it considers the top line of the file to be both line numbers
    /// 1 and 0.  The next line is 2.  The editor is a zero based index though so we need
    /// to take that into account
    let VimLineToTssLine line = 
        match line with
        | 0 -> 0
        | _ -> line - 1

    let CountOrDefault count =
        match count with 
        | Some count -> count
        | None -> 1

