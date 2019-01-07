#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor
open System.ComponentModel.Composition

module LineNumbersMarginOptions =

    [<Literal>]
    let NumberOptionName = "VsVimNumbersOption" 

    [<Literal>]
    let RelativeNumberOptionName = "VsVimRelativeNumbersOption"

    [<Literal>]
    let LineNumbersMarginOptionName = "VsVimLineNumbersMarginOption"

    let NumberOptionId = new EditorOptionKey<bool>(NumberOptionName)
    let RelativeNumberOptionId = new EditorOptionKey<bool>(RelativeNumberOptionName)
    let LineNumbersMarginOptionId = new EditorOptionKey<bool>(LineNumbersMarginOptionName)

[<Export(typeof<EditorOptionDefinition>)>]
[<Sealed>]
type public VsVimLineNumbersMarginOption() =
  inherit EditorOptionDefinition<bool>()
  override x.Key 
    with get() = LineNumbersMarginOptions.LineNumbersMarginOptionId

[<Export(typeof<EditorOptionDefinition>)>]
[<Sealed>]
type public VsVimRelativeNumbersOption() =
  inherit EditorOptionDefinition<bool>()
  override x.Key 
    with get() = LineNumbersMarginOptions.RelativeNumberOptionId
 
[<Export(typeof<EditorOptionDefinition>)>]
[<Sealed>]
type public VsVimNumbersOption() =
  inherit EditorOptionDefinition<bool>()
  override x.Key 
    with get() = LineNumbersMarginOptions.NumberOptionId

