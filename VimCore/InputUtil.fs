#light

namespace Vim
open System.Windows.Input

module InputUtil = 
    let KeyInputList = [
            KeyInput('a',Key.A);
            KeyInput('b',Key.B);
            KeyInput('c',Key.C);
            KeyInput('d',Key.D);
            KeyInput('e',Key.E);
            KeyInput('f',Key.F);
            KeyInput('g',Key.G);
            KeyInput('h',Key.H);
            KeyInput('i',Key.I);
            KeyInput('j',Key.J);
            KeyInput('k',Key.K);
            KeyInput('l',Key.L);
            KeyInput('m',Key.M);
            KeyInput('n',Key.N);
            KeyInput('o',Key.O);
            KeyInput('p',Key.P);
            KeyInput('q',Key.Q);
            KeyInput('r',Key.R);
            KeyInput('s',Key.S);
            KeyInput('t',Key.T);
            KeyInput('u',Key.U);
            KeyInput('v',Key.V);
            KeyInput('w',Key.W);
            KeyInput('x',Key.X);
            KeyInput('y',Key.Y);
            KeyInput('z',Key.Z);
            KeyInput('A',Key.A,ModifierKeys.Shift);
            KeyInput('B',Key.B,ModifierKeys.Shift);
            KeyInput('C',Key.C,ModifierKeys.Shift);
            KeyInput('D',Key.D,ModifierKeys.Shift);
            KeyInput('E',Key.E,ModifierKeys.Shift);
            KeyInput('F',Key.F,ModifierKeys.Shift);
            KeyInput('G',Key.G,ModifierKeys.Shift);
            KeyInput('H',Key.H,ModifierKeys.Shift);
            KeyInput('I',Key.I,ModifierKeys.Shift);
            KeyInput('J',Key.J,ModifierKeys.Shift);
            KeyInput('K',Key.K,ModifierKeys.Shift);
            KeyInput('L',Key.L,ModifierKeys.Shift);
            KeyInput('M',Key.M,ModifierKeys.Shift);
            KeyInput('N',Key.N,ModifierKeys.Shift);
            KeyInput('O',Key.O,ModifierKeys.Shift);
            KeyInput('P',Key.P,ModifierKeys.Shift);
            KeyInput('Q',Key.Q,ModifierKeys.Shift);
            KeyInput('R',Key.R,ModifierKeys.Shift);
            KeyInput('S',Key.S,ModifierKeys.Shift);
            KeyInput('T',Key.T,ModifierKeys.Shift);
            KeyInput('U',Key.U,ModifierKeys.Shift);
            KeyInput('V',Key.V,ModifierKeys.Shift);
            KeyInput('W',Key.W,ModifierKeys.Shift);
            KeyInput('X',Key.X,ModifierKeys.Shift);
            KeyInput('Y',Key.Y,ModifierKeys.Shift);
            KeyInput('Z',Key.Z,ModifierKeys.Shift);
            KeyInput('0', Key.D0);
            KeyInput('1', Key.D1);
            KeyInput('2', Key.D2);
            KeyInput('3', Key.D3);
            KeyInput('4', Key.D4);
            KeyInput('5', Key.D5);
            KeyInput('6', Key.D6);
            KeyInput('7', Key.D7);
            KeyInput('8', Key.D8);
            KeyInput('9', Key.D9);
            KeyInput(')', Key.D0, ModifierKeys.Shift);
            KeyInput('!', Key.D1, ModifierKeys.Shift);
            KeyInput('@', Key.D2, ModifierKeys.Shift);
            KeyInput('#', Key.D3, ModifierKeys.Shift);
            KeyInput('$', Key.D4, ModifierKeys.Shift);
            KeyInput('%', Key.D5, ModifierKeys.Shift);
            KeyInput('^', Key.D6, ModifierKeys.Shift);
            KeyInput('&', Key.D7, ModifierKeys.Shift);
            KeyInput('*', Key.D8, ModifierKeys.Shift);
            KeyInput('(', Key.D9, ModifierKeys.Shift);
            KeyInput(',', Key.OemComma);
            KeyInput('<', Key.OemComma, ModifierKeys.Shift);
            KeyInput('.', Key.OemPeriod);
            KeyInput('>', Key.OemPeriod, ModifierKeys.Shift);
            KeyInput('[', Key.OemOpenBrackets);
            KeyInput(']', Key.OemCloseBrackets);
            KeyInput('{', Key.OemOpenBrackets, ModifierKeys.Shift);
            KeyInput('}', Key.OemCloseBrackets, ModifierKeys.Shift);
            KeyInput(' ', Key.Space);
            KeyInput('/', Key.Oem2);
            KeyInput('?', Key.Oem2, ModifierKeys.Shift);
            KeyInput('\r', Key.Return);
            KeyInput('\n', Key.LineFeed);
            KeyInput((char)27, Key.Escape);
            KeyInput(';', Key.OemSemicolon);
            KeyInput(':', Key.OemSemicolon, ModifierKeys.Shift);
            KeyInput('\\', Key.OemBackslash); 
            KeyInput(''', Key.OemQuotes);
            KeyInput('"', Key.OemQuotes, ModifierKeys.Shift);
            KeyInput('\b', Key.Back);
            KeyInput('\t', Key.Tab);
            KeyInput('-', Key.OemMinus);
            KeyInput('_', Key.OemMinus, ModifierKeys.Shift);
            KeyInput('+', Key.OemPlus, ModifierKeys.Shift);
            KeyInput('=', Key.OemPlus);]
            
    let FindKeyInput (k:Key) (m:ModifierKeys)= 
        let filter (e:KeyInput) = e.Key = k && e.ModifierKeys = m
        KeyInputList 
            |> List.filter filter
            |> List.tryPick (fun e -> Some e)
            
    let KeyInputToChar (ki:KeyInput) = ki.Char
    let KeyToKeyInput k = 
        match FindKeyInput k (ModifierKeys.None) with
        | Some ke -> KeyInput(ke.Char, ke.Key, ke.ModifierKeys)
        | None -> KeyInput((System.Char.MinValue),k, (ModifierKeys.None))
    let KeyToChar k = 
        let ki = KeyToKeyInput k
        ki.Char

    /// Try and convert the specified char to a predefined KeyInput structure.  Returns an
    /// empty value if no such KeyInput structure exists
    let TryCharToKeyInput c =
        match KeyInputList |> List.filter (fun e -> e.Char = c) with
        | h::_ -> Some h
        | _ -> None
                        
    let CharToKeyInput c = 
        match TryCharToKeyInput c with
        | Some ki -> ki
        | None ->
            // In some cases we don't have the correct Key enumeration available and 
            // have to rely on the char value to be correct
            KeyInput(c, Key.None)
    
    let KeyAndModifierToKeyInput k modifier =
        match FindKeyInput k modifier with
        | Some ke -> KeyInput(ke.Char, ke.Key, ke.ModifierKeys)
        | None -> 
            let c = KeyToChar k
            KeyInput(c, k, modifier)
            
    
