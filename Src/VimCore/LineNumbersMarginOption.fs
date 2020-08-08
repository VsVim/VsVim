namespace Vim

open Microsoft.VisualStudio.Text.Editor
open System.ComponentModel.Composition

module LineNumbersMarginOptions =

    [<Literal>]
    let LineNumbersMarginOptionName = "VsVimLineNumbersMarginOption"

    let LineNumbersMarginOptionId = new EditorOptionKey<bool>(LineNumbersMarginOptionName)

[<Export(typeof<EditorOptionDefinition>)>]
[<Sealed>]
type public VsVimLineNumbersMarginOption() =
    inherit EditorOptionDefinition<bool>()
    override x.Default = false
    override x.Key = LineNumbersMarginOptions.LineNumbersMarginOptionId
