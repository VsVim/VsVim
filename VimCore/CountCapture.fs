#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor;
open System.Windows.Input

type internal CountResult =
    | Complete of int * KeyInput
    | NeedMore of (KeyInput -> CountResult)

module internal CountCapture =

    /// At least one digit is seen.  This will wait for the completion of the number
    /// as input by the user
    let rec private WaitForFinish (num:string) =
        let inner (ki:KeyInput) =
            match ki.IsDigit with
                | true ->
                    let digit = ki.Char.ToString()
                    let num = num + digit
                    WaitForFinish num
                | false ->
                    let realNum = System.Int32.Parse(num)
                    Complete(realNum, ki)
        NeedMore(inner)
        
    
    /// Process a count based on the input, if no count is given then 1 will be returned
    let Process (ki:KeyInput) = 
        match ki.IsDigit with
            | true -> 
                let digit = ki.Char.ToString()
                WaitForFinish digit
            | false -> Complete(1,ki)
        
