#light

namespace Vim

[<RequireQualifiedAccess>]
type internal EastAsianWidth = 
    | FullWidth
    | HalfWidth
    | Wide
    | Narrow
    | Ambiguous

    with

    static member OfText text = 
        match text with 
        | "F" -> EastAsianWidth.FullWidth
        | "H" -> EastAsianWidth.HalfWidth
        | "W" -> EastAsianWidth.Wide
        | "N" -> EastAsianWidth.Narrow
        | "Na" -> EastAsianWidth.Narrow
        | "A" -> EastAsianWidth.Ambiguous
        | _ -> invalidArg "text" "Not a valid value"

type internal UnicodeRangeEntry = {
    Start: int
    Last: int
    Width: EastAsianWidth
}

    with

    member x.Contains codePoint = codePoint >= x.Start && codePoint <= x.Last

    override x.ToString() = sprintf "%d -> %d %O" x.Start x.Last x.Width

type internal IntervalTreeNode
    (
        _entry: UnicodeRangeEntry,
        _left: IntervalTreeNode option,
        _right: IntervalTreeNode option
    ) =

    let _height = 1 + (max (IntervalTreeNode.GetHeight _left) (IntervalTreeNode.GetHeight _right))
    let _count = 1 + (IntervalTreeNode.GetCount _left) + (IntervalTreeNode.GetCount _right)

    new(entry: UnicodeRangeEntry) =
        IntervalTreeNode(entry, None, None)

    member x.Entry = _entry
    member x.Left = _left
    member x.Right = _right
    member x.Height = _height
    member x.Count = _count
    member x.BalanceFactor = 
        let leftHeight = IntervalTreeNode.GetHeight x.Left
        let rightHeight = IntervalTreeNode.GetHeight x.Right
        rightHeight - leftHeight

    member x.WithEntry entry = IntervalTreeNode(entry, x.Left, x.Right)
    member x.WithLeft (left: IntervalTreeNode) = IntervalTreeNode(x.Entry, Some left, x.Right)
    member x.WithLeft (left: IntervalTreeNode option) = IntervalTreeNode(x.Entry, left, x.Right)
    member x.WithRight (right: IntervalTreeNode) = IntervalTreeNode(x.Entry, x.Left, Some right)
    member x.WithRight (right: IntervalTreeNode option) = IntervalTreeNode(x.Entry, x.Left, right)

    static member private GetHeight (node: IntervalTreeNode option) = 
        match node with 
        | None -> 0
        | Some node -> node.Height

    static member private GetCount (node: IntervalTreeNode option) = 
        match node with 
        | None -> 0
        | Some node -> node.Count

/// A balanced internal tree for efficient lookup of unicode information
type internal IntervalTree
    (
        _root: IntervalTreeNode option
    ) =

    new() = 
        IntervalTree(None)

    member x.Root = _root
    member x.Height = match x.Root with Some node -> node.Height | None -> 0
    member x.Count = match x.Root with Some node -> node.Count | None -> 0

    member x.Insert entry = 
        let rotateLeft (node: IntervalTreeNode) (right: IntervalTreeNode) =
            let node = node.WithRight right.Left
            let right = right.WithLeft node
            right

        let rotateRight (node: IntervalTreeNode) (left: IntervalTreeNode) =
            let node = node.WithLeft left.Right
            let left = left.WithRight node
            left

        let balance (node: IntervalTreeNode) =
            let factor = node.BalanceFactor
            if factor > 1 then 
                let right = Option.get node.Right
                if right.BalanceFactor > 0 then 
                    rotateLeft node right
                else 
                    let right = rotateRight right (Option.get right.Left)
                    rotateLeft node right
            elif factor < -1 then
                let left = Option.get node.Left
                if left.BalanceFactor < 0 then
                    rotateRight node left
                else
                    let left = rotateLeft left (Option.get left.Right)
                    rotateRight node left
            else
                node

        let rec insertCore (node: IntervalTreeNode option) entry = 
            match node with
            | None -> IntervalTreeNode(entry)
            | Some node -> 
                let current = node.Entry
                if 
                    (entry.Start = current.Last || entry.Last = current.Start) &&
                    entry.Width = current.Width
                then
                    let current = { current with Start = min current.Start entry.Start; Last = max current.Last entry.Last }
                    node.WithEntry current
                else
                    let node = 
                        if entry.Start < current.Start then node.WithLeft (insertCore node.Left entry)
                        else node.WithRight (insertCore node.Right entry)
                    balance node

        let root = insertCore _root entry
        IntervalTree(Some root)

    member x.Find codePoint = 
        let rec findCore (node: IntervalTreeNode option) codePoint = 
            match node with 
            | None -> None
            | Some node -> 
                 if node.Entry.Contains codePoint then Some node.Entry
                 elif codePoint < node.Entry.Start then findCore node.Left codePoint
                 else findCore node.Right codePoint
        findCore _root codePoint

    static member Empty = IntervalTree(None)

module UnicodeUtil =

    /// All of the known categories. This is taken from the following table 
    /// https://www.unicode.org/Public/UCD/latest/ucd/EastAsianWidth.txt
    /// EastAsianWidth-11.0.0.txt
    ///
    /// This is deliberately done as a function to prevent the array from being persisted 
    /// throughout the execution of the process. The other data structures created from this
    /// hold the necessary data.
    let CreateUnicodeRangeEntries() = 
        [|
            { Start = 0x0000; Last = 0x001F; Width = EastAsianWidth.OfText "N"  } // 0000..001F;N     # Cc    [32] <control-0000>..<control-001F>
            { Start = 0x0020; Last = 0x0020; Width = EastAsianWidth.OfText "Na" } // 0020;Na          # Zs         SPACE
            { Start = 0x0021; Last = 0x0023; Width = EastAsianWidth.OfText "Na" } // 0021..0023;Na    # Po     [3] EXCLAMATION MARK..NUMBER SIGN
            { Start = 0x0024; Last = 0x0024; Width = EastAsianWidth.OfText "Na" } // 0024;Na          # Sc         DOLLAR SIGN
            { Start = 0x0025; Last = 0x0027; Width = EastAsianWidth.OfText "Na" } // 0025..0027;Na    # Po     [3] PERCENT SIGN..APOSTROPHE
            { Start = 0x0028; Last = 0x0028; Width = EastAsianWidth.OfText "Na" } // 0028;Na          # Ps         LEFT PARENTHESIS
            { Start = 0x0029; Last = 0x0029; Width = EastAsianWidth.OfText "Na" } // 0029;Na          # Pe         RIGHT PARENTHESIS
            { Start = 0x002A; Last = 0x002A; Width = EastAsianWidth.OfText "Na" } // 002A;Na          # Po         ASTERISK
            { Start = 0x002B; Last = 0x002B; Width = EastAsianWidth.OfText "Na" } // 002B;Na          # Sm         PLUS SIGN
            { Start = 0x002C; Last = 0x002C; Width = EastAsianWidth.OfText "Na" } // 002C;Na          # Po         COMMA
            { Start = 0x002D; Last = 0x002D; Width = EastAsianWidth.OfText "Na" } // 002D;Na          # Pd         HYPHEN-MINUS
            { Start = 0x002E; Last = 0x002F; Width = EastAsianWidth.OfText "Na" } // 002E..002F;Na    # Po     [2] FULL STOP..SOLIDUS
            { Start = 0x0030; Last = 0x0039; Width = EastAsianWidth.OfText "Na" } // 0030..0039;Na    # Nd    [10] DIGIT ZERO..DIGIT NINE
            { Start = 0x003A; Last = 0x003B; Width = EastAsianWidth.OfText "Na" } // 003A..003B;Na    # Po     [2] COLON..SEMICOLON
            { Start = 0x003C; Last = 0x003E; Width = EastAsianWidth.OfText "Na" } // 003C..003E;Na    # Sm     [3] LESS-THAN SIGN..GREATER-THAN SIGN
            { Start = 0x003F; Last = 0x0040; Width = EastAsianWidth.OfText "Na" } // 003F..0040;Na    # Po     [2] QUESTION MARK..COMMERCIAL AT
            { Start = 0x0041; Last = 0x005A; Width = EastAsianWidth.OfText "Na" } // 0041..005A;Na    # Lu    [26] LATIN CAPITAL LETTER A..LATIN CAPITAL LETTER Z
            { Start = 0x005B; Last = 0x005B; Width = EastAsianWidth.OfText "Na" } // 005B;Na          # Ps         LEFT SQUARE BRACKET
            { Start = 0x005C; Last = 0x005C; Width = EastAsianWidth.OfText "Na" } // 005C;Na          # Po         REVERSE SOLIDUS
            { Start = 0x005D; Last = 0x005D; Width = EastAsianWidth.OfText "Na" } // 005D;Na          # Pe         RIGHT SQUARE BRACKET
            { Start = 0x005E; Last = 0x005E; Width = EastAsianWidth.OfText "Na" } // 005E;Na          # Sk         CIRCUMFLEX ACCENT
            { Start = 0x005F; Last = 0x005F; Width = EastAsianWidth.OfText "Na" } // 005F;Na          # Pc         LOW LINE
            { Start = 0x0060; Last = 0x0060; Width = EastAsianWidth.OfText "Na" } // 0060;Na          # Sk         GRAVE ACCENT
            { Start = 0x0061; Last = 0x007A; Width = EastAsianWidth.OfText "Na" } // 0061..007A;Na    # Ll    [26] LATIN SMALL LETTER A..LATIN SMALL LETTER Z
            { Start = 0x007B; Last = 0x007B; Width = EastAsianWidth.OfText "Na" } // 007B;Na          # Ps         LEFT CURLY BRACKET
            { Start = 0x007C; Last = 0x007C; Width = EastAsianWidth.OfText "Na" } // 007C;Na          # Sm         VERTICAL LINE
            { Start = 0x007D; Last = 0x007D; Width = EastAsianWidth.OfText "Na" } // 007D;Na          # Pe         RIGHT CURLY BRACKET
            { Start = 0x007E; Last = 0x007E; Width = EastAsianWidth.OfText "Na" } // 007E;Na          # Sm         TILDE
            { Start = 0x007F; Last = 0x007F; Width = EastAsianWidth.OfText "N"  } // 007F;N           # Cc         <control-007F>
            { Start = 0x0080; Last = 0x009F; Width = EastAsianWidth.OfText "N"  } // 0080..009F;N     # Cc    [32] <control-0080>..<control-009F>
            { Start = 0x00A0; Last = 0x00A0; Width = EastAsianWidth.OfText "N"  } // 00A0;N           # Zs         NO-BREAK SPACE
            { Start = 0x00A1; Last = 0x00A1; Width = EastAsianWidth.OfText "A"  } // 00A1;A           # Po         INVERTED EXCLAMATION MARK
            { Start = 0x00A2; Last = 0x00A3; Width = EastAsianWidth.OfText "Na" } // 00A2..00A3;Na    # Sc     [2] CENT SIGN..POUND SIGN
            { Start = 0x00A4; Last = 0x00A4; Width = EastAsianWidth.OfText "A"  } // 00A4;A           # Sc         CURRENCY SIGN
            { Start = 0x00A5; Last = 0x00A5; Width = EastAsianWidth.OfText "Na" } // 00A5;Na          # Sc         YEN SIGN
            { Start = 0x00A6; Last = 0x00A6; Width = EastAsianWidth.OfText "Na" } // 00A6;Na          # So         BROKEN BAR
            { Start = 0x00A7; Last = 0x00A7; Width = EastAsianWidth.OfText "A"  } // 00A7;A           # Po         SECTION SIGN
            { Start = 0x00A8; Last = 0x00A8; Width = EastAsianWidth.OfText "A"  } // 00A8;A           # Sk         DIAERESIS
            { Start = 0x00A9; Last = 0x00A9; Width = EastAsianWidth.OfText "N"  } // 00A9;N           # So         COPYRIGHT SIGN
            { Start = 0x00AA; Last = 0x00AA; Width = EastAsianWidth.OfText "A"  } // 00AA;A           # Lo         FEMININE ORDINAL INDICATOR
            { Start = 0x00AB; Last = 0x00AB; Width = EastAsianWidth.OfText "N"  } // 00AB;N           # Pi         LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
            { Start = 0x00AC; Last = 0x00AC; Width = EastAsianWidth.OfText "Na" } // 00AC;Na          # Sm         NOT SIGN
            { Start = 0x00AD; Last = 0x00AD; Width = EastAsianWidth.OfText "A"  } // 00AD;A           # Cf         SOFT HYPHEN
            { Start = 0x00AE; Last = 0x00AE; Width = EastAsianWidth.OfText "A"  } // 00AE;A           # So         REGISTERED SIGN
            { Start = 0x00AF; Last = 0x00AF; Width = EastAsianWidth.OfText "Na" } // 00AF;Na          # Sk         MACRON
            { Start = 0x00B0; Last = 0x00B0; Width = EastAsianWidth.OfText "A"  } // 00B0;A           # So         DEGREE SIGN
            { Start = 0x00B1; Last = 0x00B1; Width = EastAsianWidth.OfText "A"  } // 00B1;A           # Sm         PLUS-MINUS SIGN
            { Start = 0x00B2; Last = 0x00B3; Width = EastAsianWidth.OfText "A"  } // 00B2..00B3;A     # No     [2] SUPERSCRIPT TWO..SUPERSCRIPT THREE
            { Start = 0x00B4; Last = 0x00B4; Width = EastAsianWidth.OfText "A"  } // 00B4;A           # Sk         ACUTE ACCENT
            { Start = 0x00B5; Last = 0x00B5; Width = EastAsianWidth.OfText "N"  } // 00B5;N           # Ll         MICRO SIGN
            { Start = 0x00B6; Last = 0x00B7; Width = EastAsianWidth.OfText "A"  } // 00B6..00B7;A     # Po     [2] PILCROW SIGN..MIDDLE DOT
            { Start = 0x00B8; Last = 0x00B8; Width = EastAsianWidth.OfText "A"  } // 00B8;A           # Sk         CEDILLA
            { Start = 0x00B9; Last = 0x00B9; Width = EastAsianWidth.OfText "A"  } // 00B9;A           # No         SUPERSCRIPT ONE
            { Start = 0x00BA; Last = 0x00BA; Width = EastAsianWidth.OfText "A"  } // 00BA;A           # Lo         MASCULINE ORDINAL INDICATOR
            { Start = 0x00BB; Last = 0x00BB; Width = EastAsianWidth.OfText "N"  } // 00BB;N           # Pf         RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK
            { Start = 0x00BC; Last = 0x00BE; Width = EastAsianWidth.OfText "A"  } // 00BC..00BE;A     # No     [3] VULGAR FRACTION ONE QUARTER..VULGAR FRACTION THREE QUARTERS
            { Start = 0x00BF; Last = 0x00BF; Width = EastAsianWidth.OfText "A"  } // 00BF;A           # Po         INVERTED QUESTION MARK
            { Start = 0x00C0; Last = 0x00C5; Width = EastAsianWidth.OfText "N"  } // 00C0..00C5;N     # Lu     [6] LATIN CAPITAL LETTER A WITH GRAVE..LATIN CAPITAL LETTER A WITH RING ABOVE
            { Start = 0x00C6; Last = 0x00C6; Width = EastAsianWidth.OfText "A"  } // 00C6;A           # Lu         LATIN CAPITAL LETTER AE
            { Start = 0x00C7; Last = 0x00CF; Width = EastAsianWidth.OfText "N"  } // 00C7..00CF;N     # Lu     [9] LATIN CAPITAL LETTER C WITH CEDILLA..LATIN CAPITAL LETTER I WITH DIAERESIS
            { Start = 0x00D0; Last = 0x00D0; Width = EastAsianWidth.OfText "A"  } // 00D0;A           # Lu         LATIN CAPITAL LETTER ETH
            { Start = 0x00D1; Last = 0x00D6; Width = EastAsianWidth.OfText "N"  } // 00D1..00D6;N     # Lu     [6] LATIN CAPITAL LETTER N WITH TILDE..LATIN CAPITAL LETTER O WITH DIAERESIS
            { Start = 0x00D7; Last = 0x00D7; Width = EastAsianWidth.OfText "A"  } // 00D7;A           # Sm         MULTIPLICATION SIGN
            { Start = 0x00D8; Last = 0x00D8; Width = EastAsianWidth.OfText "A"  } // 00D8;A           # Lu         LATIN CAPITAL LETTER O WITH STROKE
            { Start = 0x00D9; Last = 0x00DD; Width = EastAsianWidth.OfText "N"  } // 00D9..00DD;N     # Lu     [5] LATIN CAPITAL LETTER U WITH GRAVE..LATIN CAPITAL LETTER Y WITH ACUTE
            { Start = 0x00DE; Last = 0x00E1; Width = EastAsianWidth.OfText "A"  } // 00DE..00E1;A     # L&     [4] LATIN CAPITAL LETTER THORN..LATIN SMALL LETTER A WITH ACUTE
            { Start = 0x00E2; Last = 0x00E5; Width = EastAsianWidth.OfText "N"  } // 00E2..00E5;N     # Ll     [4] LATIN SMALL LETTER A WITH CIRCUMFLEX..LATIN SMALL LETTER A WITH RING ABOVE
            { Start = 0x00E6; Last = 0x00E6; Width = EastAsianWidth.OfText "A"  } // 00E6;A           # Ll         LATIN SMALL LETTER AE
            { Start = 0x00E7; Last = 0x00E7; Width = EastAsianWidth.OfText "N"  } // 00E7;N           # Ll         LATIN SMALL LETTER C WITH CEDILLA
            { Start = 0x00E8; Last = 0x00EA; Width = EastAsianWidth.OfText "A"  } // 00E8..00EA;A     # Ll     [3] LATIN SMALL LETTER E WITH GRAVE..LATIN SMALL LETTER E WITH CIRCUMFLEX
            { Start = 0x00EB; Last = 0x00EB; Width = EastAsianWidth.OfText "N"  } // 00EB;N           # Ll         LATIN SMALL LETTER E WITH DIAERESIS
            { Start = 0x00EC; Last = 0x00ED; Width = EastAsianWidth.OfText "A"  } // 00EC..00ED;A     # Ll     [2] LATIN SMALL LETTER I WITH GRAVE..LATIN SMALL LETTER I WITH ACUTE
            { Start = 0x00EE; Last = 0x00EF; Width = EastAsianWidth.OfText "N"  } // 00EE..00EF;N     # Ll     [2] LATIN SMALL LETTER I WITH CIRCUMFLEX..LATIN SMALL LETTER I WITH DIAERESIS
            { Start = 0x00F0; Last = 0x00F0; Width = EastAsianWidth.OfText "A"  } // 00F0;A           # Ll         LATIN SMALL LETTER ETH
            { Start = 0x00F1; Last = 0x00F1; Width = EastAsianWidth.OfText "N"  } // 00F1;N           # Ll         LATIN SMALL LETTER N WITH TILDE
            { Start = 0x00F2; Last = 0x00F3; Width = EastAsianWidth.OfText "A"  } // 00F2..00F3;A     # Ll     [2] LATIN SMALL LETTER O WITH GRAVE..LATIN SMALL LETTER O WITH ACUTE
            { Start = 0x00F4; Last = 0x00F6; Width = EastAsianWidth.OfText "N"  } // 00F4..00F6;N     # Ll     [3] LATIN SMALL LETTER O WITH CIRCUMFLEX..LATIN SMALL LETTER O WITH DIAERESIS
            { Start = 0x00F7; Last = 0x00F7; Width = EastAsianWidth.OfText "A"  } // 00F7;A           # Sm         DIVISION SIGN
            { Start = 0x00F8; Last = 0x00FA; Width = EastAsianWidth.OfText "A"  } // 00F8..00FA;A     # Ll     [3] LATIN SMALL LETTER O WITH STROKE..LATIN SMALL LETTER U WITH ACUTE
            { Start = 0x00FB; Last = 0x00FB; Width = EastAsianWidth.OfText "N"  } // 00FB;N           # Ll         LATIN SMALL LETTER U WITH CIRCUMFLEX
            { Start = 0x00FC; Last = 0x00FC; Width = EastAsianWidth.OfText "A"  } // 00FC;A           # Ll         LATIN SMALL LETTER U WITH DIAERESIS
            { Start = 0x00FD; Last = 0x00FD; Width = EastAsianWidth.OfText "N"  } // 00FD;N           # Ll         LATIN SMALL LETTER Y WITH ACUTE
            { Start = 0x00FE; Last = 0x00FE; Width = EastAsianWidth.OfText "A"  } // 00FE;A           # Ll         LATIN SMALL LETTER THORN
            { Start = 0x00FF; Last = 0x00FF; Width = EastAsianWidth.OfText "N"  } // 00FF;N           # Ll         LATIN SMALL LETTER Y WITH DIAERESIS
            { Start = 0x0100; Last = 0x0100; Width = EastAsianWidth.OfText "N"  } // 0100;N           # Lu         LATIN CAPITAL LETTER A WITH MACRON
            { Start = 0x0101; Last = 0x0101; Width = EastAsianWidth.OfText "A"  } // 0101;A           # Ll         LATIN SMALL LETTER A WITH MACRON
            { Start = 0x0102; Last = 0x0110; Width = EastAsianWidth.OfText "N"  } // 0102..0110;N     # L&    [15] LATIN CAPITAL LETTER A WITH BREVE..LATIN CAPITAL LETTER D WITH STROKE
            { Start = 0x0111; Last = 0x0111; Width = EastAsianWidth.OfText "A"  } // 0111;A           # Ll         LATIN SMALL LETTER D WITH STROKE
            { Start = 0x0112; Last = 0x0112; Width = EastAsianWidth.OfText "N"  } // 0112;N           # Lu         LATIN CAPITAL LETTER E WITH MACRON
            { Start = 0x0113; Last = 0x0113; Width = EastAsianWidth.OfText "A"  } // 0113;A           # Ll         LATIN SMALL LETTER E WITH MACRON
            { Start = 0x0114; Last = 0x011A; Width = EastAsianWidth.OfText "N"  } // 0114..011A;N     # L&     [7] LATIN CAPITAL LETTER E WITH BREVE..LATIN CAPITAL LETTER E WITH CARON
            { Start = 0x011B; Last = 0x011B; Width = EastAsianWidth.OfText "A"  } // 011B;A           # Ll         LATIN SMALL LETTER E WITH CARON
            { Start = 0x011C; Last = 0x0125; Width = EastAsianWidth.OfText "N"  } // 011C..0125;N     # L&    [10] LATIN CAPITAL LETTER G WITH CIRCUMFLEX..LATIN SMALL LETTER H WITH CIRCUMFLEX
            { Start = 0x0126; Last = 0x0127; Width = EastAsianWidth.OfText "A"  } // 0126..0127;A     # L&     [2] LATIN CAPITAL LETTER H WITH STROKE..LATIN SMALL LETTER H WITH STROKE
            { Start = 0x0128; Last = 0x012A; Width = EastAsianWidth.OfText "N"  } // 0128..012A;N     # L&     [3] LATIN CAPITAL LETTER I WITH TILDE..LATIN CAPITAL LETTER I WITH MACRON
            { Start = 0x012B; Last = 0x012B; Width = EastAsianWidth.OfText "A"  } // 012B;A           # Ll         LATIN SMALL LETTER I WITH MACRON
            { Start = 0x012C; Last = 0x0130; Width = EastAsianWidth.OfText "N"  } // 012C..0130;N     # L&     [5] LATIN CAPITAL LETTER I WITH BREVE..LATIN CAPITAL LETTER I WITH DOT ABOVE
            { Start = 0x0131; Last = 0x0133; Width = EastAsianWidth.OfText "A"  } // 0131..0133;A     # L&     [3] LATIN SMALL LETTER DOTLESS I..LATIN SMALL LIGATURE IJ
            { Start = 0x0134; Last = 0x0137; Width = EastAsianWidth.OfText "N"  } // 0134..0137;N     # L&     [4] LATIN CAPITAL LETTER J WITH CIRCUMFLEX..LATIN SMALL LETTER K WITH CEDILLA
            { Start = 0x0138; Last = 0x0138; Width = EastAsianWidth.OfText "A"  } // 0138;A           # Ll         LATIN SMALL LETTER KRA
            { Start = 0x0139; Last = 0x013E; Width = EastAsianWidth.OfText "N"  } // 0139..013E;N     # L&     [6] LATIN CAPITAL LETTER L WITH ACUTE..LATIN SMALL LETTER L WITH CARON
            { Start = 0x013F; Last = 0x0142; Width = EastAsianWidth.OfText "A"  } // 013F..0142;A     # L&     [4] LATIN CAPITAL LETTER L WITH MIDDLE DOT..LATIN SMALL LETTER L WITH STROKE
            { Start = 0x0143; Last = 0x0143; Width = EastAsianWidth.OfText "N"  } // 0143;N           # Lu         LATIN CAPITAL LETTER N WITH ACUTE
            { Start = 0x0144; Last = 0x0144; Width = EastAsianWidth.OfText "A"  } // 0144;A           # Ll         LATIN SMALL LETTER N WITH ACUTE
            { Start = 0x0145; Last = 0x0147; Width = EastAsianWidth.OfText "N"  } // 0145..0147;N     # L&     [3] LATIN CAPITAL LETTER N WITH CEDILLA..LATIN CAPITAL LETTER N WITH CARON
            { Start = 0x0148; Last = 0x014B; Width = EastAsianWidth.OfText "A"  } // 0148..014B;A     # L&     [4] LATIN SMALL LETTER N WITH CARON..LATIN SMALL LETTER ENG
            { Start = 0x014C; Last = 0x014C; Width = EastAsianWidth.OfText "N"  } // 014C;N           # Lu         LATIN CAPITAL LETTER O WITH MACRON
            { Start = 0x014D; Last = 0x014D; Width = EastAsianWidth.OfText "A"  } // 014D;A           # Ll         LATIN SMALL LETTER O WITH MACRON
            { Start = 0x014E; Last = 0x0151; Width = EastAsianWidth.OfText "N"  } // 014E..0151;N     # L&     [4] LATIN CAPITAL LETTER O WITH BREVE..LATIN SMALL LETTER O WITH DOUBLE ACUTE
            { Start = 0x0152; Last = 0x0153; Width = EastAsianWidth.OfText "A"  } // 0152..0153;A     # L&     [2] LATIN CAPITAL LIGATURE OE..LATIN SMALL LIGATURE OE
            { Start = 0x0154; Last = 0x0165; Width = EastAsianWidth.OfText "N"  } // 0154..0165;N     # L&    [18] LATIN CAPITAL LETTER R WITH ACUTE..LATIN SMALL LETTER T WITH CARON
            { Start = 0x0166; Last = 0x0167; Width = EastAsianWidth.OfText "A"  } // 0166..0167;A     # L&     [2] LATIN CAPITAL LETTER T WITH STROKE..LATIN SMALL LETTER T WITH STROKE
            { Start = 0x0168; Last = 0x016A; Width = EastAsianWidth.OfText "N"  } // 0168..016A;N     # L&     [3] LATIN CAPITAL LETTER U WITH TILDE..LATIN CAPITAL LETTER U WITH MACRON
            { Start = 0x016B; Last = 0x016B; Width = EastAsianWidth.OfText "A"  } // 016B;A           # Ll         LATIN SMALL LETTER U WITH MACRON
            { Start = 0x016C; Last = 0x017F; Width = EastAsianWidth.OfText "N"  } // 016C..017F;N     # L&    [20] LATIN CAPITAL LETTER U WITH BREVE..LATIN SMALL LETTER LONG S
            { Start = 0x0180; Last = 0x01BA; Width = EastAsianWidth.OfText "N"  } // 0180..01BA;N     # L&    [59] LATIN SMALL LETTER B WITH STROKE..LATIN SMALL LETTER EZH WITH TAIL
            { Start = 0x01BB; Last = 0x01BB; Width = EastAsianWidth.OfText "N"  } // 01BB;N           # Lo         LATIN LETTER TWO WITH STROKE
            { Start = 0x01BC; Last = 0x01BF; Width = EastAsianWidth.OfText "N"  } // 01BC..01BF;N     # L&     [4] LATIN CAPITAL LETTER TONE FIVE..LATIN LETTER WYNN
            { Start = 0x01C0; Last = 0x01C3; Width = EastAsianWidth.OfText "N"  } // 01C0..01C3;N     # Lo     [4] LATIN LETTER DENTAL CLICK..LATIN LETTER RETROFLEX CLICK
            { Start = 0x01C4; Last = 0x01CD; Width = EastAsianWidth.OfText "N"  } // 01C4..01CD;N     # L&    [10] LATIN CAPITAL LETTER DZ WITH CARON..LATIN CAPITAL LETTER A WITH CARON
            { Start = 0x01CE; Last = 0x01CE; Width = EastAsianWidth.OfText "A"  } // 01CE;A           # Ll         LATIN SMALL LETTER A WITH CARON
            { Start = 0x01CF; Last = 0x01CF; Width = EastAsianWidth.OfText "N"  } // 01CF;N           # Lu         LATIN CAPITAL LETTER I WITH CARON
            { Start = 0x01D0; Last = 0x01D0; Width = EastAsianWidth.OfText "A"  } // 01D0;A           # Ll         LATIN SMALL LETTER I WITH CARON
            { Start = 0x01D1; Last = 0x01D1; Width = EastAsianWidth.OfText "N"  } // 01D1;N           # Lu         LATIN CAPITAL LETTER O WITH CARON
            { Start = 0x01D2; Last = 0x01D2; Width = EastAsianWidth.OfText "A"  } // 01D2;A           # Ll         LATIN SMALL LETTER O WITH CARON
            { Start = 0x01D3; Last = 0x01D3; Width = EastAsianWidth.OfText "N"  } // 01D3;N           # Lu         LATIN CAPITAL LETTER U WITH CARON
            { Start = 0x01D4; Last = 0x01D4; Width = EastAsianWidth.OfText "A"  } // 01D4;A           # Ll         LATIN SMALL LETTER U WITH CARON
            { Start = 0x01D5; Last = 0x01D5; Width = EastAsianWidth.OfText "N"  } // 01D5;N           # Lu         LATIN CAPITAL LETTER U WITH DIAERESIS AND MACRON
            { Start = 0x01D6; Last = 0x01D6; Width = EastAsianWidth.OfText "A"  } // 01D6;A           # Ll         LATIN SMALL LETTER U WITH DIAERESIS AND MACRON
            { Start = 0x01D7; Last = 0x01D7; Width = EastAsianWidth.OfText "N"  } // 01D7;N           # Lu         LATIN CAPITAL LETTER U WITH DIAERESIS AND ACUTE
            { Start = 0x01D8; Last = 0x01D8; Width = EastAsianWidth.OfText "A"  } // 01D8;A           # Ll         LATIN SMALL LETTER U WITH DIAERESIS AND ACUTE
            { Start = 0x01D9; Last = 0x01D9; Width = EastAsianWidth.OfText "N"  } // 01D9;N           # Lu         LATIN CAPITAL LETTER U WITH DIAERESIS AND CARON
            { Start = 0x01DA; Last = 0x01DA; Width = EastAsianWidth.OfText "A"  } // 01DA;A           # Ll         LATIN SMALL LETTER U WITH DIAERESIS AND CARON
            { Start = 0x01DB; Last = 0x01DB; Width = EastAsianWidth.OfText "N"  } // 01DB;N           # Lu         LATIN CAPITAL LETTER U WITH DIAERESIS AND GRAVE
            { Start = 0x01DC; Last = 0x01DC; Width = EastAsianWidth.OfText "A"  } // 01DC;A           # Ll         LATIN SMALL LETTER U WITH DIAERESIS AND GRAVE
            { Start = 0x01DD; Last = 0x024F; Width = EastAsianWidth.OfText "N"  } // 01DD..024F;N     # L&   [115] LATIN SMALL LETTER TURNED E..LATIN SMALL LETTER Y WITH STROKE
            { Start = 0x0250; Last = 0x0250; Width = EastAsianWidth.OfText "N"  } // 0250;N           # Ll         LATIN SMALL LETTER TURNED A
            { Start = 0x0251; Last = 0x0251; Width = EastAsianWidth.OfText "A"  } // 0251;A           # Ll         LATIN SMALL LETTER ALPHA
            { Start = 0x0252; Last = 0x0260; Width = EastAsianWidth.OfText "N"  } // 0252..0260;N     # Ll    [15] LATIN SMALL LETTER TURNED ALPHA..LATIN SMALL LETTER G WITH HOOK
            { Start = 0x0261; Last = 0x0261; Width = EastAsianWidth.OfText "A"  } // 0261;A           # Ll         LATIN SMALL LETTER SCRIPT G
            { Start = 0x0262; Last = 0x0293; Width = EastAsianWidth.OfText "N"  } // 0262..0293;N     # Ll    [50] LATIN LETTER SMALL CAPITAL G..LATIN SMALL LETTER EZH WITH CURL
            { Start = 0x0294; Last = 0x0294; Width = EastAsianWidth.OfText "N"  } // 0294;N           # Lo         LATIN LETTER GLOTTAL STOP
            { Start = 0x0295; Last = 0x02AF; Width = EastAsianWidth.OfText "N"  } // 0295..02AF;N     # Ll    [27] LATIN LETTER PHARYNGEAL VOICED FRICATIVE..LATIN SMALL LETTER TURNED H WITH FISHHOOK AND TAIL
            { Start = 0x02B0; Last = 0x02C1; Width = EastAsianWidth.OfText "N"  } // 02B0..02C1;N     # Lm    [18] MODIFIER LETTER SMALL H..MODIFIER LETTER REVERSED GLOTTAL STOP
            { Start = 0x02C2; Last = 0x02C3; Width = EastAsianWidth.OfText "N"  } // 02C2..02C3;N     # Sk     [2] MODIFIER LETTER LEFT ARROWHEAD..MODIFIER LETTER RIGHT ARROWHEAD
            { Start = 0x02C4; Last = 0x02C4; Width = EastAsianWidth.OfText "A"  } // 02C4;A           # Sk         MODIFIER LETTER UP ARROWHEAD
            { Start = 0x02C5; Last = 0x02C5; Width = EastAsianWidth.OfText "N"  } // 02C5;N           # Sk         MODIFIER LETTER DOWN ARROWHEAD
            { Start = 0x02C6; Last = 0x02C6; Width = EastAsianWidth.OfText "N"  } // 02C6;N           # Lm         MODIFIER LETTER CIRCUMFLEX ACCENT
            { Start = 0x02C7; Last = 0x02C7; Width = EastAsianWidth.OfText "A"  } // 02C7;A           # Lm         CARON
            { Start = 0x02C8; Last = 0x02C8; Width = EastAsianWidth.OfText "N"  } // 02C8;N           # Lm         MODIFIER LETTER VERTICAL LINE
            { Start = 0x02C9; Last = 0x02CB; Width = EastAsianWidth.OfText "A"  } // 02C9..02CB;A     # Lm     [3] MODIFIER LETTER MACRON..MODIFIER LETTER GRAVE ACCENT
            { Start = 0x02CC; Last = 0x02CC; Width = EastAsianWidth.OfText "N"  } // 02CC;N           # Lm         MODIFIER LETTER LOW VERTICAL LINE
            { Start = 0x02CD; Last = 0x02CD; Width = EastAsianWidth.OfText "A"  } // 02CD;A           # Lm         MODIFIER LETTER LOW MACRON
            { Start = 0x02CE; Last = 0x02CF; Width = EastAsianWidth.OfText "N"  } // 02CE..02CF;N     # Lm     [2] MODIFIER LETTER LOW GRAVE ACCENT..MODIFIER LETTER LOW ACUTE ACCENT
            { Start = 0x02D0; Last = 0x02D0; Width = EastAsianWidth.OfText "A"  } // 02D0;A           # Lm         MODIFIER LETTER TRIANGULAR COLON
            { Start = 0x02D1; Last = 0x02D1; Width = EastAsianWidth.OfText "N"  } // 02D1;N           # Lm         MODIFIER LETTER HALF TRIANGULAR COLON
            { Start = 0x02D2; Last = 0x02D7; Width = EastAsianWidth.OfText "N"  } // 02D2..02D7;N     # Sk     [6] MODIFIER LETTER CENTRED RIGHT HALF RING..MODIFIER LETTER MINUS SIGN
            { Start = 0x02D8; Last = 0x02DB; Width = EastAsianWidth.OfText "A"  } // 02D8..02DB;A     # Sk     [4] BREVE..OGONEK
            { Start = 0x02DC; Last = 0x02DC; Width = EastAsianWidth.OfText "N"  } // 02DC;N           # Sk         SMALL TILDE
            { Start = 0x02DD; Last = 0x02DD; Width = EastAsianWidth.OfText "A"  } // 02DD;A           # Sk         DOUBLE ACUTE ACCENT
            { Start = 0x02DE; Last = 0x02DE; Width = EastAsianWidth.OfText "N"  } // 02DE;N           # Sk         MODIFIER LETTER RHOTIC HOOK
            { Start = 0x02DF; Last = 0x02DF; Width = EastAsianWidth.OfText "A"  } // 02DF;A           # Sk         MODIFIER LETTER CROSS ACCENT
            { Start = 0x02E0; Last = 0x02E4; Width = EastAsianWidth.OfText "N"  } // 02E0..02E4;N     # Lm     [5] MODIFIER LETTER SMALL GAMMA..MODIFIER LETTER SMALL REVERSED GLOTTAL STOP
            { Start = 0x02E5; Last = 0x02EB; Width = EastAsianWidth.OfText "N"  } // 02E5..02EB;N     # Sk     [7] MODIFIER LETTER EXTRA-HIGH TONE BAR..MODIFIER LETTER YANG DEPARTING TONE MARK
            { Start = 0x02EC; Last = 0x02EC; Width = EastAsianWidth.OfText "N"  } // 02EC;N           # Lm         MODIFIER LETTER VOICING
            { Start = 0x02ED; Last = 0x02ED; Width = EastAsianWidth.OfText "N"  } // 02ED;N           # Sk         MODIFIER LETTER UNASPIRATED
            { Start = 0x02EE; Last = 0x02EE; Width = EastAsianWidth.OfText "N"  } // 02EE;N           # Lm         MODIFIER LETTER DOUBLE APOSTROPHE
            { Start = 0x02EF; Last = 0x02FF; Width = EastAsianWidth.OfText "N"  } // 02EF..02FF;N     # Sk    [17] MODIFIER LETTER LOW DOWN ARROWHEAD..MODIFIER LETTER LOW LEFT ARROW
            { Start = 0x0300; Last = 0x036F; Width = EastAsianWidth.OfText "A"  } // 0300..036F;A     # Mn   [112] COMBINING GRAVE ACCENT..COMBINING LATIN SMALL LETTER X
            { Start = 0x0370; Last = 0x0373; Width = EastAsianWidth.OfText "N"  } // 0370..0373;N     # L&     [4] GREEK CAPITAL LETTER HETA..GREEK SMALL LETTER ARCHAIC SAMPI
            { Start = 0x0374; Last = 0x0374; Width = EastAsianWidth.OfText "N"  } // 0374;N           # Lm         GREEK NUMERAL SIGN
            { Start = 0x0375; Last = 0x0375; Width = EastAsianWidth.OfText "N"  } // 0375;N           # Sk         GREEK LOWER NUMERAL SIGN
            { Start = 0x0376; Last = 0x0377; Width = EastAsianWidth.OfText "N"  } // 0376..0377;N     # L&     [2] GREEK CAPITAL LETTER PAMPHYLIAN DIGAMMA..GREEK SMALL LETTER PAMPHYLIAN DIGAMMA
            { Start = 0x037A; Last = 0x037A; Width = EastAsianWidth.OfText "N"  } // 037A;N           # Lm         GREEK YPOGEGRAMMENI
            { Start = 0x037B; Last = 0x037D; Width = EastAsianWidth.OfText "N"  } // 037B..037D;N     # Ll     [3] GREEK SMALL REVERSED LUNATE SIGMA SYMBOL..GREEK SMALL REVERSED DOTTED LUNATE SIGMA SYMBOL
            { Start = 0x037E; Last = 0x037E; Width = EastAsianWidth.OfText "N"  } // 037E;N           # Po         GREEK QUESTION MARK
            { Start = 0x037F; Last = 0x037F; Width = EastAsianWidth.OfText "N"  } // 037F;N           # Lu         GREEK CAPITAL LETTER YOT
            { Start = 0x0384; Last = 0x0385; Width = EastAsianWidth.OfText "N"  } // 0384..0385;N     # Sk     [2] GREEK TONOS..GREEK DIALYTIKA TONOS
            { Start = 0x0386; Last = 0x0386; Width = EastAsianWidth.OfText "N"  } // 0386;N           # Lu         GREEK CAPITAL LETTER ALPHA WITH TONOS
            { Start = 0x0387; Last = 0x0387; Width = EastAsianWidth.OfText "N"  } // 0387;N           # Po         GREEK ANO TELEIA
            { Start = 0x0388; Last = 0x038A; Width = EastAsianWidth.OfText "N"  } // 0388..038A;N     # Lu     [3] GREEK CAPITAL LETTER EPSILON WITH TONOS..GREEK CAPITAL LETTER IOTA WITH TONOS
            { Start = 0x038C; Last = 0x038C; Width = EastAsianWidth.OfText "N"  } // 038C;N           # Lu         GREEK CAPITAL LETTER OMICRON WITH TONOS
            { Start = 0x038E; Last = 0x0390; Width = EastAsianWidth.OfText "N"  } // 038E..0390;N     # L&     [3] GREEK CAPITAL LETTER UPSILON WITH TONOS..GREEK SMALL LETTER IOTA WITH DIALYTIKA AND TONOS
            { Start = 0x0391; Last = 0x03A1; Width = EastAsianWidth.OfText "A"  } // 0391..03A1;A     # Lu    [17] GREEK CAPITAL LETTER ALPHA..GREEK CAPITAL LETTER RHO
            { Start = 0x03A3; Last = 0x03A9; Width = EastAsianWidth.OfText "A"  } // 03A3..03A9;A     # Lu     [7] GREEK CAPITAL LETTER SIGMA..GREEK CAPITAL LETTER OMEGA
            { Start = 0x03AA; Last = 0x03B0; Width = EastAsianWidth.OfText "N"  } // 03AA..03B0;N     # L&     [7] GREEK CAPITAL LETTER IOTA WITH DIALYTIKA..GREEK SMALL LETTER UPSILON WITH DIALYTIKA AND TONOS
            { Start = 0x03B1; Last = 0x03C1; Width = EastAsianWidth.OfText "A"  } // 03B1..03C1;A     # Ll    [17] GREEK SMALL LETTER ALPHA..GREEK SMALL LETTER RHO
            { Start = 0x03C2; Last = 0x03C2; Width = EastAsianWidth.OfText "N"  } // 03C2;N           # Ll         GREEK SMALL LETTER FINAL SIGMA
            { Start = 0x03C3; Last = 0x03C9; Width = EastAsianWidth.OfText "A"  } // 03C3..03C9;A     # Ll     [7] GREEK SMALL LETTER SIGMA..GREEK SMALL LETTER OMEGA
            { Start = 0x03CA; Last = 0x03F5; Width = EastAsianWidth.OfText "N"  } // 03CA..03F5;N     # L&    [44] GREEK SMALL LETTER IOTA WITH DIALYTIKA..GREEK LUNATE EPSILON SYMBOL
            { Start = 0x03F6; Last = 0x03F6; Width = EastAsianWidth.OfText "N"  } // 03F6;N           # Sm         GREEK REVERSED LUNATE EPSILON SYMBOL
            { Start = 0x03F7; Last = 0x03FF; Width = EastAsianWidth.OfText "N"  } // 03F7..03FF;N     # L&     [9] GREEK CAPITAL LETTER SHO..GREEK CAPITAL REVERSED DOTTED LUNATE SIGMA SYMBOL
            { Start = 0x0400; Last = 0x0400; Width = EastAsianWidth.OfText "N"  } // 0400;N           # Lu         CYRILLIC CAPITAL LETTER IE WITH GRAVE
            { Start = 0x0401; Last = 0x0401; Width = EastAsianWidth.OfText "A"  } // 0401;A           # Lu         CYRILLIC CAPITAL LETTER IO
            { Start = 0x0402; Last = 0x040F; Width = EastAsianWidth.OfText "N"  } // 0402..040F;N     # Lu    [14] CYRILLIC CAPITAL LETTER DJE..CYRILLIC CAPITAL LETTER DZHE
            { Start = 0x0410; Last = 0x044F; Width = EastAsianWidth.OfText "A"  } // 0410..044F;A     # L&    [64] CYRILLIC CAPITAL LETTER A..CYRILLIC SMALL LETTER YA
            { Start = 0x0450; Last = 0x0450; Width = EastAsianWidth.OfText "N"  } // 0450;N           # Ll         CYRILLIC SMALL LETTER IE WITH GRAVE
            { Start = 0x0451; Last = 0x0451; Width = EastAsianWidth.OfText "A"  } // 0451;A           # Ll         CYRILLIC SMALL LETTER IO
            { Start = 0x0452; Last = 0x0481; Width = EastAsianWidth.OfText "N"  } // 0452..0481;N     # L&    [48] CYRILLIC SMALL LETTER DJE..CYRILLIC SMALL LETTER KOPPA
            { Start = 0x0482; Last = 0x0482; Width = EastAsianWidth.OfText "N"  } // 0482;N           # So         CYRILLIC THOUSANDS SIGN
            { Start = 0x0483; Last = 0x0487; Width = EastAsianWidth.OfText "N"  } // 0483..0487;N     # Mn     [5] COMBINING CYRILLIC TITLO..COMBINING CYRILLIC POKRYTIE
            { Start = 0x0488; Last = 0x0489; Width = EastAsianWidth.OfText "N"  } // 0488..0489;N     # Me     [2] COMBINING CYRILLIC HUNDRED THOUSANDS SIGN..COMBINING CYRILLIC MILLIONS SIGN
            { Start = 0x048A; Last = 0x04FF; Width = EastAsianWidth.OfText "N"  } // 048A..04FF;N     # L&   [118] CYRILLIC CAPITAL LETTER SHORT I WITH TAIL..CYRILLIC SMALL LETTER HA WITH STROKE
            { Start = 0x0500; Last = 0x052F; Width = EastAsianWidth.OfText "N"  } // 0500..052F;N     # L&    [48] CYRILLIC CAPITAL LETTER KOMI DE..CYRILLIC SMALL LETTER EL WITH DESCENDER
            { Start = 0x0531; Last = 0x0556; Width = EastAsianWidth.OfText "N"  } // 0531..0556;N     # Lu    [38] ARMENIAN CAPITAL LETTER AYB..ARMENIAN CAPITAL LETTER FEH
            { Start = 0x0559; Last = 0x0559; Width = EastAsianWidth.OfText "N"  } // 0559;N           # Lm         ARMENIAN MODIFIER LETTER LEFT HALF RING
            { Start = 0x055A; Last = 0x055F; Width = EastAsianWidth.OfText "N"  } // 055A..055F;N     # Po     [6] ARMENIAN APOSTROPHE..ARMENIAN ABBREVIATION MARK
            { Start = 0x0560; Last = 0x0588; Width = EastAsianWidth.OfText "N"  } // 0560..0588;N     # Ll    [41] ARMENIAN SMALL LETTER TURNED AYB..ARMENIAN SMALL LETTER YI WITH STROKE
            { Start = 0x0589; Last = 0x0589; Width = EastAsianWidth.OfText "N"  } // 0589;N           # Po         ARMENIAN FULL STOP
            { Start = 0x058A; Last = 0x058A; Width = EastAsianWidth.OfText "N"  } // 058A;N           # Pd         ARMENIAN HYPHEN
            { Start = 0x058D; Last = 0x058E; Width = EastAsianWidth.OfText "N"  } // 058D..058E;N     # So     [2] RIGHT-FACING ARMENIAN ETERNITY SIGN..LEFT-FACING ARMENIAN ETERNITY SIGN
            { Start = 0x058F; Last = 0x058F; Width = EastAsianWidth.OfText "N"  } // 058F;N           # Sc         ARMENIAN DRAM SIGN
            { Start = 0x0591; Last = 0x05BD; Width = EastAsianWidth.OfText "N"  } // 0591..05BD;N     # Mn    [45] HEBREW ACCENT ETNAHTA..HEBREW POINT METEG
            { Start = 0x05BE; Last = 0x05BE; Width = EastAsianWidth.OfText "N"  } // 05BE;N           # Pd         HEBREW PUNCTUATION MAQAF
            { Start = 0x05BF; Last = 0x05BF; Width = EastAsianWidth.OfText "N"  } // 05BF;N           # Mn         HEBREW POINT RAFE
            { Start = 0x05C0; Last = 0x05C0; Width = EastAsianWidth.OfText "N"  } // 05C0;N           # Po         HEBREW PUNCTUATION PASEQ
            { Start = 0x05C1; Last = 0x05C2; Width = EastAsianWidth.OfText "N"  } // 05C1..05C2;N     # Mn     [2] HEBREW POINT SHIN DOT..HEBREW POINT SIN DOT
            { Start = 0x05C3; Last = 0x05C3; Width = EastAsianWidth.OfText "N"  } // 05C3;N           # Po         HEBREW PUNCTUATION SOF PASUQ
            { Start = 0x05C4; Last = 0x05C5; Width = EastAsianWidth.OfText "N"  } // 05C4..05C5;N     # Mn     [2] HEBREW MARK UPPER DOT..HEBREW MARK LOWER DOT
            { Start = 0x05C6; Last = 0x05C6; Width = EastAsianWidth.OfText "N"  } // 05C6;N           # Po         HEBREW PUNCTUATION NUN HAFUKHA
            { Start = 0x05C7; Last = 0x05C7; Width = EastAsianWidth.OfText "N"  } // 05C7;N           # Mn         HEBREW POINT QAMATS QATAN
            { Start = 0x05D0; Last = 0x05EA; Width = EastAsianWidth.OfText "N"  } // 05D0..05EA;N     # Lo    [27] HEBREW LETTER ALEF..HEBREW LETTER TAV
            { Start = 0x05EF; Last = 0x05F2; Width = EastAsianWidth.OfText "N"  } // 05EF..05F2;N     # Lo     [4] HEBREW YOD TRIANGLE..HEBREW LIGATURE YIDDISH DOUBLE YOD
            { Start = 0x05F3; Last = 0x05F4; Width = EastAsianWidth.OfText "N"  } // 05F3..05F4;N     # Po     [2] HEBREW PUNCTUATION GERESH..HEBREW PUNCTUATION GERSHAYIM
            { Start = 0x0600; Last = 0x0605; Width = EastAsianWidth.OfText "N"  } // 0600..0605;N     # Cf     [6] ARABIC NUMBER SIGN..ARABIC NUMBER MARK ABOVE
            { Start = 0x0606; Last = 0x0608; Width = EastAsianWidth.OfText "N"  } // 0606..0608;N     # Sm     [3] ARABIC-INDIC CUBE ROOT..ARABIC RAY
            { Start = 0x0609; Last = 0x060A; Width = EastAsianWidth.OfText "N"  } // 0609..060A;N     # Po     [2] ARABIC-INDIC PER MILLE SIGN..ARABIC-INDIC PER TEN THOUSAND SIGN
            { Start = 0x060B; Last = 0x060B; Width = EastAsianWidth.OfText "N"  } // 060B;N           # Sc         AFGHANI SIGN
            { Start = 0x060C; Last = 0x060D; Width = EastAsianWidth.OfText "N"  } // 060C..060D;N     # Po     [2] ARABIC COMMA..ARABIC DATE SEPARATOR
            { Start = 0x060E; Last = 0x060F; Width = EastAsianWidth.OfText "N"  } // 060E..060F;N     # So     [2] ARABIC POETIC VERSE SIGN..ARABIC SIGN MISRA
            { Start = 0x0610; Last = 0x061A; Width = EastAsianWidth.OfText "N"  } // 0610..061A;N     # Mn    [11] ARABIC SIGN SALLALLAHOU ALAYHE WASSALLAM..ARABIC SMALL KASRA
            { Start = 0x061B; Last = 0x061B; Width = EastAsianWidth.OfText "N"  } // 061B;N           # Po         ARABIC SEMICOLON
            { Start = 0x061C; Last = 0x061C; Width = EastAsianWidth.OfText "N"  } // 061C;N           # Cf         ARABIC LETTER MARK
            { Start = 0x061E; Last = 0x061F; Width = EastAsianWidth.OfText "N"  } // 061E..061F;N     # Po     [2] ARABIC TRIPLE DOT PUNCTUATION MARK..ARABIC QUESTION MARK
            { Start = 0x0620; Last = 0x063F; Width = EastAsianWidth.OfText "N"  } // 0620..063F;N     # Lo    [32] ARABIC LETTER KASHMIRI YEH..ARABIC LETTER FARSI YEH WITH THREE DOTS ABOVE
            { Start = 0x0640; Last = 0x0640; Width = EastAsianWidth.OfText "N"  } // 0640;N           # Lm         ARABIC TATWEEL
            { Start = 0x0641; Last = 0x064A; Width = EastAsianWidth.OfText "N"  } // 0641..064A;N     # Lo    [10] ARABIC LETTER FEH..ARABIC LETTER YEH
            { Start = 0x064B; Last = 0x065F; Width = EastAsianWidth.OfText "N"  } // 064B..065F;N     # Mn    [21] ARABIC FATHATAN..ARABIC WAVY HAMZA BELOW
            { Start = 0x0660; Last = 0x0669; Width = EastAsianWidth.OfText "N"  } // 0660..0669;N     # Nd    [10] ARABIC-INDIC DIGIT ZERO..ARABIC-INDIC DIGIT NINE
            { Start = 0x066A; Last = 0x066D; Width = EastAsianWidth.OfText "N"  } // 066A..066D;N     # Po     [4] ARABIC PERCENT SIGN..ARABIC FIVE POINTED STAR
            { Start = 0x066E; Last = 0x066F; Width = EastAsianWidth.OfText "N"  } // 066E..066F;N     # Lo     [2] ARABIC LETTER DOTLESS BEH..ARABIC LETTER DOTLESS QAF
            { Start = 0x0670; Last = 0x0670; Width = EastAsianWidth.OfText "N"  } // 0670;N           # Mn         ARABIC LETTER SUPERSCRIPT ALEF
            { Start = 0x0671; Last = 0x06D3; Width = EastAsianWidth.OfText "N"  } // 0671..06D3;N     # Lo    [99] ARABIC LETTER ALEF WASLA..ARABIC LETTER YEH BARREE WITH HAMZA ABOVE
            { Start = 0x06D4; Last = 0x06D4; Width = EastAsianWidth.OfText "N"  } // 06D4;N           # Po         ARABIC FULL STOP
            { Start = 0x06D5; Last = 0x06D5; Width = EastAsianWidth.OfText "N"  } // 06D5;N           # Lo         ARABIC LETTER AE
            { Start = 0x06D6; Last = 0x06DC; Width = EastAsianWidth.OfText "N"  } // 06D6..06DC;N     # Mn     [7] ARABIC SMALL HIGH LIGATURE SAD WITH LAM WITH ALEF MAKSURA..ARABIC SMALL HIGH SEEN
            { Start = 0x06DD; Last = 0x06DD; Width = EastAsianWidth.OfText "N"  } // 06DD;N           # Cf         ARABIC END OF AYAH
            { Start = 0x06DE; Last = 0x06DE; Width = EastAsianWidth.OfText "N"  } // 06DE;N           # So         ARABIC START OF RUB EL HIZB
            { Start = 0x06DF; Last = 0x06E4; Width = EastAsianWidth.OfText "N"  } // 06DF..06E4;N     # Mn     [6] ARABIC SMALL HIGH ROUNDED ZERO..ARABIC SMALL HIGH MADDA
            { Start = 0x06E5; Last = 0x06E6; Width = EastAsianWidth.OfText "N"  } // 06E5..06E6;N     # Lm     [2] ARABIC SMALL WAW..ARABIC SMALL YEH
            { Start = 0x06E7; Last = 0x06E8; Width = EastAsianWidth.OfText "N"  } // 06E7..06E8;N     # Mn     [2] ARABIC SMALL HIGH YEH..ARABIC SMALL HIGH NOON
            { Start = 0x06E9; Last = 0x06E9; Width = EastAsianWidth.OfText "N"  } // 06E9;N           # So         ARABIC PLACE OF SAJDAH
            { Start = 0x06EA; Last = 0x06ED; Width = EastAsianWidth.OfText "N"  } // 06EA..06ED;N     # Mn     [4] ARABIC EMPTY CENTRE LOW STOP..ARABIC SMALL LOW MEEM
            { Start = 0x06EE; Last = 0x06EF; Width = EastAsianWidth.OfText "N"  } // 06EE..06EF;N     # Lo     [2] ARABIC LETTER DAL WITH INVERTED V..ARABIC LETTER REH WITH INVERTED V
            { Start = 0x06F0; Last = 0x06F9; Width = EastAsianWidth.OfText "N"  } // 06F0..06F9;N     # Nd    [10] EXTENDED ARABIC-INDIC DIGIT ZERO..EXTENDED ARABIC-INDIC DIGIT NINE
            { Start = 0x06FA; Last = 0x06FC; Width = EastAsianWidth.OfText "N"  } // 06FA..06FC;N     # Lo     [3] ARABIC LETTER SHEEN WITH DOT BELOW..ARABIC LETTER GHAIN WITH DOT BELOW
            { Start = 0x06FD; Last = 0x06FE; Width = EastAsianWidth.OfText "N"  } // 06FD..06FE;N     # So     [2] ARABIC SIGN SINDHI AMPERSAND..ARABIC SIGN SINDHI POSTPOSITION MEN
            { Start = 0x06FF; Last = 0x06FF; Width = EastAsianWidth.OfText "N"  } // 06FF;N           # Lo         ARABIC LETTER HEH WITH INVERTED V
            { Start = 0x0700; Last = 0x070D; Width = EastAsianWidth.OfText "N"  } // 0700..070D;N     # Po    [14] SYRIAC END OF PARAGRAPH..SYRIAC HARKLEAN ASTERISCUS
            { Start = 0x070F; Last = 0x070F; Width = EastAsianWidth.OfText "N"  } // 070F;N           # Cf         SYRIAC ABBREVIATION MARK
            { Start = 0x0710; Last = 0x0710; Width = EastAsianWidth.OfText "N"  } // 0710;N           # Lo         SYRIAC LETTER ALAPH
            { Start = 0x0711; Last = 0x0711; Width = EastAsianWidth.OfText "N"  } // 0711;N           # Mn         SYRIAC LETTER SUPERSCRIPT ALAPH
            { Start = 0x0712; Last = 0x072F; Width = EastAsianWidth.OfText "N"  } // 0712..072F;N     # Lo    [30] SYRIAC LETTER BETH..SYRIAC LETTER PERSIAN DHALATH
            { Start = 0x0730; Last = 0x074A; Width = EastAsianWidth.OfText "N"  } // 0730..074A;N     # Mn    [27] SYRIAC PTHAHA ABOVE..SYRIAC BARREKH
            { Start = 0x074D; Last = 0x074F; Width = EastAsianWidth.OfText "N"  } // 074D..074F;N     # Lo     [3] SYRIAC LETTER SOGDIAN ZHAIN..SYRIAC LETTER SOGDIAN FE
            { Start = 0x0750; Last = 0x077F; Width = EastAsianWidth.OfText "N"  } // 0750..077F;N     # Lo    [48] ARABIC LETTER BEH WITH THREE DOTS HORIZONTALLY BELOW..ARABIC LETTER KAF WITH TWO DOTS ABOVE
            { Start = 0x0780; Last = 0x07A5; Width = EastAsianWidth.OfText "N"  } // 0780..07A5;N     # Lo    [38] THAANA LETTER HAA..THAANA LETTER WAAVU
            { Start = 0x07A6; Last = 0x07B0; Width = EastAsianWidth.OfText "N"  } // 07A6..07B0;N     # Mn    [11] THAANA ABAFILI..THAANA SUKUN
            { Start = 0x07B1; Last = 0x07B1; Width = EastAsianWidth.OfText "N"  } // 07B1;N           # Lo         THAANA LETTER NAA
            { Start = 0x07C0; Last = 0x07C9; Width = EastAsianWidth.OfText "N"  } // 07C0..07C9;N     # Nd    [10] NKO DIGIT ZERO..NKO DIGIT NINE
            { Start = 0x07CA; Last = 0x07EA; Width = EastAsianWidth.OfText "N"  } // 07CA..07EA;N     # Lo    [33] NKO LETTER A..NKO LETTER JONA RA
            { Start = 0x07EB; Last = 0x07F3; Width = EastAsianWidth.OfText "N"  } // 07EB..07F3;N     # Mn     [9] NKO COMBINING SHORT HIGH TONE..NKO COMBINING DOUBLE DOT ABOVE
            { Start = 0x07F4; Last = 0x07F5; Width = EastAsianWidth.OfText "N"  } // 07F4..07F5;N     # Lm     [2] NKO HIGH TONE APOSTROPHE..NKO LOW TONE APOSTROPHE
            { Start = 0x07F6; Last = 0x07F6; Width = EastAsianWidth.OfText "N"  } // 07F6;N           # So         NKO SYMBOL OO DENNEN
            { Start = 0x07F7; Last = 0x07F9; Width = EastAsianWidth.OfText "N"  } // 07F7..07F9;N     # Po     [3] NKO SYMBOL GBAKURUNEN..NKO EXCLAMATION MARK
            { Start = 0x07FA; Last = 0x07FA; Width = EastAsianWidth.OfText "N"  } // 07FA;N           # Lm         NKO LAJANYALAN
            { Start = 0x07FD; Last = 0x07FD; Width = EastAsianWidth.OfText "N"  } // 07FD;N           # Mn         NKO DANTAYALAN
            { Start = 0x07FE; Last = 0x07FF; Width = EastAsianWidth.OfText "N"  } // 07FE..07FF;N     # Sc     [2] NKO DOROME SIGN..NKO TAMAN SIGN
            { Start = 0x0800; Last = 0x0815; Width = EastAsianWidth.OfText "N"  } // 0800..0815;N     # Lo    [22] SAMARITAN LETTER ALAF..SAMARITAN LETTER TAAF
            { Start = 0x0816; Last = 0x0819; Width = EastAsianWidth.OfText "N"  } // 0816..0819;N     # Mn     [4] SAMARITAN MARK IN..SAMARITAN MARK DAGESH
            { Start = 0x081A; Last = 0x081A; Width = EastAsianWidth.OfText "N"  } // 081A;N           # Lm         SAMARITAN MODIFIER LETTER EPENTHETIC YUT
            { Start = 0x081B; Last = 0x0823; Width = EastAsianWidth.OfText "N"  } // 081B..0823;N     # Mn     [9] SAMARITAN MARK EPENTHETIC YUT..SAMARITAN VOWEL SIGN A
            { Start = 0x0824; Last = 0x0824; Width = EastAsianWidth.OfText "N"  } // 0824;N           # Lm         SAMARITAN MODIFIER LETTER SHORT A
            { Start = 0x0825; Last = 0x0827; Width = EastAsianWidth.OfText "N"  } // 0825..0827;N     # Mn     [3] SAMARITAN VOWEL SIGN SHORT A..SAMARITAN VOWEL SIGN U
            { Start = 0x0828; Last = 0x0828; Width = EastAsianWidth.OfText "N"  } // 0828;N           # Lm         SAMARITAN MODIFIER LETTER I
            { Start = 0x0829; Last = 0x082D; Width = EastAsianWidth.OfText "N"  } // 0829..082D;N     # Mn     [5] SAMARITAN VOWEL SIGN LONG I..SAMARITAN MARK NEQUDAA
            { Start = 0x0830; Last = 0x083E; Width = EastAsianWidth.OfText "N"  } // 0830..083E;N     # Po    [15] SAMARITAN PUNCTUATION NEQUDAA..SAMARITAN PUNCTUATION ANNAAU
            { Start = 0x0840; Last = 0x0858; Width = EastAsianWidth.OfText "N"  } // 0840..0858;N     # Lo    [25] MANDAIC LETTER HALQA..MANDAIC LETTER AIN
            { Start = 0x0859; Last = 0x085B; Width = EastAsianWidth.OfText "N"  } // 0859..085B;N     # Mn     [3] MANDAIC AFFRICATION MARK..MANDAIC GEMINATION MARK
            { Start = 0x085E; Last = 0x085E; Width = EastAsianWidth.OfText "N"  } // 085E;N           # Po         MANDAIC PUNCTUATION
            { Start = 0x0860; Last = 0x086A; Width = EastAsianWidth.OfText "N"  } // 0860..086A;N     # Lo    [11] SYRIAC LETTER MALAYALAM NGA..SYRIAC LETTER MALAYALAM SSA
            { Start = 0x08A0; Last = 0x08B4; Width = EastAsianWidth.OfText "N"  } // 08A0..08B4;N     # Lo    [21] ARABIC LETTER BEH WITH SMALL V BELOW..ARABIC LETTER KAF WITH DOT BELOW
            { Start = 0x08B6; Last = 0x08BD; Width = EastAsianWidth.OfText "N"  } // 08B6..08BD;N     # Lo     [8] ARABIC LETTER BEH WITH SMALL MEEM ABOVE..ARABIC LETTER AFRICAN NOON
            { Start = 0x08D3; Last = 0x08E1; Width = EastAsianWidth.OfText "N"  } // 08D3..08E1;N     # Mn    [15] ARABIC SMALL LOW WAW..ARABIC SMALL HIGH SIGN SAFHA
            { Start = 0x08E2; Last = 0x08E2; Width = EastAsianWidth.OfText "N"  } // 08E2;N           # Cf         ARABIC DISPUTED END OF AYAH
            { Start = 0x08E3; Last = 0x08FF; Width = EastAsianWidth.OfText "N"  } // 08E3..08FF;N     # Mn    [29] ARABIC TURNED DAMMA BELOW..ARABIC MARK SIDEWAYS NOON GHUNNA
            { Start = 0x0900; Last = 0x0902; Width = EastAsianWidth.OfText "N"  } // 0900..0902;N     # Mn     [3] DEVANAGARI SIGN INVERTED CANDRABINDU..DEVANAGARI SIGN ANUSVARA
            { Start = 0x0903; Last = 0x0903; Width = EastAsianWidth.OfText "N"  } // 0903;N           # Mc         DEVANAGARI SIGN VISARGA
            { Start = 0x0904; Last = 0x0939; Width = EastAsianWidth.OfText "N"  } // 0904..0939;N     # Lo    [54] DEVANAGARI LETTER SHORT A..DEVANAGARI LETTER HA
            { Start = 0x093A; Last = 0x093A; Width = EastAsianWidth.OfText "N"  } // 093A;N           # Mn         DEVANAGARI VOWEL SIGN OE
            { Start = 0x093B; Last = 0x093B; Width = EastAsianWidth.OfText "N"  } // 093B;N           # Mc         DEVANAGARI VOWEL SIGN OOE
            { Start = 0x093C; Last = 0x093C; Width = EastAsianWidth.OfText "N"  } // 093C;N           # Mn         DEVANAGARI SIGN NUKTA
            { Start = 0x093D; Last = 0x093D; Width = EastAsianWidth.OfText "N"  } // 093D;N           # Lo         DEVANAGARI SIGN AVAGRAHA
            { Start = 0x093E; Last = 0x0940; Width = EastAsianWidth.OfText "N"  } // 093E..0940;N     # Mc     [3] DEVANAGARI VOWEL SIGN AA..DEVANAGARI VOWEL SIGN II
            { Start = 0x0941; Last = 0x0948; Width = EastAsianWidth.OfText "N"  } // 0941..0948;N     # Mn     [8] DEVANAGARI VOWEL SIGN U..DEVANAGARI VOWEL SIGN AI
            { Start = 0x0949; Last = 0x094C; Width = EastAsianWidth.OfText "N"  } // 0949..094C;N     # Mc     [4] DEVANAGARI VOWEL SIGN CANDRA O..DEVANAGARI VOWEL SIGN AU
            { Start = 0x094D; Last = 0x094D; Width = EastAsianWidth.OfText "N"  } // 094D;N           # Mn         DEVANAGARI SIGN VIRAMA
            { Start = 0x094E; Last = 0x094F; Width = EastAsianWidth.OfText "N"  } // 094E..094F;N     # Mc     [2] DEVANAGARI VOWEL SIGN PRISHTHAMATRA E..DEVANAGARI VOWEL SIGN AW
            { Start = 0x0950; Last = 0x0950; Width = EastAsianWidth.OfText "N"  } // 0950;N           # Lo         DEVANAGARI OM
            { Start = 0x0951; Last = 0x0957; Width = EastAsianWidth.OfText "N"  } // 0951..0957;N     # Mn     [7] DEVANAGARI STRESS SIGN UDATTA..DEVANAGARI VOWEL SIGN UUE
            { Start = 0x0958; Last = 0x0961; Width = EastAsianWidth.OfText "N"  } // 0958..0961;N     # Lo    [10] DEVANAGARI LETTER QA..DEVANAGARI LETTER VOCALIC LL
            { Start = 0x0962; Last = 0x0963; Width = EastAsianWidth.OfText "N"  } // 0962..0963;N     # Mn     [2] DEVANAGARI VOWEL SIGN VOCALIC L..DEVANAGARI VOWEL SIGN VOCALIC LL
            { Start = 0x0964; Last = 0x0965; Width = EastAsianWidth.OfText "N"  } // 0964..0965;N     # Po     [2] DEVANAGARI DANDA..DEVANAGARI DOUBLE DANDA
            { Start = 0x0966; Last = 0x096F; Width = EastAsianWidth.OfText "N"  } // 0966..096F;N     # Nd    [10] DEVANAGARI DIGIT ZERO..DEVANAGARI DIGIT NINE
            { Start = 0x0970; Last = 0x0970; Width = EastAsianWidth.OfText "N"  } // 0970;N           # Po         DEVANAGARI ABBREVIATION SIGN
            { Start = 0x0971; Last = 0x0971; Width = EastAsianWidth.OfText "N"  } // 0971;N           # Lm         DEVANAGARI SIGN HIGH SPACING DOT
            { Start = 0x0972; Last = 0x097F; Width = EastAsianWidth.OfText "N"  } // 0972..097F;N     # Lo    [14] DEVANAGARI LETTER CANDRA A..DEVANAGARI LETTER BBA
            { Start = 0x0980; Last = 0x0980; Width = EastAsianWidth.OfText "N"  } // 0980;N           # Lo         BENGALI ANJI
            { Start = 0x0981; Last = 0x0981; Width = EastAsianWidth.OfText "N"  } // 0981;N           # Mn         BENGALI SIGN CANDRABINDU
            { Start = 0x0982; Last = 0x0983; Width = EastAsianWidth.OfText "N"  } // 0982..0983;N     # Mc     [2] BENGALI SIGN ANUSVARA..BENGALI SIGN VISARGA
            { Start = 0x0985; Last = 0x098C; Width = EastAsianWidth.OfText "N"  } // 0985..098C;N     # Lo     [8] BENGALI LETTER A..BENGALI LETTER VOCALIC L
            { Start = 0x098F; Last = 0x0990; Width = EastAsianWidth.OfText "N"  } // 098F..0990;N     # Lo     [2] BENGALI LETTER E..BENGALI LETTER AI
            { Start = 0x0993; Last = 0x09A8; Width = EastAsianWidth.OfText "N"  } // 0993..09A8;N     # Lo    [22] BENGALI LETTER O..BENGALI LETTER NA
            { Start = 0x09AA; Last = 0x09B0; Width = EastAsianWidth.OfText "N"  } // 09AA..09B0;N     # Lo     [7] BENGALI LETTER PA..BENGALI LETTER RA
            { Start = 0x09B2; Last = 0x09B2; Width = EastAsianWidth.OfText "N"  } // 09B2;N           # Lo         BENGALI LETTER LA
            { Start = 0x09B6; Last = 0x09B9; Width = EastAsianWidth.OfText "N"  } // 09B6..09B9;N     # Lo     [4] BENGALI LETTER SHA..BENGALI LETTER HA
            { Start = 0x09BC; Last = 0x09BC; Width = EastAsianWidth.OfText "N"  } // 09BC;N           # Mn         BENGALI SIGN NUKTA
            { Start = 0x09BD; Last = 0x09BD; Width = EastAsianWidth.OfText "N"  } // 09BD;N           # Lo         BENGALI SIGN AVAGRAHA
            { Start = 0x09BE; Last = 0x09C0; Width = EastAsianWidth.OfText "N"  } // 09BE..09C0;N     # Mc     [3] BENGALI VOWEL SIGN AA..BENGALI VOWEL SIGN II
            { Start = 0x09C1; Last = 0x09C4; Width = EastAsianWidth.OfText "N"  } // 09C1..09C4;N     # Mn     [4] BENGALI VOWEL SIGN U..BENGALI VOWEL SIGN VOCALIC RR
            { Start = 0x09C7; Last = 0x09C8; Width = EastAsianWidth.OfText "N"  } // 09C7..09C8;N     # Mc     [2] BENGALI VOWEL SIGN E..BENGALI VOWEL SIGN AI
            { Start = 0x09CB; Last = 0x09CC; Width = EastAsianWidth.OfText "N"  } // 09CB..09CC;N     # Mc     [2] BENGALI VOWEL SIGN O..BENGALI VOWEL SIGN AU
            { Start = 0x09CD; Last = 0x09CD; Width = EastAsianWidth.OfText "N"  } // 09CD;N           # Mn         BENGALI SIGN VIRAMA
            { Start = 0x09CE; Last = 0x09CE; Width = EastAsianWidth.OfText "N"  } // 09CE;N           # Lo         BENGALI LETTER KHANDA TA
            { Start = 0x09D7; Last = 0x09D7; Width = EastAsianWidth.OfText "N"  } // 09D7;N           # Mc         BENGALI AU LENGTH MARK
            { Start = 0x09DC; Last = 0x09DD; Width = EastAsianWidth.OfText "N"  } // 09DC..09DD;N     # Lo     [2] BENGALI LETTER RRA..BENGALI LETTER RHA
            { Start = 0x09DF; Last = 0x09E1; Width = EastAsianWidth.OfText "N"  } // 09DF..09E1;N     # Lo     [3] BENGALI LETTER YYA..BENGALI LETTER VOCALIC LL
            { Start = 0x09E2; Last = 0x09E3; Width = EastAsianWidth.OfText "N"  } // 09E2..09E3;N     # Mn     [2] BENGALI VOWEL SIGN VOCALIC L..BENGALI VOWEL SIGN VOCALIC LL
            { Start = 0x09E6; Last = 0x09EF; Width = EastAsianWidth.OfText "N"  } // 09E6..09EF;N     # Nd    [10] BENGALI DIGIT ZERO..BENGALI DIGIT NINE
            { Start = 0x09F0; Last = 0x09F1; Width = EastAsianWidth.OfText "N"  } // 09F0..09F1;N     # Lo     [2] BENGALI LETTER RA WITH MIDDLE DIAGONAL..BENGALI LETTER RA WITH LOWER DIAGONAL
            { Start = 0x09F2; Last = 0x09F3; Width = EastAsianWidth.OfText "N"  } // 09F2..09F3;N     # Sc     [2] BENGALI RUPEE MARK..BENGALI RUPEE SIGN
            { Start = 0x09F4; Last = 0x09F9; Width = EastAsianWidth.OfText "N"  } // 09F4..09F9;N     # No     [6] BENGALI CURRENCY NUMERATOR ONE..BENGALI CURRENCY DENOMINATOR SIXTEEN
            { Start = 0x09FA; Last = 0x09FA; Width = EastAsianWidth.OfText "N"  } // 09FA;N           # So         BENGALI ISSHAR
            { Start = 0x09FB; Last = 0x09FB; Width = EastAsianWidth.OfText "N"  } // 09FB;N           # Sc         BENGALI GANDA MARK
            { Start = 0x09FC; Last = 0x09FC; Width = EastAsianWidth.OfText "N"  } // 09FC;N           # Lo         BENGALI LETTER VEDIC ANUSVARA
            { Start = 0x09FD; Last = 0x09FD; Width = EastAsianWidth.OfText "N"  } // 09FD;N           # Po         BENGALI ABBREVIATION SIGN
            { Start = 0x09FE; Last = 0x09FE; Width = EastAsianWidth.OfText "N"  } // 09FE;N           # Mn         BENGALI SANDHI MARK
            { Start = 0x0A01; Last = 0x0A02; Width = EastAsianWidth.OfText "N"  } // 0A01..0A02;N     # Mn     [2] GURMUKHI SIGN ADAK BINDI..GURMUKHI SIGN BINDI
            { Start = 0x0A03; Last = 0x0A03; Width = EastAsianWidth.OfText "N"  } // 0A03;N           # Mc         GURMUKHI SIGN VISARGA
            { Start = 0x0A05; Last = 0x0A0A; Width = EastAsianWidth.OfText "N"  } // 0A05..0A0A;N     # Lo     [6] GURMUKHI LETTER A..GURMUKHI LETTER UU
            { Start = 0x0A0F; Last = 0x0A10; Width = EastAsianWidth.OfText "N"  } // 0A0F..0A10;N     # Lo     [2] GURMUKHI LETTER EE..GURMUKHI LETTER AI
            { Start = 0x0A13; Last = 0x0A28; Width = EastAsianWidth.OfText "N"  } // 0A13..0A28;N     # Lo    [22] GURMUKHI LETTER OO..GURMUKHI LETTER NA
            { Start = 0x0A2A; Last = 0x0A30; Width = EastAsianWidth.OfText "N"  } // 0A2A..0A30;N     # Lo     [7] GURMUKHI LETTER PA..GURMUKHI LETTER RA
            { Start = 0x0A32; Last = 0x0A33; Width = EastAsianWidth.OfText "N"  } // 0A32..0A33;N     # Lo     [2] GURMUKHI LETTER LA..GURMUKHI LETTER LLA
            { Start = 0x0A35; Last = 0x0A36; Width = EastAsianWidth.OfText "N"  } // 0A35..0A36;N     # Lo     [2] GURMUKHI LETTER VA..GURMUKHI LETTER SHA
            { Start = 0x0A38; Last = 0x0A39; Width = EastAsianWidth.OfText "N"  } // 0A38..0A39;N     # Lo     [2] GURMUKHI LETTER SA..GURMUKHI LETTER HA
            { Start = 0x0A3C; Last = 0x0A3C; Width = EastAsianWidth.OfText "N"  } // 0A3C;N           # Mn         GURMUKHI SIGN NUKTA
            { Start = 0x0A3E; Last = 0x0A40; Width = EastAsianWidth.OfText "N"  } // 0A3E..0A40;N     # Mc     [3] GURMUKHI VOWEL SIGN AA..GURMUKHI VOWEL SIGN II
            { Start = 0x0A41; Last = 0x0A42; Width = EastAsianWidth.OfText "N"  } // 0A41..0A42;N     # Mn     [2] GURMUKHI VOWEL SIGN U..GURMUKHI VOWEL SIGN UU
            { Start = 0x0A47; Last = 0x0A48; Width = EastAsianWidth.OfText "N"  } // 0A47..0A48;N     # Mn     [2] GURMUKHI VOWEL SIGN EE..GURMUKHI VOWEL SIGN AI
            { Start = 0x0A4B; Last = 0x0A4D; Width = EastAsianWidth.OfText "N"  } // 0A4B..0A4D;N     # Mn     [3] GURMUKHI VOWEL SIGN OO..GURMUKHI SIGN VIRAMA
            { Start = 0x0A51; Last = 0x0A51; Width = EastAsianWidth.OfText "N"  } // 0A51;N           # Mn         GURMUKHI SIGN UDAAT
            { Start = 0x0A59; Last = 0x0A5C; Width = EastAsianWidth.OfText "N"  } // 0A59..0A5C;N     # Lo     [4] GURMUKHI LETTER KHHA..GURMUKHI LETTER RRA
            { Start = 0x0A5E; Last = 0x0A5E; Width = EastAsianWidth.OfText "N"  } // 0A5E;N           # Lo         GURMUKHI LETTER FA
            { Start = 0x0A66; Last = 0x0A6F; Width = EastAsianWidth.OfText "N"  } // 0A66..0A6F;N     # Nd    [10] GURMUKHI DIGIT ZERO..GURMUKHI DIGIT NINE
            { Start = 0x0A70; Last = 0x0A71; Width = EastAsianWidth.OfText "N"  } // 0A70..0A71;N     # Mn     [2] GURMUKHI TIPPI..GURMUKHI ADDAK
            { Start = 0x0A72; Last = 0x0A74; Width = EastAsianWidth.OfText "N"  } // 0A72..0A74;N     # Lo     [3] GURMUKHI IRI..GURMUKHI EK ONKAR
            { Start = 0x0A75; Last = 0x0A75; Width = EastAsianWidth.OfText "N"  } // 0A75;N           # Mn         GURMUKHI SIGN YAKASH
            { Start = 0x0A76; Last = 0x0A76; Width = EastAsianWidth.OfText "N"  } // 0A76;N           # Po         GURMUKHI ABBREVIATION SIGN
            { Start = 0x0A81; Last = 0x0A82; Width = EastAsianWidth.OfText "N"  } // 0A81..0A82;N     # Mn     [2] GUJARATI SIGN CANDRABINDU..GUJARATI SIGN ANUSVARA
            { Start = 0x0A83; Last = 0x0A83; Width = EastAsianWidth.OfText "N"  } // 0A83;N           # Mc         GUJARATI SIGN VISARGA
            { Start = 0x0A85; Last = 0x0A8D; Width = EastAsianWidth.OfText "N"  } // 0A85..0A8D;N     # Lo     [9] GUJARATI LETTER A..GUJARATI VOWEL CANDRA E
            { Start = 0x0A8F; Last = 0x0A91; Width = EastAsianWidth.OfText "N"  } // 0A8F..0A91;N     # Lo     [3] GUJARATI LETTER E..GUJARATI VOWEL CANDRA O
            { Start = 0x0A93; Last = 0x0AA8; Width = EastAsianWidth.OfText "N"  } // 0A93..0AA8;N     # Lo    [22] GUJARATI LETTER O..GUJARATI LETTER NA
            { Start = 0x0AAA; Last = 0x0AB0; Width = EastAsianWidth.OfText "N"  } // 0AAA..0AB0;N     # Lo     [7] GUJARATI LETTER PA..GUJARATI LETTER RA
            { Start = 0x0AB2; Last = 0x0AB3; Width = EastAsianWidth.OfText "N"  } // 0AB2..0AB3;N     # Lo     [2] GUJARATI LETTER LA..GUJARATI LETTER LLA
            { Start = 0x0AB5; Last = 0x0AB9; Width = EastAsianWidth.OfText "N"  } // 0AB5..0AB9;N     # Lo     [5] GUJARATI LETTER VA..GUJARATI LETTER HA
            { Start = 0x0ABC; Last = 0x0ABC; Width = EastAsianWidth.OfText "N"  } // 0ABC;N           # Mn         GUJARATI SIGN NUKTA
            { Start = 0x0ABD; Last = 0x0ABD; Width = EastAsianWidth.OfText "N"  } // 0ABD;N           # Lo         GUJARATI SIGN AVAGRAHA
            { Start = 0x0ABE; Last = 0x0AC0; Width = EastAsianWidth.OfText "N"  } // 0ABE..0AC0;N     # Mc     [3] GUJARATI VOWEL SIGN AA..GUJARATI VOWEL SIGN II
            { Start = 0x0AC1; Last = 0x0AC5; Width = EastAsianWidth.OfText "N"  } // 0AC1..0AC5;N     # Mn     [5] GUJARATI VOWEL SIGN U..GUJARATI VOWEL SIGN CANDRA E
            { Start = 0x0AC7; Last = 0x0AC8; Width = EastAsianWidth.OfText "N"  } // 0AC7..0AC8;N     # Mn     [2] GUJARATI VOWEL SIGN E..GUJARATI VOWEL SIGN AI
            { Start = 0x0AC9; Last = 0x0AC9; Width = EastAsianWidth.OfText "N"  } // 0AC9;N           # Mc         GUJARATI VOWEL SIGN CANDRA O
            { Start = 0x0ACB; Last = 0x0ACC; Width = EastAsianWidth.OfText "N"  } // 0ACB..0ACC;N     # Mc     [2] GUJARATI VOWEL SIGN O..GUJARATI VOWEL SIGN AU
            { Start = 0x0ACD; Last = 0x0ACD; Width = EastAsianWidth.OfText "N"  } // 0ACD;N           # Mn         GUJARATI SIGN VIRAMA
            { Start = 0x0AD0; Last = 0x0AD0; Width = EastAsianWidth.OfText "N"  } // 0AD0;N           # Lo         GUJARATI OM
            { Start = 0x0AE0; Last = 0x0AE1; Width = EastAsianWidth.OfText "N"  } // 0AE0..0AE1;N     # Lo     [2] GUJARATI LETTER VOCALIC RR..GUJARATI LETTER VOCALIC LL
            { Start = 0x0AE2; Last = 0x0AE3; Width = EastAsianWidth.OfText "N"  } // 0AE2..0AE3;N     # Mn     [2] GUJARATI VOWEL SIGN VOCALIC L..GUJARATI VOWEL SIGN VOCALIC LL
            { Start = 0x0AE6; Last = 0x0AEF; Width = EastAsianWidth.OfText "N"  } // 0AE6..0AEF;N     # Nd    [10] GUJARATI DIGIT ZERO..GUJARATI DIGIT NINE
            { Start = 0x0AF0; Last = 0x0AF0; Width = EastAsianWidth.OfText "N"  } // 0AF0;N           # Po         GUJARATI ABBREVIATION SIGN
            { Start = 0x0AF1; Last = 0x0AF1; Width = EastAsianWidth.OfText "N"  } // 0AF1;N           # Sc         GUJARATI RUPEE SIGN
            { Start = 0x0AF9; Last = 0x0AF9; Width = EastAsianWidth.OfText "N"  } // 0AF9;N           # Lo         GUJARATI LETTER ZHA
            { Start = 0x0AFA; Last = 0x0AFF; Width = EastAsianWidth.OfText "N"  } // 0AFA..0AFF;N     # Mn     [6] GUJARATI SIGN SUKUN..GUJARATI SIGN TWO-CIRCLE NUKTA ABOVE
            { Start = 0x0B01; Last = 0x0B01; Width = EastAsianWidth.OfText "N"  } // 0B01;N           # Mn         ORIYA SIGN CANDRABINDU
            { Start = 0x0B02; Last = 0x0B03; Width = EastAsianWidth.OfText "N"  } // 0B02..0B03;N     # Mc     [2] ORIYA SIGN ANUSVARA..ORIYA SIGN VISARGA
            { Start = 0x0B05; Last = 0x0B0C; Width = EastAsianWidth.OfText "N"  } // 0B05..0B0C;N     # Lo     [8] ORIYA LETTER A..ORIYA LETTER VOCALIC L
            { Start = 0x0B0F; Last = 0x0B10; Width = EastAsianWidth.OfText "N"  } // 0B0F..0B10;N     # Lo     [2] ORIYA LETTER E..ORIYA LETTER AI
            { Start = 0x0B13; Last = 0x0B28; Width = EastAsianWidth.OfText "N"  } // 0B13..0B28;N     # Lo    [22] ORIYA LETTER O..ORIYA LETTER NA
            { Start = 0x0B2A; Last = 0x0B30; Width = EastAsianWidth.OfText "N"  } // 0B2A..0B30;N     # Lo     [7] ORIYA LETTER PA..ORIYA LETTER RA
            { Start = 0x0B32; Last = 0x0B33; Width = EastAsianWidth.OfText "N"  } // 0B32..0B33;N     # Lo     [2] ORIYA LETTER LA..ORIYA LETTER LLA
            { Start = 0x0B35; Last = 0x0B39; Width = EastAsianWidth.OfText "N"  } // 0B35..0B39;N     # Lo     [5] ORIYA LETTER VA..ORIYA LETTER HA
            { Start = 0x0B3C; Last = 0x0B3C; Width = EastAsianWidth.OfText "N"  } // 0B3C;N           # Mn         ORIYA SIGN NUKTA
            { Start = 0x0B3D; Last = 0x0B3D; Width = EastAsianWidth.OfText "N"  } // 0B3D;N           # Lo         ORIYA SIGN AVAGRAHA
            { Start = 0x0B3E; Last = 0x0B3E; Width = EastAsianWidth.OfText "N"  } // 0B3E;N           # Mc         ORIYA VOWEL SIGN AA
            { Start = 0x0B3F; Last = 0x0B3F; Width = EastAsianWidth.OfText "N"  } // 0B3F;N           # Mn         ORIYA VOWEL SIGN I
            { Start = 0x0B40; Last = 0x0B40; Width = EastAsianWidth.OfText "N"  } // 0B40;N           # Mc         ORIYA VOWEL SIGN II
            { Start = 0x0B41; Last = 0x0B44; Width = EastAsianWidth.OfText "N"  } // 0B41..0B44;N     # Mn     [4] ORIYA VOWEL SIGN U..ORIYA VOWEL SIGN VOCALIC RR
            { Start = 0x0B47; Last = 0x0B48; Width = EastAsianWidth.OfText "N"  } // 0B47..0B48;N     # Mc     [2] ORIYA VOWEL SIGN E..ORIYA VOWEL SIGN AI
            { Start = 0x0B4B; Last = 0x0B4C; Width = EastAsianWidth.OfText "N"  } // 0B4B..0B4C;N     # Mc     [2] ORIYA VOWEL SIGN O..ORIYA VOWEL SIGN AU
            { Start = 0x0B4D; Last = 0x0B4D; Width = EastAsianWidth.OfText "N"  } // 0B4D;N           # Mn         ORIYA SIGN VIRAMA
            { Start = 0x0B56; Last = 0x0B56; Width = EastAsianWidth.OfText "N"  } // 0B56;N           # Mn         ORIYA AI LENGTH MARK
            { Start = 0x0B57; Last = 0x0B57; Width = EastAsianWidth.OfText "N"  } // 0B57;N           # Mc         ORIYA AU LENGTH MARK
            { Start = 0x0B5C; Last = 0x0B5D; Width = EastAsianWidth.OfText "N"  } // 0B5C..0B5D;N     # Lo     [2] ORIYA LETTER RRA..ORIYA LETTER RHA
            { Start = 0x0B5F; Last = 0x0B61; Width = EastAsianWidth.OfText "N"  } // 0B5F..0B61;N     # Lo     [3] ORIYA LETTER YYA..ORIYA LETTER VOCALIC LL
            { Start = 0x0B62; Last = 0x0B63; Width = EastAsianWidth.OfText "N"  } // 0B62..0B63;N     # Mn     [2] ORIYA VOWEL SIGN VOCALIC L..ORIYA VOWEL SIGN VOCALIC LL
            { Start = 0x0B66; Last = 0x0B6F; Width = EastAsianWidth.OfText "N"  } // 0B66..0B6F;N     # Nd    [10] ORIYA DIGIT ZERO..ORIYA DIGIT NINE
            { Start = 0x0B70; Last = 0x0B70; Width = EastAsianWidth.OfText "N"  } // 0B70;N           # So         ORIYA ISSHAR
            { Start = 0x0B71; Last = 0x0B71; Width = EastAsianWidth.OfText "N"  } // 0B71;N           # Lo         ORIYA LETTER WA
            { Start = 0x0B72; Last = 0x0B77; Width = EastAsianWidth.OfText "N"  } // 0B72..0B77;N     # No     [6] ORIYA FRACTION ONE QUARTER..ORIYA FRACTION THREE SIXTEENTHS
            { Start = 0x0B82; Last = 0x0B82; Width = EastAsianWidth.OfText "N"  } // 0B82;N           # Mn         TAMIL SIGN ANUSVARA
            { Start = 0x0B83; Last = 0x0B83; Width = EastAsianWidth.OfText "N"  } // 0B83;N           # Lo         TAMIL SIGN VISARGA
            { Start = 0x0B85; Last = 0x0B8A; Width = EastAsianWidth.OfText "N"  } // 0B85..0B8A;N     # Lo     [6] TAMIL LETTER A..TAMIL LETTER UU
            { Start = 0x0B8E; Last = 0x0B90; Width = EastAsianWidth.OfText "N"  } // 0B8E..0B90;N     # Lo     [3] TAMIL LETTER E..TAMIL LETTER AI
            { Start = 0x0B92; Last = 0x0B95; Width = EastAsianWidth.OfText "N"  } // 0B92..0B95;N     # Lo     [4] TAMIL LETTER O..TAMIL LETTER KA
            { Start = 0x0B99; Last = 0x0B9A; Width = EastAsianWidth.OfText "N"  } // 0B99..0B9A;N     # Lo     [2] TAMIL LETTER NGA..TAMIL LETTER CA
            { Start = 0x0B9C; Last = 0x0B9C; Width = EastAsianWidth.OfText "N"  } // 0B9C;N           # Lo         TAMIL LETTER JA
            { Start = 0x0B9E; Last = 0x0B9F; Width = EastAsianWidth.OfText "N"  } // 0B9E..0B9F;N     # Lo     [2] TAMIL LETTER NYA..TAMIL LETTER TTA
            { Start = 0x0BA3; Last = 0x0BA4; Width = EastAsianWidth.OfText "N"  } // 0BA3..0BA4;N     # Lo     [2] TAMIL LETTER NNA..TAMIL LETTER TA
            { Start = 0x0BA8; Last = 0x0BAA; Width = EastAsianWidth.OfText "N"  } // 0BA8..0BAA;N     # Lo     [3] TAMIL LETTER NA..TAMIL LETTER PA
            { Start = 0x0BAE; Last = 0x0BB9; Width = EastAsianWidth.OfText "N"  } // 0BAE..0BB9;N     # Lo    [12] TAMIL LETTER MA..TAMIL LETTER HA
            { Start = 0x0BBE; Last = 0x0BBF; Width = EastAsianWidth.OfText "N"  } // 0BBE..0BBF;N     # Mc     [2] TAMIL VOWEL SIGN AA..TAMIL VOWEL SIGN I
            { Start = 0x0BC0; Last = 0x0BC0; Width = EastAsianWidth.OfText "N"  } // 0BC0;N           # Mn         TAMIL VOWEL SIGN II
            { Start = 0x0BC1; Last = 0x0BC2; Width = EastAsianWidth.OfText "N"  } // 0BC1..0BC2;N     # Mc     [2] TAMIL VOWEL SIGN U..TAMIL VOWEL SIGN UU
            { Start = 0x0BC6; Last = 0x0BC8; Width = EastAsianWidth.OfText "N"  } // 0BC6..0BC8;N     # Mc     [3] TAMIL VOWEL SIGN E..TAMIL VOWEL SIGN AI
            { Start = 0x0BCA; Last = 0x0BCC; Width = EastAsianWidth.OfText "N"  } // 0BCA..0BCC;N     # Mc     [3] TAMIL VOWEL SIGN O..TAMIL VOWEL SIGN AU
            { Start = 0x0BCD; Last = 0x0BCD; Width = EastAsianWidth.OfText "N"  } // 0BCD;N           # Mn         TAMIL SIGN VIRAMA
            { Start = 0x0BD0; Last = 0x0BD0; Width = EastAsianWidth.OfText "N"  } // 0BD0;N           # Lo         TAMIL OM
            { Start = 0x0BD7; Last = 0x0BD7; Width = EastAsianWidth.OfText "N"  } // 0BD7;N           # Mc         TAMIL AU LENGTH MARK
            { Start = 0x0BE6; Last = 0x0BEF; Width = EastAsianWidth.OfText "N"  } // 0BE6..0BEF;N     # Nd    [10] TAMIL DIGIT ZERO..TAMIL DIGIT NINE
            { Start = 0x0BF0; Last = 0x0BF2; Width = EastAsianWidth.OfText "N"  } // 0BF0..0BF2;N     # No     [3] TAMIL NUMBER TEN..TAMIL NUMBER ONE THOUSAND
            { Start = 0x0BF3; Last = 0x0BF8; Width = EastAsianWidth.OfText "N"  } // 0BF3..0BF8;N     # So     [6] TAMIL DAY SIGN..TAMIL AS ABOVE SIGN
            { Start = 0x0BF9; Last = 0x0BF9; Width = EastAsianWidth.OfText "N"  } // 0BF9;N           # Sc         TAMIL RUPEE SIGN
            { Start = 0x0BFA; Last = 0x0BFA; Width = EastAsianWidth.OfText "N"  } // 0BFA;N           # So         TAMIL NUMBER SIGN
            { Start = 0x0C00; Last = 0x0C00; Width = EastAsianWidth.OfText "N"  } // 0C00;N           # Mn         TELUGU SIGN COMBINING CANDRABINDU ABOVE
            { Start = 0x0C01; Last = 0x0C03; Width = EastAsianWidth.OfText "N"  } // 0C01..0C03;N     # Mc     [3] TELUGU SIGN CANDRABINDU..TELUGU SIGN VISARGA
            { Start = 0x0C04; Last = 0x0C04; Width = EastAsianWidth.OfText "N"  } // 0C04;N           # Mn         TELUGU SIGN COMBINING ANUSVARA ABOVE
            { Start = 0x0C05; Last = 0x0C0C; Width = EastAsianWidth.OfText "N"  } // 0C05..0C0C;N     # Lo     [8] TELUGU LETTER A..TELUGU LETTER VOCALIC L
            { Start = 0x0C0E; Last = 0x0C10; Width = EastAsianWidth.OfText "N"  } // 0C0E..0C10;N     # Lo     [3] TELUGU LETTER E..TELUGU LETTER AI
            { Start = 0x0C12; Last = 0x0C28; Width = EastAsianWidth.OfText "N"  } // 0C12..0C28;N     # Lo    [23] TELUGU LETTER O..TELUGU LETTER NA
            { Start = 0x0C2A; Last = 0x0C39; Width = EastAsianWidth.OfText "N"  } // 0C2A..0C39;N     # Lo    [16] TELUGU LETTER PA..TELUGU LETTER HA
            { Start = 0x0C3D; Last = 0x0C3D; Width = EastAsianWidth.OfText "N"  } // 0C3D;N           # Lo         TELUGU SIGN AVAGRAHA
            { Start = 0x0C3E; Last = 0x0C40; Width = EastAsianWidth.OfText "N"  } // 0C3E..0C40;N     # Mn     [3] TELUGU VOWEL SIGN AA..TELUGU VOWEL SIGN II
            { Start = 0x0C41; Last = 0x0C44; Width = EastAsianWidth.OfText "N"  } // 0C41..0C44;N     # Mc     [4] TELUGU VOWEL SIGN U..TELUGU VOWEL SIGN VOCALIC RR
            { Start = 0x0C46; Last = 0x0C48; Width = EastAsianWidth.OfText "N"  } // 0C46..0C48;N     # Mn     [3] TELUGU VOWEL SIGN E..TELUGU VOWEL SIGN AI
            { Start = 0x0C4A; Last = 0x0C4D; Width = EastAsianWidth.OfText "N"  } // 0C4A..0C4D;N     # Mn     [4] TELUGU VOWEL SIGN O..TELUGU SIGN VIRAMA
            { Start = 0x0C55; Last = 0x0C56; Width = EastAsianWidth.OfText "N"  } // 0C55..0C56;N     # Mn     [2] TELUGU LENGTH MARK..TELUGU AI LENGTH MARK
            { Start = 0x0C58; Last = 0x0C5A; Width = EastAsianWidth.OfText "N"  } // 0C58..0C5A;N     # Lo     [3] TELUGU LETTER TSA..TELUGU LETTER RRRA
            { Start = 0x0C60; Last = 0x0C61; Width = EastAsianWidth.OfText "N"  } // 0C60..0C61;N     # Lo     [2] TELUGU LETTER VOCALIC RR..TELUGU LETTER VOCALIC LL
            { Start = 0x0C62; Last = 0x0C63; Width = EastAsianWidth.OfText "N"  } // 0C62..0C63;N     # Mn     [2] TELUGU VOWEL SIGN VOCALIC L..TELUGU VOWEL SIGN VOCALIC LL
            { Start = 0x0C66; Last = 0x0C6F; Width = EastAsianWidth.OfText "N"  } // 0C66..0C6F;N     # Nd    [10] TELUGU DIGIT ZERO..TELUGU DIGIT NINE
            { Start = 0x0C78; Last = 0x0C7E; Width = EastAsianWidth.OfText "N"  } // 0C78..0C7E;N     # No     [7] TELUGU FRACTION DIGIT ZERO FOR ODD POWERS OF FOUR..TELUGU FRACTION DIGIT THREE FOR EVEN POWERS OF FOUR
            { Start = 0x0C7F; Last = 0x0C7F; Width = EastAsianWidth.OfText "N"  } // 0C7F;N           # So         TELUGU SIGN TUUMU
            { Start = 0x0C80; Last = 0x0C80; Width = EastAsianWidth.OfText "N"  } // 0C80;N           # Lo         KANNADA SIGN SPACING CANDRABINDU
            { Start = 0x0C81; Last = 0x0C81; Width = EastAsianWidth.OfText "N"  } // 0C81;N           # Mn         KANNADA SIGN CANDRABINDU
            { Start = 0x0C82; Last = 0x0C83; Width = EastAsianWidth.OfText "N"  } // 0C82..0C83;N     # Mc     [2] KANNADA SIGN ANUSVARA..KANNADA SIGN VISARGA
            { Start = 0x0C84; Last = 0x0C84; Width = EastAsianWidth.OfText "N"  } // 0C84;N           # Po         KANNADA SIGN SIDDHAM
            { Start = 0x0C85; Last = 0x0C8C; Width = EastAsianWidth.OfText "N"  } // 0C85..0C8C;N     # Lo     [8] KANNADA LETTER A..KANNADA LETTER VOCALIC L
            { Start = 0x0C8E; Last = 0x0C90; Width = EastAsianWidth.OfText "N"  } // 0C8E..0C90;N     # Lo     [3] KANNADA LETTER E..KANNADA LETTER AI
            { Start = 0x0C92; Last = 0x0CA8; Width = EastAsianWidth.OfText "N"  } // 0C92..0CA8;N     # Lo    [23] KANNADA LETTER O..KANNADA LETTER NA
            { Start = 0x0CAA; Last = 0x0CB3; Width = EastAsianWidth.OfText "N"  } // 0CAA..0CB3;N     # Lo    [10] KANNADA LETTER PA..KANNADA LETTER LLA
            { Start = 0x0CB5; Last = 0x0CB9; Width = EastAsianWidth.OfText "N"  } // 0CB5..0CB9;N     # Lo     [5] KANNADA LETTER VA..KANNADA LETTER HA
            { Start = 0x0CBC; Last = 0x0CBC; Width = EastAsianWidth.OfText "N"  } // 0CBC;N           # Mn         KANNADA SIGN NUKTA
            { Start = 0x0CBD; Last = 0x0CBD; Width = EastAsianWidth.OfText "N"  } // 0CBD;N           # Lo         KANNADA SIGN AVAGRAHA
            { Start = 0x0CBE; Last = 0x0CBE; Width = EastAsianWidth.OfText "N"  } // 0CBE;N           # Mc         KANNADA VOWEL SIGN AA
            { Start = 0x0CBF; Last = 0x0CBF; Width = EastAsianWidth.OfText "N"  } // 0CBF;N           # Mn         KANNADA VOWEL SIGN I
            { Start = 0x0CC0; Last = 0x0CC4; Width = EastAsianWidth.OfText "N"  } // 0CC0..0CC4;N     # Mc     [5] KANNADA VOWEL SIGN II..KANNADA VOWEL SIGN VOCALIC RR
            { Start = 0x0CC6; Last = 0x0CC6; Width = EastAsianWidth.OfText "N"  } // 0CC6;N           # Mn         KANNADA VOWEL SIGN E
            { Start = 0x0CC7; Last = 0x0CC8; Width = EastAsianWidth.OfText "N"  } // 0CC7..0CC8;N     # Mc     [2] KANNADA VOWEL SIGN EE..KANNADA VOWEL SIGN AI
            { Start = 0x0CCA; Last = 0x0CCB; Width = EastAsianWidth.OfText "N"  } // 0CCA..0CCB;N     # Mc     [2] KANNADA VOWEL SIGN O..KANNADA VOWEL SIGN OO
            { Start = 0x0CCC; Last = 0x0CCD; Width = EastAsianWidth.OfText "N"  } // 0CCC..0CCD;N     # Mn     [2] KANNADA VOWEL SIGN AU..KANNADA SIGN VIRAMA
            { Start = 0x0CD5; Last = 0x0CD6; Width = EastAsianWidth.OfText "N"  } // 0CD5..0CD6;N     # Mc     [2] KANNADA LENGTH MARK..KANNADA AI LENGTH MARK
            { Start = 0x0CDE; Last = 0x0CDE; Width = EastAsianWidth.OfText "N"  } // 0CDE;N           # Lo         KANNADA LETTER FA
            { Start = 0x0CE0; Last = 0x0CE1; Width = EastAsianWidth.OfText "N"  } // 0CE0..0CE1;N     # Lo     [2] KANNADA LETTER VOCALIC RR..KANNADA LETTER VOCALIC LL
            { Start = 0x0CE2; Last = 0x0CE3; Width = EastAsianWidth.OfText "N"  } // 0CE2..0CE3;N     # Mn     [2] KANNADA VOWEL SIGN VOCALIC L..KANNADA VOWEL SIGN VOCALIC LL
            { Start = 0x0CE6; Last = 0x0CEF; Width = EastAsianWidth.OfText "N"  } // 0CE6..0CEF;N     # Nd    [10] KANNADA DIGIT ZERO..KANNADA DIGIT NINE
            { Start = 0x0CF1; Last = 0x0CF2; Width = EastAsianWidth.OfText "N"  } // 0CF1..0CF2;N     # Lo     [2] KANNADA SIGN JIHVAMULIYA..KANNADA SIGN UPADHMANIYA
            { Start = 0x0D00; Last = 0x0D01; Width = EastAsianWidth.OfText "N"  } // 0D00..0D01;N     # Mn     [2] MALAYALAM SIGN COMBINING ANUSVARA ABOVE..MALAYALAM SIGN CANDRABINDU
            { Start = 0x0D02; Last = 0x0D03; Width = EastAsianWidth.OfText "N"  } // 0D02..0D03;N     # Mc     [2] MALAYALAM SIGN ANUSVARA..MALAYALAM SIGN VISARGA
            { Start = 0x0D05; Last = 0x0D0C; Width = EastAsianWidth.OfText "N"  } // 0D05..0D0C;N     # Lo     [8] MALAYALAM LETTER A..MALAYALAM LETTER VOCALIC L
            { Start = 0x0D0E; Last = 0x0D10; Width = EastAsianWidth.OfText "N"  } // 0D0E..0D10;N     # Lo     [3] MALAYALAM LETTER E..MALAYALAM LETTER AI
            { Start = 0x0D12; Last = 0x0D3A; Width = EastAsianWidth.OfText "N"  } // 0D12..0D3A;N     # Lo    [41] MALAYALAM LETTER O..MALAYALAM LETTER TTTA
            { Start = 0x0D3B; Last = 0x0D3C; Width = EastAsianWidth.OfText "N"  } // 0D3B..0D3C;N     # Mn     [2] MALAYALAM SIGN VERTICAL BAR VIRAMA..MALAYALAM SIGN CIRCULAR VIRAMA
            { Start = 0x0D3D; Last = 0x0D3D; Width = EastAsianWidth.OfText "N"  } // 0D3D;N           # Lo         MALAYALAM SIGN AVAGRAHA
            { Start = 0x0D3E; Last = 0x0D40; Width = EastAsianWidth.OfText "N"  } // 0D3E..0D40;N     # Mc     [3] MALAYALAM VOWEL SIGN AA..MALAYALAM VOWEL SIGN II
            { Start = 0x0D41; Last = 0x0D44; Width = EastAsianWidth.OfText "N"  } // 0D41..0D44;N     # Mn     [4] MALAYALAM VOWEL SIGN U..MALAYALAM VOWEL SIGN VOCALIC RR
            { Start = 0x0D46; Last = 0x0D48; Width = EastAsianWidth.OfText "N"  } // 0D46..0D48;N     # Mc     [3] MALAYALAM VOWEL SIGN E..MALAYALAM VOWEL SIGN AI
            { Start = 0x0D4A; Last = 0x0D4C; Width = EastAsianWidth.OfText "N"  } // 0D4A..0D4C;N     # Mc     [3] MALAYALAM VOWEL SIGN O..MALAYALAM VOWEL SIGN AU
            { Start = 0x0D4D; Last = 0x0D4D; Width = EastAsianWidth.OfText "N"  } // 0D4D;N           # Mn         MALAYALAM SIGN VIRAMA
            { Start = 0x0D4E; Last = 0x0D4E; Width = EastAsianWidth.OfText "N"  } // 0D4E;N           # Lo         MALAYALAM LETTER DOT REPH
            { Start = 0x0D4F; Last = 0x0D4F; Width = EastAsianWidth.OfText "N"  } // 0D4F;N           # So         MALAYALAM SIGN PARA
            { Start = 0x0D54; Last = 0x0D56; Width = EastAsianWidth.OfText "N"  } // 0D54..0D56;N     # Lo     [3] MALAYALAM LETTER CHILLU M..MALAYALAM LETTER CHILLU LLL
            { Start = 0x0D57; Last = 0x0D57; Width = EastAsianWidth.OfText "N"  } // 0D57;N           # Mc         MALAYALAM AU LENGTH MARK
            { Start = 0x0D58; Last = 0x0D5E; Width = EastAsianWidth.OfText "N"  } // 0D58..0D5E;N     # No     [7] MALAYALAM FRACTION ONE ONE-HUNDRED-AND-SIXTIETH..MALAYALAM FRACTION ONE FIFTH
            { Start = 0x0D5F; Last = 0x0D61; Width = EastAsianWidth.OfText "N"  } // 0D5F..0D61;N     # Lo     [3] MALAYALAM LETTER ARCHAIC II..MALAYALAM LETTER VOCALIC LL
            { Start = 0x0D62; Last = 0x0D63; Width = EastAsianWidth.OfText "N"  } // 0D62..0D63;N     # Mn     [2] MALAYALAM VOWEL SIGN VOCALIC L..MALAYALAM VOWEL SIGN VOCALIC LL
            { Start = 0x0D66; Last = 0x0D6F; Width = EastAsianWidth.OfText "N"  } // 0D66..0D6F;N     # Nd    [10] MALAYALAM DIGIT ZERO..MALAYALAM DIGIT NINE
            { Start = 0x0D70; Last = 0x0D78; Width = EastAsianWidth.OfText "N"  } // 0D70..0D78;N     # No     [9] MALAYALAM NUMBER TEN..MALAYALAM FRACTION THREE SIXTEENTHS
            { Start = 0x0D79; Last = 0x0D79; Width = EastAsianWidth.OfText "N"  } // 0D79;N           # So         MALAYALAM DATE MARK
            { Start = 0x0D7A; Last = 0x0D7F; Width = EastAsianWidth.OfText "N"  } // 0D7A..0D7F;N     # Lo     [6] MALAYALAM LETTER CHILLU NN..MALAYALAM LETTER CHILLU K
            { Start = 0x0D82; Last = 0x0D83; Width = EastAsianWidth.OfText "N"  } // 0D82..0D83;N     # Mc     [2] SINHALA SIGN ANUSVARAYA..SINHALA SIGN VISARGAYA
            { Start = 0x0D85; Last = 0x0D96; Width = EastAsianWidth.OfText "N"  } // 0D85..0D96;N     # Lo    [18] SINHALA LETTER AYANNA..SINHALA LETTER AUYANNA
            { Start = 0x0D9A; Last = 0x0DB1; Width = EastAsianWidth.OfText "N"  } // 0D9A..0DB1;N     # Lo    [24] SINHALA LETTER ALPAPRAANA KAYANNA..SINHALA LETTER DANTAJA NAYANNA
            { Start = 0x0DB3; Last = 0x0DBB; Width = EastAsianWidth.OfText "N"  } // 0DB3..0DBB;N     # Lo     [9] SINHALA LETTER SANYAKA DAYANNA..SINHALA LETTER RAYANNA
            { Start = 0x0DBD; Last = 0x0DBD; Width = EastAsianWidth.OfText "N"  } // 0DBD;N           # Lo         SINHALA LETTER DANTAJA LAYANNA
            { Start = 0x0DC0; Last = 0x0DC6; Width = EastAsianWidth.OfText "N"  } // 0DC0..0DC6;N     # Lo     [7] SINHALA LETTER VAYANNA..SINHALA LETTER FAYANNA
            { Start = 0x0DCA; Last = 0x0DCA; Width = EastAsianWidth.OfText "N"  } // 0DCA;N           # Mn         SINHALA SIGN AL-LAKUNA
            { Start = 0x0DCF; Last = 0x0DD1; Width = EastAsianWidth.OfText "N"  } // 0DCF..0DD1;N     # Mc     [3] SINHALA VOWEL SIGN AELA-PILLA..SINHALA VOWEL SIGN DIGA AEDA-PILLA
            { Start = 0x0DD2; Last = 0x0DD4; Width = EastAsianWidth.OfText "N"  } // 0DD2..0DD4;N     # Mn     [3] SINHALA VOWEL SIGN KETTI IS-PILLA..SINHALA VOWEL SIGN KETTI PAA-PILLA
            { Start = 0x0DD6; Last = 0x0DD6; Width = EastAsianWidth.OfText "N"  } // 0DD6;N           # Mn         SINHALA VOWEL SIGN DIGA PAA-PILLA
            { Start = 0x0DD8; Last = 0x0DDF; Width = EastAsianWidth.OfText "N"  } // 0DD8..0DDF;N     # Mc     [8] SINHALA VOWEL SIGN GAETTA-PILLA..SINHALA VOWEL SIGN GAYANUKITTA
            { Start = 0x0DE6; Last = 0x0DEF; Width = EastAsianWidth.OfText "N"  } // 0DE6..0DEF;N     # Nd    [10] SINHALA LITH DIGIT ZERO..SINHALA LITH DIGIT NINE
            { Start = 0x0DF2; Last = 0x0DF3; Width = EastAsianWidth.OfText "N"  } // 0DF2..0DF3;N     # Mc     [2] SINHALA VOWEL SIGN DIGA GAETTA-PILLA..SINHALA VOWEL SIGN DIGA GAYANUKITTA
            { Start = 0x0DF4; Last = 0x0DF4; Width = EastAsianWidth.OfText "N"  } // 0DF4;N           # Po         SINHALA PUNCTUATION KUNDDALIYA
            { Start = 0x0E01; Last = 0x0E30; Width = EastAsianWidth.OfText "N"  } // 0E01..0E30;N     # Lo    [48] THAI CHARACTER KO KAI..THAI CHARACTER SARA A
            { Start = 0x0E31; Last = 0x0E31; Width = EastAsianWidth.OfText "N"  } // 0E31;N           # Mn         THAI CHARACTER MAI HAN-AKAT
            { Start = 0x0E32; Last = 0x0E33; Width = EastAsianWidth.OfText "N"  } // 0E32..0E33;N     # Lo     [2] THAI CHARACTER SARA AA..THAI CHARACTER SARA AM
            { Start = 0x0E34; Last = 0x0E3A; Width = EastAsianWidth.OfText "N"  } // 0E34..0E3A;N     # Mn     [7] THAI CHARACTER SARA I..THAI CHARACTER PHINTHU
            { Start = 0x0E3F; Last = 0x0E3F; Width = EastAsianWidth.OfText "N"  } // 0E3F;N           # Sc         THAI CURRENCY SYMBOL BAHT
            { Start = 0x0E40; Last = 0x0E45; Width = EastAsianWidth.OfText "N"  } // 0E40..0E45;N     # Lo     [6] THAI CHARACTER SARA E..THAI CHARACTER LAKKHANGYAO
            { Start = 0x0E46; Last = 0x0E46; Width = EastAsianWidth.OfText "N"  } // 0E46;N           # Lm         THAI CHARACTER MAIYAMOK
            { Start = 0x0E47; Last = 0x0E4E; Width = EastAsianWidth.OfText "N"  } // 0E47..0E4E;N     # Mn     [8] THAI CHARACTER MAITAIKHU..THAI CHARACTER YAMAKKAN
            { Start = 0x0E4F; Last = 0x0E4F; Width = EastAsianWidth.OfText "N"  } // 0E4F;N           # Po         THAI CHARACTER FONGMAN
            { Start = 0x0E50; Last = 0x0E59; Width = EastAsianWidth.OfText "N"  } // 0E50..0E59;N     # Nd    [10] THAI DIGIT ZERO..THAI DIGIT NINE
            { Start = 0x0E5A; Last = 0x0E5B; Width = EastAsianWidth.OfText "N"  } // 0E5A..0E5B;N     # Po     [2] THAI CHARACTER ANGKHANKHU..THAI CHARACTER KHOMUT
            { Start = 0x0E81; Last = 0x0E82; Width = EastAsianWidth.OfText "N"  } // 0E81..0E82;N     # Lo     [2] LAO LETTER KO..LAO LETTER KHO SUNG
            { Start = 0x0E84; Last = 0x0E84; Width = EastAsianWidth.OfText "N"  } // 0E84;N           # Lo         LAO LETTER KHO TAM
            { Start = 0x0E87; Last = 0x0E88; Width = EastAsianWidth.OfText "N"  } // 0E87..0E88;N     # Lo     [2] LAO LETTER NGO..LAO LETTER CO
            { Start = 0x0E8A; Last = 0x0E8A; Width = EastAsianWidth.OfText "N"  } // 0E8A;N           # Lo         LAO LETTER SO TAM
            { Start = 0x0E8D; Last = 0x0E8D; Width = EastAsianWidth.OfText "N"  } // 0E8D;N           # Lo         LAO LETTER NYO
            { Start = 0x0E94; Last = 0x0E97; Width = EastAsianWidth.OfText "N"  } // 0E94..0E97;N     # Lo     [4] LAO LETTER DO..LAO LETTER THO TAM
            { Start = 0x0E99; Last = 0x0E9F; Width = EastAsianWidth.OfText "N"  } // 0E99..0E9F;N     # Lo     [7] LAO LETTER NO..LAO LETTER FO SUNG
            { Start = 0x0EA1; Last = 0x0EA3; Width = EastAsianWidth.OfText "N"  } // 0EA1..0EA3;N     # Lo     [3] LAO LETTER MO..LAO LETTER LO LING
            { Start = 0x0EA5; Last = 0x0EA5; Width = EastAsianWidth.OfText "N"  } // 0EA5;N           # Lo         LAO LETTER LO LOOT
            { Start = 0x0EA7; Last = 0x0EA7; Width = EastAsianWidth.OfText "N"  } // 0EA7;N           # Lo         LAO LETTER WO
            { Start = 0x0EAA; Last = 0x0EAB; Width = EastAsianWidth.OfText "N"  } // 0EAA..0EAB;N     # Lo     [2] LAO LETTER SO SUNG..LAO LETTER HO SUNG
            { Start = 0x0EAD; Last = 0x0EB0; Width = EastAsianWidth.OfText "N"  } // 0EAD..0EB0;N     # Lo     [4] LAO LETTER O..LAO VOWEL SIGN A
            { Start = 0x0EB1; Last = 0x0EB1; Width = EastAsianWidth.OfText "N"  } // 0EB1;N           # Mn         LAO VOWEL SIGN MAI KAN
            { Start = 0x0EB2; Last = 0x0EB3; Width = EastAsianWidth.OfText "N"  } // 0EB2..0EB3;N     # Lo     [2] LAO VOWEL SIGN AA..LAO VOWEL SIGN AM
            { Start = 0x0EB4; Last = 0x0EB9; Width = EastAsianWidth.OfText "N"  } // 0EB4..0EB9;N     # Mn     [6] LAO VOWEL SIGN I..LAO VOWEL SIGN UU
            { Start = 0x0EBB; Last = 0x0EBC; Width = EastAsianWidth.OfText "N"  } // 0EBB..0EBC;N     # Mn     [2] LAO VOWEL SIGN MAI KON..LAO SEMIVOWEL SIGN LO
            { Start = 0x0EBD; Last = 0x0EBD; Width = EastAsianWidth.OfText "N"  } // 0EBD;N           # Lo         LAO SEMIVOWEL SIGN NYO
            { Start = 0x0EC0; Last = 0x0EC4; Width = EastAsianWidth.OfText "N"  } // 0EC0..0EC4;N     # Lo     [5] LAO VOWEL SIGN E..LAO VOWEL SIGN AI
            { Start = 0x0EC6; Last = 0x0EC6; Width = EastAsianWidth.OfText "N"  } // 0EC6;N           # Lm         LAO KO LA
            { Start = 0x0EC8; Last = 0x0ECD; Width = EastAsianWidth.OfText "N"  } // 0EC8..0ECD;N     # Mn     [6] LAO TONE MAI EK..LAO NIGGAHITA
            { Start = 0x0ED0; Last = 0x0ED9; Width = EastAsianWidth.OfText "N"  } // 0ED0..0ED9;N     # Nd    [10] LAO DIGIT ZERO..LAO DIGIT NINE
            { Start = 0x0EDC; Last = 0x0EDF; Width = EastAsianWidth.OfText "N"  } // 0EDC..0EDF;N     # Lo     [4] LAO HO NO..LAO LETTER KHMU NYO
            { Start = 0x0F00; Last = 0x0F00; Width = EastAsianWidth.OfText "N"  } // 0F00;N           # Lo         TIBETAN SYLLABLE OM
            { Start = 0x0F01; Last = 0x0F03; Width = EastAsianWidth.OfText "N"  } // 0F01..0F03;N     # So     [3] TIBETAN MARK GTER YIG MGO TRUNCATED A..TIBETAN MARK GTER YIG MGO -UM GTER TSHEG MA
            { Start = 0x0F04; Last = 0x0F12; Width = EastAsianWidth.OfText "N"  } // 0F04..0F12;N     # Po    [15] TIBETAN MARK INITIAL YIG MGO MDUN MA..TIBETAN MARK RGYA GRAM SHAD
            { Start = 0x0F13; Last = 0x0F13; Width = EastAsianWidth.OfText "N"  } // 0F13;N           # So         TIBETAN MARK CARET -DZUD RTAGS ME LONG CAN
            { Start = 0x0F14; Last = 0x0F14; Width = EastAsianWidth.OfText "N"  } // 0F14;N           # Po         TIBETAN MARK GTER TSHEG
            { Start = 0x0F15; Last = 0x0F17; Width = EastAsianWidth.OfText "N"  } // 0F15..0F17;N     # So     [3] TIBETAN LOGOTYPE SIGN CHAD RTAGS..TIBETAN ASTROLOGICAL SIGN SGRA GCAN -CHAR RTAGS
            { Start = 0x0F18; Last = 0x0F19; Width = EastAsianWidth.OfText "N"  } // 0F18..0F19;N     # Mn     [2] TIBETAN ASTROLOGICAL SIGN -KHYUD PA..TIBETAN ASTROLOGICAL SIGN SDONG TSHUGS
            { Start = 0x0F1A; Last = 0x0F1F; Width = EastAsianWidth.OfText "N"  } // 0F1A..0F1F;N     # So     [6] TIBETAN SIGN RDEL DKAR GCIG..TIBETAN SIGN RDEL DKAR RDEL NAG
            { Start = 0x0F20; Last = 0x0F29; Width = EastAsianWidth.OfText "N"  } // 0F20..0F29;N     # Nd    [10] TIBETAN DIGIT ZERO..TIBETAN DIGIT NINE
            { Start = 0x0F2A; Last = 0x0F33; Width = EastAsianWidth.OfText "N"  } // 0F2A..0F33;N     # No    [10] TIBETAN DIGIT HALF ONE..TIBETAN DIGIT HALF ZERO
            { Start = 0x0F34; Last = 0x0F34; Width = EastAsianWidth.OfText "N"  } // 0F34;N           # So         TIBETAN MARK BSDUS RTAGS
            { Start = 0x0F35; Last = 0x0F35; Width = EastAsianWidth.OfText "N"  } // 0F35;N           # Mn         TIBETAN MARK NGAS BZUNG NYI ZLA
            { Start = 0x0F36; Last = 0x0F36; Width = EastAsianWidth.OfText "N"  } // 0F36;N           # So         TIBETAN MARK CARET -DZUD RTAGS BZHI MIG CAN
            { Start = 0x0F37; Last = 0x0F37; Width = EastAsianWidth.OfText "N"  } // 0F37;N           # Mn         TIBETAN MARK NGAS BZUNG SGOR RTAGS
            { Start = 0x0F38; Last = 0x0F38; Width = EastAsianWidth.OfText "N"  } // 0F38;N           # So         TIBETAN MARK CHE MGO
            { Start = 0x0F39; Last = 0x0F39; Width = EastAsianWidth.OfText "N"  } // 0F39;N           # Mn         TIBETAN MARK TSA -PHRU
            { Start = 0x0F3A; Last = 0x0F3A; Width = EastAsianWidth.OfText "N"  } // 0F3A;N           # Ps         TIBETAN MARK GUG RTAGS GYON
            { Start = 0x0F3B; Last = 0x0F3B; Width = EastAsianWidth.OfText "N"  } // 0F3B;N           # Pe         TIBETAN MARK GUG RTAGS GYAS
            { Start = 0x0F3C; Last = 0x0F3C; Width = EastAsianWidth.OfText "N"  } // 0F3C;N           # Ps         TIBETAN MARK ANG KHANG GYON
            { Start = 0x0F3D; Last = 0x0F3D; Width = EastAsianWidth.OfText "N"  } // 0F3D;N           # Pe         TIBETAN MARK ANG KHANG GYAS
            { Start = 0x0F3E; Last = 0x0F3F; Width = EastAsianWidth.OfText "N"  } // 0F3E..0F3F;N     # Mc     [2] TIBETAN SIGN YAR TSHES..TIBETAN SIGN MAR TSHES
            { Start = 0x0F40; Last = 0x0F47; Width = EastAsianWidth.OfText "N"  } // 0F40..0F47;N     # Lo     [8] TIBETAN LETTER KA..TIBETAN LETTER JA
            { Start = 0x0F49; Last = 0x0F6C; Width = EastAsianWidth.OfText "N"  } // 0F49..0F6C;N     # Lo    [36] TIBETAN LETTER NYA..TIBETAN LETTER RRA
            { Start = 0x0F71; Last = 0x0F7E; Width = EastAsianWidth.OfText "N"  } // 0F71..0F7E;N     # Mn    [14] TIBETAN VOWEL SIGN AA..TIBETAN SIGN RJES SU NGA RO
            { Start = 0x0F7F; Last = 0x0F7F; Width = EastAsianWidth.OfText "N"  } // 0F7F;N           # Mc         TIBETAN SIGN RNAM BCAD
            { Start = 0x0F80; Last = 0x0F84; Width = EastAsianWidth.OfText "N"  } // 0F80..0F84;N     # Mn     [5] TIBETAN VOWEL SIGN REVERSED I..TIBETAN MARK HALANTA
            { Start = 0x0F85; Last = 0x0F85; Width = EastAsianWidth.OfText "N"  } // 0F85;N           # Po         TIBETAN MARK PALUTA
            { Start = 0x0F86; Last = 0x0F87; Width = EastAsianWidth.OfText "N"  } // 0F86..0F87;N     # Mn     [2] TIBETAN SIGN LCI RTAGS..TIBETAN SIGN YANG RTAGS
            { Start = 0x0F88; Last = 0x0F8C; Width = EastAsianWidth.OfText "N"  } // 0F88..0F8C;N     # Lo     [5] TIBETAN SIGN LCE TSA CAN..TIBETAN SIGN INVERTED MCHU CAN
            { Start = 0x0F8D; Last = 0x0F97; Width = EastAsianWidth.OfText "N"  } // 0F8D..0F97;N     # Mn    [11] TIBETAN SUBJOINED SIGN LCE TSA CAN..TIBETAN SUBJOINED LETTER JA
            { Start = 0x0F99; Last = 0x0FBC; Width = EastAsianWidth.OfText "N"  } // 0F99..0FBC;N     # Mn    [36] TIBETAN SUBJOINED LETTER NYA..TIBETAN SUBJOINED LETTER FIXED-FORM RA
            { Start = 0x0FBE; Last = 0x0FC5; Width = EastAsianWidth.OfText "N"  } // 0FBE..0FC5;N     # So     [8] TIBETAN KU RU KHA..TIBETAN SYMBOL RDO RJE
            { Start = 0x0FC6; Last = 0x0FC6; Width = EastAsianWidth.OfText "N"  } // 0FC6;N           # Mn         TIBETAN SYMBOL PADMA GDAN
            { Start = 0x0FC7; Last = 0x0FCC; Width = EastAsianWidth.OfText "N"  } // 0FC7..0FCC;N     # So     [6] TIBETAN SYMBOL RDO RJE RGYA GRAM..TIBETAN SYMBOL NOR BU BZHI -KHYIL
            { Start = 0x0FCE; Last = 0x0FCF; Width = EastAsianWidth.OfText "N"  } // 0FCE..0FCF;N     # So     [2] TIBETAN SIGN RDEL NAG RDEL DKAR..TIBETAN SIGN RDEL NAG GSUM
            { Start = 0x0FD0; Last = 0x0FD4; Width = EastAsianWidth.OfText "N"  } // 0FD0..0FD4;N     # Po     [5] TIBETAN MARK BSKA- SHOG GI MGO RGYAN..TIBETAN MARK CLOSING BRDA RNYING YIG MGO SGAB MA
            { Start = 0x0FD5; Last = 0x0FD8; Width = EastAsianWidth.OfText "N"  } // 0FD5..0FD8;N     # So     [4] RIGHT-FACING SVASTI SIGN..LEFT-FACING SVASTI SIGN WITH DOTS
            { Start = 0x0FD9; Last = 0x0FDA; Width = EastAsianWidth.OfText "N"  } // 0FD9..0FDA;N     # Po     [2] TIBETAN MARK LEADING MCHAN RTAGS..TIBETAN MARK TRAILING MCHAN RTAGS
            { Start = 0x1000; Last = 0x102A; Width = EastAsianWidth.OfText "N"  } // 1000..102A;N     # Lo    [43] MYANMAR LETTER KA..MYANMAR LETTER AU
            { Start = 0x102B; Last = 0x102C; Width = EastAsianWidth.OfText "N"  } // 102B..102C;N     # Mc     [2] MYANMAR VOWEL SIGN TALL AA..MYANMAR VOWEL SIGN AA
            { Start = 0x102D; Last = 0x1030; Width = EastAsianWidth.OfText "N"  } // 102D..1030;N     # Mn     [4] MYANMAR VOWEL SIGN I..MYANMAR VOWEL SIGN UU
            { Start = 0x1031; Last = 0x1031; Width = EastAsianWidth.OfText "N"  } // 1031;N           # Mc         MYANMAR VOWEL SIGN E
            { Start = 0x1032; Last = 0x1037; Width = EastAsianWidth.OfText "N"  } // 1032..1037;N     # Mn     [6] MYANMAR VOWEL SIGN AI..MYANMAR SIGN DOT BELOW
            { Start = 0x1038; Last = 0x1038; Width = EastAsianWidth.OfText "N"  } // 1038;N           # Mc         MYANMAR SIGN VISARGA
            { Start = 0x1039; Last = 0x103A; Width = EastAsianWidth.OfText "N"  } // 1039..103A;N     # Mn     [2] MYANMAR SIGN VIRAMA..MYANMAR SIGN ASAT
            { Start = 0x103B; Last = 0x103C; Width = EastAsianWidth.OfText "N"  } // 103B..103C;N     # Mc     [2] MYANMAR CONSONANT SIGN MEDIAL YA..MYANMAR CONSONANT SIGN MEDIAL RA
            { Start = 0x103D; Last = 0x103E; Width = EastAsianWidth.OfText "N"  } // 103D..103E;N     # Mn     [2] MYANMAR CONSONANT SIGN MEDIAL WA..MYANMAR CONSONANT SIGN MEDIAL HA
            { Start = 0x103F; Last = 0x103F; Width = EastAsianWidth.OfText "N"  } // 103F;N           # Lo         MYANMAR LETTER GREAT SA
            { Start = 0x1040; Last = 0x1049; Width = EastAsianWidth.OfText "N"  } // 1040..1049;N     # Nd    [10] MYANMAR DIGIT ZERO..MYANMAR DIGIT NINE
            { Start = 0x104A; Last = 0x104F; Width = EastAsianWidth.OfText "N"  } // 104A..104F;N     # Po     [6] MYANMAR SIGN LITTLE SECTION..MYANMAR SYMBOL GENITIVE
            { Start = 0x1050; Last = 0x1055; Width = EastAsianWidth.OfText "N"  } // 1050..1055;N     # Lo     [6] MYANMAR LETTER SHA..MYANMAR LETTER VOCALIC LL
            { Start = 0x1056; Last = 0x1057; Width = EastAsianWidth.OfText "N"  } // 1056..1057;N     # Mc     [2] MYANMAR VOWEL SIGN VOCALIC R..MYANMAR VOWEL SIGN VOCALIC RR
            { Start = 0x1058; Last = 0x1059; Width = EastAsianWidth.OfText "N"  } // 1058..1059;N     # Mn     [2] MYANMAR VOWEL SIGN VOCALIC L..MYANMAR VOWEL SIGN VOCALIC LL
            { Start = 0x105A; Last = 0x105D; Width = EastAsianWidth.OfText "N"  } // 105A..105D;N     # Lo     [4] MYANMAR LETTER MON NGA..MYANMAR LETTER MON BBE
            { Start = 0x105E; Last = 0x1060; Width = EastAsianWidth.OfText "N"  } // 105E..1060;N     # Mn     [3] MYANMAR CONSONANT SIGN MON MEDIAL NA..MYANMAR CONSONANT SIGN MON MEDIAL LA
            { Start = 0x1061; Last = 0x1061; Width = EastAsianWidth.OfText "N"  } // 1061;N           # Lo         MYANMAR LETTER SGAW KAREN SHA
            { Start = 0x1062; Last = 0x1064; Width = EastAsianWidth.OfText "N"  } // 1062..1064;N     # Mc     [3] MYANMAR VOWEL SIGN SGAW KAREN EU..MYANMAR TONE MARK SGAW KAREN KE PHO
            { Start = 0x1065; Last = 0x1066; Width = EastAsianWidth.OfText "N"  } // 1065..1066;N     # Lo     [2] MYANMAR LETTER WESTERN PWO KAREN THA..MYANMAR LETTER WESTERN PWO KAREN PWA
            { Start = 0x1067; Last = 0x106D; Width = EastAsianWidth.OfText "N"  } // 1067..106D;N     # Mc     [7] MYANMAR VOWEL SIGN WESTERN PWO KAREN EU..MYANMAR SIGN WESTERN PWO KAREN TONE-5
            { Start = 0x106E; Last = 0x1070; Width = EastAsianWidth.OfText "N"  } // 106E..1070;N     # Lo     [3] MYANMAR LETTER EASTERN PWO KAREN NNA..MYANMAR LETTER EASTERN PWO KAREN GHWA
            { Start = 0x1071; Last = 0x1074; Width = EastAsianWidth.OfText "N"  } // 1071..1074;N     # Mn     [4] MYANMAR VOWEL SIGN GEBA KAREN I..MYANMAR VOWEL SIGN KAYAH EE
            { Start = 0x1075; Last = 0x1081; Width = EastAsianWidth.OfText "N"  } // 1075..1081;N     # Lo    [13] MYANMAR LETTER SHAN KA..MYANMAR LETTER SHAN HA
            { Start = 0x1082; Last = 0x1082; Width = EastAsianWidth.OfText "N"  } // 1082;N           # Mn         MYANMAR CONSONANT SIGN SHAN MEDIAL WA
            { Start = 0x1083; Last = 0x1084; Width = EastAsianWidth.OfText "N"  } // 1083..1084;N     # Mc     [2] MYANMAR VOWEL SIGN SHAN AA..MYANMAR VOWEL SIGN SHAN E
            { Start = 0x1085; Last = 0x1086; Width = EastAsianWidth.OfText "N"  } // 1085..1086;N     # Mn     [2] MYANMAR VOWEL SIGN SHAN E ABOVE..MYANMAR VOWEL SIGN SHAN FINAL Y
            { Start = 0x1087; Last = 0x108C; Width = EastAsianWidth.OfText "N"  } // 1087..108C;N     # Mc     [6] MYANMAR SIGN SHAN TONE-2..MYANMAR SIGN SHAN COUNCIL TONE-3
            { Start = 0x108D; Last = 0x108D; Width = EastAsianWidth.OfText "N"  } // 108D;N           # Mn         MYANMAR SIGN SHAN COUNCIL EMPHATIC TONE
            { Start = 0x108E; Last = 0x108E; Width = EastAsianWidth.OfText "N"  } // 108E;N           # Lo         MYANMAR LETTER RUMAI PALAUNG FA
            { Start = 0x108F; Last = 0x108F; Width = EastAsianWidth.OfText "N"  } // 108F;N           # Mc         MYANMAR SIGN RUMAI PALAUNG TONE-5
            { Start = 0x1090; Last = 0x1099; Width = EastAsianWidth.OfText "N"  } // 1090..1099;N     # Nd    [10] MYANMAR SHAN DIGIT ZERO..MYANMAR SHAN DIGIT NINE
            { Start = 0x109A; Last = 0x109C; Width = EastAsianWidth.OfText "N"  } // 109A..109C;N     # Mc     [3] MYANMAR SIGN KHAMTI TONE-1..MYANMAR VOWEL SIGN AITON A
            { Start = 0x109D; Last = 0x109D; Width = EastAsianWidth.OfText "N"  } // 109D;N           # Mn         MYANMAR VOWEL SIGN AITON AI
            { Start = 0x109E; Last = 0x109F; Width = EastAsianWidth.OfText "N"  } // 109E..109F;N     # So     [2] MYANMAR SYMBOL SHAN ONE..MYANMAR SYMBOL SHAN EXCLAMATION
            { Start = 0x10A0; Last = 0x10C5; Width = EastAsianWidth.OfText "N"  } // 10A0..10C5;N     # Lu    [38] GEORGIAN CAPITAL LETTER AN..GEORGIAN CAPITAL LETTER HOE
            { Start = 0x10C7; Last = 0x10C7; Width = EastAsianWidth.OfText "N"  } // 10C7;N           # Lu         GEORGIAN CAPITAL LETTER YN
            { Start = 0x10CD; Last = 0x10CD; Width = EastAsianWidth.OfText "N"  } // 10CD;N           # Lu         GEORGIAN CAPITAL LETTER AEN
            { Start = 0x10D0; Last = 0x10FA; Width = EastAsianWidth.OfText "N"  } // 10D0..10FA;N     # Ll    [43] GEORGIAN LETTER AN..GEORGIAN LETTER AIN
            { Start = 0x10FB; Last = 0x10FB; Width = EastAsianWidth.OfText "N"  } // 10FB;N           # Po         GEORGIAN PARAGRAPH SEPARATOR
            { Start = 0x10FC; Last = 0x10FC; Width = EastAsianWidth.OfText "N"  } // 10FC;N           # Lm         MODIFIER LETTER GEORGIAN NAR
            { Start = 0x10FD; Last = 0x10FF; Width = EastAsianWidth.OfText "N"  } // 10FD..10FF;N     # Ll     [3] GEORGIAN LETTER AEN..GEORGIAN LETTER LABIAL SIGN
            { Start = 0x1100; Last = 0x115F; Width = EastAsianWidth.OfText "W"  } // 1100..115F;W     # Lo    [96] HANGUL CHOSEONG KIYEOK..HANGUL CHOSEONG FILLER
            { Start = 0x1160; Last = 0x11FF; Width = EastAsianWidth.OfText "N"  } // 1160..11FF;N     # Lo   [160] HANGUL JUNGSEONG FILLER..HANGUL JONGSEONG SSANGNIEUN
            { Start = 0x1200; Last = 0x1248; Width = EastAsianWidth.OfText "N"  } // 1200..1248;N     # Lo    [73] ETHIOPIC SYLLABLE HA..ETHIOPIC SYLLABLE QWA
            { Start = 0x124A; Last = 0x124D; Width = EastAsianWidth.OfText "N"  } // 124A..124D;N     # Lo     [4] ETHIOPIC SYLLABLE QWI..ETHIOPIC SYLLABLE QWE
            { Start = 0x1250; Last = 0x1256; Width = EastAsianWidth.OfText "N"  } // 1250..1256;N     # Lo     [7] ETHIOPIC SYLLABLE QHA..ETHIOPIC SYLLABLE QHO
            { Start = 0x1258; Last = 0x1258; Width = EastAsianWidth.OfText "N"  } // 1258;N           # Lo         ETHIOPIC SYLLABLE QHWA
            { Start = 0x125A; Last = 0x125D; Width = EastAsianWidth.OfText "N"  } // 125A..125D;N     # Lo     [4] ETHIOPIC SYLLABLE QHWI..ETHIOPIC SYLLABLE QHWE
            { Start = 0x1260; Last = 0x1288; Width = EastAsianWidth.OfText "N"  } // 1260..1288;N     # Lo    [41] ETHIOPIC SYLLABLE BA..ETHIOPIC SYLLABLE XWA
            { Start = 0x128A; Last = 0x128D; Width = EastAsianWidth.OfText "N"  } // 128A..128D;N     # Lo     [4] ETHIOPIC SYLLABLE XWI..ETHIOPIC SYLLABLE XWE
            { Start = 0x1290; Last = 0x12B0; Width = EastAsianWidth.OfText "N"  } // 1290..12B0;N     # Lo    [33] ETHIOPIC SYLLABLE NA..ETHIOPIC SYLLABLE KWA
            { Start = 0x12B2; Last = 0x12B5; Width = EastAsianWidth.OfText "N"  } // 12B2..12B5;N     # Lo     [4] ETHIOPIC SYLLABLE KWI..ETHIOPIC SYLLABLE KWE
            { Start = 0x12B8; Last = 0x12BE; Width = EastAsianWidth.OfText "N"  } // 12B8..12BE;N     # Lo     [7] ETHIOPIC SYLLABLE KXA..ETHIOPIC SYLLABLE KXO
            { Start = 0x12C0; Last = 0x12C0; Width = EastAsianWidth.OfText "N"  } // 12C0;N           # Lo         ETHIOPIC SYLLABLE KXWA
            { Start = 0x12C2; Last = 0x12C5; Width = EastAsianWidth.OfText "N"  } // 12C2..12C5;N     # Lo     [4] ETHIOPIC SYLLABLE KXWI..ETHIOPIC SYLLABLE KXWE
            { Start = 0x12C8; Last = 0x12D6; Width = EastAsianWidth.OfText "N"  } // 12C8..12D6;N     # Lo    [15] ETHIOPIC SYLLABLE WA..ETHIOPIC SYLLABLE PHARYNGEAL O
            { Start = 0x12D8; Last = 0x1310; Width = EastAsianWidth.OfText "N"  } // 12D8..1310;N     # Lo    [57] ETHIOPIC SYLLABLE ZA..ETHIOPIC SYLLABLE GWA
            { Start = 0x1312; Last = 0x1315; Width = EastAsianWidth.OfText "N"  } // 1312..1315;N     # Lo     [4] ETHIOPIC SYLLABLE GWI..ETHIOPIC SYLLABLE GWE
            { Start = 0x1318; Last = 0x135A; Width = EastAsianWidth.OfText "N"  } // 1318..135A;N     # Lo    [67] ETHIOPIC SYLLABLE GGA..ETHIOPIC SYLLABLE FYA
            { Start = 0x135D; Last = 0x135F; Width = EastAsianWidth.OfText "N"  } // 135D..135F;N     # Mn     [3] ETHIOPIC COMBINING GEMINATION AND VOWEL LENGTH MARK..ETHIOPIC COMBINING GEMINATION MARK
            { Start = 0x1360; Last = 0x1368; Width = EastAsianWidth.OfText "N"  } // 1360..1368;N     # Po     [9] ETHIOPIC SECTION MARK..ETHIOPIC PARAGRAPH SEPARATOR
            { Start = 0x1369; Last = 0x137C; Width = EastAsianWidth.OfText "N"  } // 1369..137C;N     # No    [20] ETHIOPIC DIGIT ONE..ETHIOPIC NUMBER TEN THOUSAND
            { Start = 0x1380; Last = 0x138F; Width = EastAsianWidth.OfText "N"  } // 1380..138F;N     # Lo    [16] ETHIOPIC SYLLABLE SEBATBEIT MWA..ETHIOPIC SYLLABLE PWE
            { Start = 0x1390; Last = 0x1399; Width = EastAsianWidth.OfText "N"  } // 1390..1399;N     # So    [10] ETHIOPIC TONAL MARK YIZET..ETHIOPIC TONAL MARK KURT
            { Start = 0x13A0; Last = 0x13F5; Width = EastAsianWidth.OfText "N"  } // 13A0..13F5;N     # Lu    [86] CHEROKEE LETTER A..CHEROKEE LETTER MV
            { Start = 0x13F8; Last = 0x13FD; Width = EastAsianWidth.OfText "N"  } // 13F8..13FD;N     # Ll     [6] CHEROKEE SMALL LETTER YE..CHEROKEE SMALL LETTER MV
            { Start = 0x1400; Last = 0x1400; Width = EastAsianWidth.OfText "N"  } // 1400;N           # Pd         CANADIAN SYLLABICS HYPHEN
            { Start = 0x1401; Last = 0x166C; Width = EastAsianWidth.OfText "N"  } // 1401..166C;N     # Lo   [620] CANADIAN SYLLABICS E..CANADIAN SYLLABICS CARRIER TTSA
            { Start = 0x166D; Last = 0x166E; Width = EastAsianWidth.OfText "N"  } // 166D..166E;N     # Po     [2] CANADIAN SYLLABICS CHI SIGN..CANADIAN SYLLABICS FULL STOP
            { Start = 0x166F; Last = 0x167F; Width = EastAsianWidth.OfText "N"  } // 166F..167F;N     # Lo    [17] CANADIAN SYLLABICS QAI..CANADIAN SYLLABICS BLACKFOOT W
            { Start = 0x1680; Last = 0x1680; Width = EastAsianWidth.OfText "N"  } // 1680;N           # Zs         OGHAM SPACE MARK
            { Start = 0x1681; Last = 0x169A; Width = EastAsianWidth.OfText "N"  } // 1681..169A;N     # Lo    [26] OGHAM LETTER BEITH..OGHAM LETTER PEITH
            { Start = 0x169B; Last = 0x169B; Width = EastAsianWidth.OfText "N"  } // 169B;N           # Ps         OGHAM FEATHER MARK
            { Start = 0x169C; Last = 0x169C; Width = EastAsianWidth.OfText "N"  } // 169C;N           # Pe         OGHAM REVERSED FEATHER MARK
            { Start = 0x16A0; Last = 0x16EA; Width = EastAsianWidth.OfText "N"  } // 16A0..16EA;N     # Lo    [75] RUNIC LETTER FEHU FEOH FE F..RUNIC LETTER X
            { Start = 0x16EB; Last = 0x16ED; Width = EastAsianWidth.OfText "N"  } // 16EB..16ED;N     # Po     [3] RUNIC SINGLE PUNCTUATION..RUNIC CROSS PUNCTUATION
            { Start = 0x16EE; Last = 0x16F0; Width = EastAsianWidth.OfText "N"  } // 16EE..16F0;N     # Nl     [3] RUNIC ARLAUG SYMBOL..RUNIC BELGTHOR SYMBOL
            { Start = 0x16F1; Last = 0x16F8; Width = EastAsianWidth.OfText "N"  } // 16F1..16F8;N     # Lo     [8] RUNIC LETTER K..RUNIC LETTER FRANKS CASKET AESC
            { Start = 0x1700; Last = 0x170C; Width = EastAsianWidth.OfText "N"  } // 1700..170C;N     # Lo    [13] TAGALOG LETTER A..TAGALOG LETTER YA
            { Start = 0x170E; Last = 0x1711; Width = EastAsianWidth.OfText "N"  } // 170E..1711;N     # Lo     [4] TAGALOG LETTER LA..TAGALOG LETTER HA
            { Start = 0x1712; Last = 0x1714; Width = EastAsianWidth.OfText "N"  } // 1712..1714;N     # Mn     [3] TAGALOG VOWEL SIGN I..TAGALOG SIGN VIRAMA
            { Start = 0x1720; Last = 0x1731; Width = EastAsianWidth.OfText "N"  } // 1720..1731;N     # Lo    [18] HANUNOO LETTER A..HANUNOO LETTER HA
            { Start = 0x1732; Last = 0x1734; Width = EastAsianWidth.OfText "N"  } // 1732..1734;N     # Mn     [3] HANUNOO VOWEL SIGN I..HANUNOO SIGN PAMUDPOD
            { Start = 0x1735; Last = 0x1736; Width = EastAsianWidth.OfText "N"  } // 1735..1736;N     # Po     [2] PHILIPPINE SINGLE PUNCTUATION..PHILIPPINE DOUBLE PUNCTUATION
            { Start = 0x1740; Last = 0x1751; Width = EastAsianWidth.OfText "N"  } // 1740..1751;N     # Lo    [18] BUHID LETTER A..BUHID LETTER HA
            { Start = 0x1752; Last = 0x1753; Width = EastAsianWidth.OfText "N"  } // 1752..1753;N     # Mn     [2] BUHID VOWEL SIGN I..BUHID VOWEL SIGN U
            { Start = 0x1760; Last = 0x176C; Width = EastAsianWidth.OfText "N"  } // 1760..176C;N     # Lo    [13] TAGBANWA LETTER A..TAGBANWA LETTER YA
            { Start = 0x176E; Last = 0x1770; Width = EastAsianWidth.OfText "N"  } // 176E..1770;N     # Lo     [3] TAGBANWA LETTER LA..TAGBANWA LETTER SA
            { Start = 0x1772; Last = 0x1773; Width = EastAsianWidth.OfText "N"  } // 1772..1773;N     # Mn     [2] TAGBANWA VOWEL SIGN I..TAGBANWA VOWEL SIGN U
            { Start = 0x1780; Last = 0x17B3; Width = EastAsianWidth.OfText "N"  } // 1780..17B3;N     # Lo    [52] KHMER LETTER KA..KHMER INDEPENDENT VOWEL QAU
            { Start = 0x17B4; Last = 0x17B5; Width = EastAsianWidth.OfText "N"  } // 17B4..17B5;N     # Mn     [2] KHMER VOWEL INHERENT AQ..KHMER VOWEL INHERENT AA
            { Start = 0x17B6; Last = 0x17B6; Width = EastAsianWidth.OfText "N"  } // 17B6;N           # Mc         KHMER VOWEL SIGN AA
            { Start = 0x17B7; Last = 0x17BD; Width = EastAsianWidth.OfText "N"  } // 17B7..17BD;N     # Mn     [7] KHMER VOWEL SIGN I..KHMER VOWEL SIGN UA
            { Start = 0x17BE; Last = 0x17C5; Width = EastAsianWidth.OfText "N"  } // 17BE..17C5;N     # Mc     [8] KHMER VOWEL SIGN OE..KHMER VOWEL SIGN AU
            { Start = 0x17C6; Last = 0x17C6; Width = EastAsianWidth.OfText "N"  } // 17C6;N           # Mn         KHMER SIGN NIKAHIT
            { Start = 0x17C7; Last = 0x17C8; Width = EastAsianWidth.OfText "N"  } // 17C7..17C8;N     # Mc     [2] KHMER SIGN REAHMUK..KHMER SIGN YUUKALEAPINTU
            { Start = 0x17C9; Last = 0x17D3; Width = EastAsianWidth.OfText "N"  } // 17C9..17D3;N     # Mn    [11] KHMER SIGN MUUSIKATOAN..KHMER SIGN BATHAMASAT
            { Start = 0x17D4; Last = 0x17D6; Width = EastAsianWidth.OfText "N"  } // 17D4..17D6;N     # Po     [3] KHMER SIGN KHAN..KHMER SIGN CAMNUC PII KUUH
            { Start = 0x17D7; Last = 0x17D7; Width = EastAsianWidth.OfText "N"  } // 17D7;N           # Lm         KHMER SIGN LEK TOO
            { Start = 0x17D8; Last = 0x17DA; Width = EastAsianWidth.OfText "N"  } // 17D8..17DA;N     # Po     [3] KHMER SIGN BEYYAL..KHMER SIGN KOOMUUT
            { Start = 0x17DB; Last = 0x17DB; Width = EastAsianWidth.OfText "N"  } // 17DB;N           # Sc         KHMER CURRENCY SYMBOL RIEL
            { Start = 0x17DC; Last = 0x17DC; Width = EastAsianWidth.OfText "N"  } // 17DC;N           # Lo         KHMER SIGN AVAKRAHASANYA
            { Start = 0x17DD; Last = 0x17DD; Width = EastAsianWidth.OfText "N"  } // 17DD;N           # Mn         KHMER SIGN ATTHACAN
            { Start = 0x17E0; Last = 0x17E9; Width = EastAsianWidth.OfText "N"  } // 17E0..17E9;N     # Nd    [10] KHMER DIGIT ZERO..KHMER DIGIT NINE
            { Start = 0x17F0; Last = 0x17F9; Width = EastAsianWidth.OfText "N"  } // 17F0..17F9;N     # No    [10] KHMER SYMBOL LEK ATTAK SON..KHMER SYMBOL LEK ATTAK PRAM-BUON
            { Start = 0x1800; Last = 0x1805; Width = EastAsianWidth.OfText "N"  } // 1800..1805;N     # Po     [6] MONGOLIAN BIRGA..MONGOLIAN FOUR DOTS
            { Start = 0x1806; Last = 0x1806; Width = EastAsianWidth.OfText "N"  } // 1806;N           # Pd         MONGOLIAN TODO SOFT HYPHEN
            { Start = 0x1807; Last = 0x180A; Width = EastAsianWidth.OfText "N"  } // 1807..180A;N     # Po     [4] MONGOLIAN SIBE SYLLABLE BOUNDARY MARKER..MONGOLIAN NIRUGU
            { Start = 0x180B; Last = 0x180D; Width = EastAsianWidth.OfText "N"  } // 180B..180D;N     # Mn     [3] MONGOLIAN FREE VARIATION SELECTOR ONE..MONGOLIAN FREE VARIATION SELECTOR THREE
            { Start = 0x180E; Last = 0x180E; Width = EastAsianWidth.OfText "N"  } // 180E;N           # Cf         MONGOLIAN VOWEL SEPARATOR
            { Start = 0x1810; Last = 0x1819; Width = EastAsianWidth.OfText "N"  } // 1810..1819;N     # Nd    [10] MONGOLIAN DIGIT ZERO..MONGOLIAN DIGIT NINE
            { Start = 0x1820; Last = 0x1842; Width = EastAsianWidth.OfText "N"  } // 1820..1842;N     # Lo    [35] MONGOLIAN LETTER A..MONGOLIAN LETTER CHI
            { Start = 0x1843; Last = 0x1843; Width = EastAsianWidth.OfText "N"  } // 1843;N           # Lm         MONGOLIAN LETTER TODO LONG VOWEL SIGN
            { Start = 0x1844; Last = 0x1878; Width = EastAsianWidth.OfText "N"  } // 1844..1878;N     # Lo    [53] MONGOLIAN LETTER TODO E..MONGOLIAN LETTER CHA WITH TWO DOTS
            { Start = 0x1880; Last = 0x1884; Width = EastAsianWidth.OfText "N"  } // 1880..1884;N     # Lo     [5] MONGOLIAN LETTER ALI GALI ANUSVARA ONE..MONGOLIAN LETTER ALI GALI INVERTED UBADAMA
            { Start = 0x1885; Last = 0x1886; Width = EastAsianWidth.OfText "N"  } // 1885..1886;N     # Mn     [2] MONGOLIAN LETTER ALI GALI BALUDA..MONGOLIAN LETTER ALI GALI THREE BALUDA
            { Start = 0x1887; Last = 0x18A8; Width = EastAsianWidth.OfText "N"  } // 1887..18A8;N     # Lo    [34] MONGOLIAN LETTER ALI GALI A..MONGOLIAN LETTER MANCHU ALI GALI BHA
            { Start = 0x18A9; Last = 0x18A9; Width = EastAsianWidth.OfText "N"  } // 18A9;N           # Mn         MONGOLIAN LETTER ALI GALI DAGALGA
            { Start = 0x18AA; Last = 0x18AA; Width = EastAsianWidth.OfText "N"  } // 18AA;N           # Lo         MONGOLIAN LETTER MANCHU ALI GALI LHA
            { Start = 0x18B0; Last = 0x18F5; Width = EastAsianWidth.OfText "N"  } // 18B0..18F5;N     # Lo    [70] CANADIAN SYLLABICS OY..CANADIAN SYLLABICS CARRIER DENTAL S
            { Start = 0x1900; Last = 0x191E; Width = EastAsianWidth.OfText "N"  } // 1900..191E;N     # Lo    [31] LIMBU VOWEL-CARRIER LETTER..LIMBU LETTER TRA
            { Start = 0x1920; Last = 0x1922; Width = EastAsianWidth.OfText "N"  } // 1920..1922;N     # Mn     [3] LIMBU VOWEL SIGN A..LIMBU VOWEL SIGN U
            { Start = 0x1923; Last = 0x1926; Width = EastAsianWidth.OfText "N"  } // 1923..1926;N     # Mc     [4] LIMBU VOWEL SIGN EE..LIMBU VOWEL SIGN AU
            { Start = 0x1927; Last = 0x1928; Width = EastAsianWidth.OfText "N"  } // 1927..1928;N     # Mn     [2] LIMBU VOWEL SIGN E..LIMBU VOWEL SIGN O
            { Start = 0x1929; Last = 0x192B; Width = EastAsianWidth.OfText "N"  } // 1929..192B;N     # Mc     [3] LIMBU SUBJOINED LETTER YA..LIMBU SUBJOINED LETTER WA
            { Start = 0x1930; Last = 0x1931; Width = EastAsianWidth.OfText "N"  } // 1930..1931;N     # Mc     [2] LIMBU SMALL LETTER KA..LIMBU SMALL LETTER NGA
            { Start = 0x1932; Last = 0x1932; Width = EastAsianWidth.OfText "N"  } // 1932;N           # Mn         LIMBU SMALL LETTER ANUSVARA
            { Start = 0x1933; Last = 0x1938; Width = EastAsianWidth.OfText "N"  } // 1933..1938;N     # Mc     [6] LIMBU SMALL LETTER TA..LIMBU SMALL LETTER LA
            { Start = 0x1939; Last = 0x193B; Width = EastAsianWidth.OfText "N"  } // 1939..193B;N     # Mn     [3] LIMBU SIGN MUKPHRENG..LIMBU SIGN SA-I
            { Start = 0x1940; Last = 0x1940; Width = EastAsianWidth.OfText "N"  } // 1940;N           # So         LIMBU SIGN LOO
            { Start = 0x1944; Last = 0x1945; Width = EastAsianWidth.OfText "N"  } // 1944..1945;N     # Po     [2] LIMBU EXCLAMATION MARK..LIMBU QUESTION MARK
            { Start = 0x1946; Last = 0x194F; Width = EastAsianWidth.OfText "N"  } // 1946..194F;N     # Nd    [10] LIMBU DIGIT ZERO..LIMBU DIGIT NINE
            { Start = 0x1950; Last = 0x196D; Width = EastAsianWidth.OfText "N"  } // 1950..196D;N     # Lo    [30] TAI LE LETTER KA..TAI LE LETTER AI
            { Start = 0x1970; Last = 0x1974; Width = EastAsianWidth.OfText "N"  } // 1970..1974;N     # Lo     [5] TAI LE LETTER TONE-2..TAI LE LETTER TONE-6
            { Start = 0x1980; Last = 0x19AB; Width = EastAsianWidth.OfText "N"  } // 1980..19AB;N     # Lo    [44] NEW TAI LUE LETTER HIGH QA..NEW TAI LUE LETTER LOW SUA
            { Start = 0x19B0; Last = 0x19C9; Width = EastAsianWidth.OfText "N"  } // 19B0..19C9;N     # Lo    [26] NEW TAI LUE VOWEL SIGN VOWEL SHORTENER..NEW TAI LUE TONE MARK-2
            { Start = 0x19D0; Last = 0x19D9; Width = EastAsianWidth.OfText "N"  } // 19D0..19D9;N     # Nd    [10] NEW TAI LUE DIGIT ZERO..NEW TAI LUE DIGIT NINE
            { Start = 0x19DA; Last = 0x19DA; Width = EastAsianWidth.OfText "N"  } // 19DA;N           # No         NEW TAI LUE THAM DIGIT ONE
            { Start = 0x19DE; Last = 0x19DF; Width = EastAsianWidth.OfText "N"  } // 19DE..19DF;N     # So     [2] NEW TAI LUE SIGN LAE..NEW TAI LUE SIGN LAEV
            { Start = 0x19E0; Last = 0x19FF; Width = EastAsianWidth.OfText "N"  } // 19E0..19FF;N     # So    [32] KHMER SYMBOL PATHAMASAT..KHMER SYMBOL DAP-PRAM ROC
            { Start = 0x1A00; Last = 0x1A16; Width = EastAsianWidth.OfText "N"  } // 1A00..1A16;N     # Lo    [23] BUGINESE LETTER KA..BUGINESE LETTER HA
            { Start = 0x1A17; Last = 0x1A18; Width = EastAsianWidth.OfText "N"  } // 1A17..1A18;N     # Mn     [2] BUGINESE VOWEL SIGN I..BUGINESE VOWEL SIGN U
            { Start = 0x1A19; Last = 0x1A1A; Width = EastAsianWidth.OfText "N"  } // 1A19..1A1A;N     # Mc     [2] BUGINESE VOWEL SIGN E..BUGINESE VOWEL SIGN O
            { Start = 0x1A1B; Last = 0x1A1B; Width = EastAsianWidth.OfText "N"  } // 1A1B;N           # Mn         BUGINESE VOWEL SIGN AE
            { Start = 0x1A1E; Last = 0x1A1F; Width = EastAsianWidth.OfText "N"  } // 1A1E..1A1F;N     # Po     [2] BUGINESE PALLAWA..BUGINESE END OF SECTION
            { Start = 0x1A20; Last = 0x1A54; Width = EastAsianWidth.OfText "N"  } // 1A20..1A54;N     # Lo    [53] TAI THAM LETTER HIGH KA..TAI THAM LETTER GREAT SA
            { Start = 0x1A55; Last = 0x1A55; Width = EastAsianWidth.OfText "N"  } // 1A55;N           # Mc         TAI THAM CONSONANT SIGN MEDIAL RA
            { Start = 0x1A56; Last = 0x1A56; Width = EastAsianWidth.OfText "N"  } // 1A56;N           # Mn         TAI THAM CONSONANT SIGN MEDIAL LA
            { Start = 0x1A57; Last = 0x1A57; Width = EastAsianWidth.OfText "N"  } // 1A57;N           # Mc         TAI THAM CONSONANT SIGN LA TANG LAI
            { Start = 0x1A58; Last = 0x1A5E; Width = EastAsianWidth.OfText "N"  } // 1A58..1A5E;N     # Mn     [7] TAI THAM SIGN MAI KANG LAI..TAI THAM CONSONANT SIGN SA
            { Start = 0x1A60; Last = 0x1A60; Width = EastAsianWidth.OfText "N"  } // 1A60;N           # Mn         TAI THAM SIGN SAKOT
            { Start = 0x1A61; Last = 0x1A61; Width = EastAsianWidth.OfText "N"  } // 1A61;N           # Mc         TAI THAM VOWEL SIGN A
            { Start = 0x1A62; Last = 0x1A62; Width = EastAsianWidth.OfText "N"  } // 1A62;N           # Mn         TAI THAM VOWEL SIGN MAI SAT
            { Start = 0x1A63; Last = 0x1A64; Width = EastAsianWidth.OfText "N"  } // 1A63..1A64;N     # Mc     [2] TAI THAM VOWEL SIGN AA..TAI THAM VOWEL SIGN TALL AA
            { Start = 0x1A65; Last = 0x1A6C; Width = EastAsianWidth.OfText "N"  } // 1A65..1A6C;N     # Mn     [8] TAI THAM VOWEL SIGN I..TAI THAM VOWEL SIGN OA BELOW
            { Start = 0x1A6D; Last = 0x1A72; Width = EastAsianWidth.OfText "N"  } // 1A6D..1A72;N     # Mc     [6] TAI THAM VOWEL SIGN OY..TAI THAM VOWEL SIGN THAM AI
            { Start = 0x1A73; Last = 0x1A7C; Width = EastAsianWidth.OfText "N"  } // 1A73..1A7C;N     # Mn    [10] TAI THAM VOWEL SIGN OA ABOVE..TAI THAM SIGN KHUEN-LUE KARAN
            { Start = 0x1A7F; Last = 0x1A7F; Width = EastAsianWidth.OfText "N"  } // 1A7F;N           # Mn         TAI THAM COMBINING CRYPTOGRAMMIC DOT
            { Start = 0x1A80; Last = 0x1A89; Width = EastAsianWidth.OfText "N"  } // 1A80..1A89;N     # Nd    [10] TAI THAM HORA DIGIT ZERO..TAI THAM HORA DIGIT NINE
            { Start = 0x1A90; Last = 0x1A99; Width = EastAsianWidth.OfText "N"  } // 1A90..1A99;N     # Nd    [10] TAI THAM THAM DIGIT ZERO..TAI THAM THAM DIGIT NINE
            { Start = 0x1AA0; Last = 0x1AA6; Width = EastAsianWidth.OfText "N"  } // 1AA0..1AA6;N     # Po     [7] TAI THAM SIGN WIANG..TAI THAM SIGN REVERSED ROTATED RANA
            { Start = 0x1AA7; Last = 0x1AA7; Width = EastAsianWidth.OfText "N"  } // 1AA7;N           # Lm         TAI THAM SIGN MAI YAMOK
            { Start = 0x1AA8; Last = 0x1AAD; Width = EastAsianWidth.OfText "N"  } // 1AA8..1AAD;N     # Po     [6] TAI THAM SIGN KAAN..TAI THAM SIGN CAANG
            { Start = 0x1AB0; Last = 0x1ABD; Width = EastAsianWidth.OfText "N"  } // 1AB0..1ABD;N     # Mn    [14] COMBINING DOUBLED CIRCUMFLEX ACCENT..COMBINING PARENTHESES BELOW
            { Start = 0x1ABE; Last = 0x1ABE; Width = EastAsianWidth.OfText "N"  } // 1ABE;N           # Me         COMBINING PARENTHESES OVERLAY
            { Start = 0x1B00; Last = 0x1B03; Width = EastAsianWidth.OfText "N"  } // 1B00..1B03;N     # Mn     [4] BALINESE SIGN ULU RICEM..BALINESE SIGN SURANG
            { Start = 0x1B04; Last = 0x1B04; Width = EastAsianWidth.OfText "N"  } // 1B04;N           # Mc         BALINESE SIGN BISAH
            { Start = 0x1B05; Last = 0x1B33; Width = EastAsianWidth.OfText "N"  } // 1B05..1B33;N     # Lo    [47] BALINESE LETTER AKARA..BALINESE LETTER HA
            { Start = 0x1B34; Last = 0x1B34; Width = EastAsianWidth.OfText "N"  } // 1B34;N           # Mn         BALINESE SIGN REREKAN
            { Start = 0x1B35; Last = 0x1B35; Width = EastAsianWidth.OfText "N"  } // 1B35;N           # Mc         BALINESE VOWEL SIGN TEDUNG
            { Start = 0x1B36; Last = 0x1B3A; Width = EastAsianWidth.OfText "N"  } // 1B36..1B3A;N     # Mn     [5] BALINESE VOWEL SIGN ULU..BALINESE VOWEL SIGN RA REPA
            { Start = 0x1B3B; Last = 0x1B3B; Width = EastAsianWidth.OfText "N"  } // 1B3B;N           # Mc         BALINESE VOWEL SIGN RA REPA TEDUNG
            { Start = 0x1B3C; Last = 0x1B3C; Width = EastAsianWidth.OfText "N"  } // 1B3C;N           # Mn         BALINESE VOWEL SIGN LA LENGA
            { Start = 0x1B3D; Last = 0x1B41; Width = EastAsianWidth.OfText "N"  } // 1B3D..1B41;N     # Mc     [5] BALINESE VOWEL SIGN LA LENGA TEDUNG..BALINESE VOWEL SIGN TALING REPA TEDUNG
            { Start = 0x1B42; Last = 0x1B42; Width = EastAsianWidth.OfText "N"  } // 1B42;N           # Mn         BALINESE VOWEL SIGN PEPET
            { Start = 0x1B43; Last = 0x1B44; Width = EastAsianWidth.OfText "N"  } // 1B43..1B44;N     # Mc     [2] BALINESE VOWEL SIGN PEPET TEDUNG..BALINESE ADEG ADEG
            { Start = 0x1B45; Last = 0x1B4B; Width = EastAsianWidth.OfText "N"  } // 1B45..1B4B;N     # Lo     [7] BALINESE LETTER KAF SASAK..BALINESE LETTER ASYURA SASAK
            { Start = 0x1B50; Last = 0x1B59; Width = EastAsianWidth.OfText "N"  } // 1B50..1B59;N     # Nd    [10] BALINESE DIGIT ZERO..BALINESE DIGIT NINE
            { Start = 0x1B5A; Last = 0x1B60; Width = EastAsianWidth.OfText "N"  } // 1B5A..1B60;N     # Po     [7] BALINESE PANTI..BALINESE PAMENENG
            { Start = 0x1B61; Last = 0x1B6A; Width = EastAsianWidth.OfText "N"  } // 1B61..1B6A;N     # So    [10] BALINESE MUSICAL SYMBOL DONG..BALINESE MUSICAL SYMBOL DANG GEDE
            { Start = 0x1B6B; Last = 0x1B73; Width = EastAsianWidth.OfText "N"  } // 1B6B..1B73;N     # Mn     [9] BALINESE MUSICAL SYMBOL COMBINING TEGEH..BALINESE MUSICAL SYMBOL COMBINING GONG
            { Start = 0x1B74; Last = 0x1B7C; Width = EastAsianWidth.OfText "N"  } // 1B74..1B7C;N     # So     [9] BALINESE MUSICAL SYMBOL RIGHT-HAND OPEN DUG..BALINESE MUSICAL SYMBOL LEFT-HAND OPEN PING
            { Start = 0x1B80; Last = 0x1B81; Width = EastAsianWidth.OfText "N"  } // 1B80..1B81;N     # Mn     [2] SUNDANESE SIGN PANYECEK..SUNDANESE SIGN PANGLAYAR
            { Start = 0x1B82; Last = 0x1B82; Width = EastAsianWidth.OfText "N"  } // 1B82;N           # Mc         SUNDANESE SIGN PANGWISAD
            { Start = 0x1B83; Last = 0x1BA0; Width = EastAsianWidth.OfText "N"  } // 1B83..1BA0;N     # Lo    [30] SUNDANESE LETTER A..SUNDANESE LETTER HA
            { Start = 0x1BA1; Last = 0x1BA1; Width = EastAsianWidth.OfText "N"  } // 1BA1;N           # Mc         SUNDANESE CONSONANT SIGN PAMINGKAL
            { Start = 0x1BA2; Last = 0x1BA5; Width = EastAsianWidth.OfText "N"  } // 1BA2..1BA5;N     # Mn     [4] SUNDANESE CONSONANT SIGN PANYAKRA..SUNDANESE VOWEL SIGN PANYUKU
            { Start = 0x1BA6; Last = 0x1BA7; Width = EastAsianWidth.OfText "N"  } // 1BA6..1BA7;N     # Mc     [2] SUNDANESE VOWEL SIGN PANAELAENG..SUNDANESE VOWEL SIGN PANOLONG
            { Start = 0x1BA8; Last = 0x1BA9; Width = EastAsianWidth.OfText "N"  } // 1BA8..1BA9;N     # Mn     [2] SUNDANESE VOWEL SIGN PAMEPET..SUNDANESE VOWEL SIGN PANEULEUNG
            { Start = 0x1BAA; Last = 0x1BAA; Width = EastAsianWidth.OfText "N"  } // 1BAA;N           # Mc         SUNDANESE SIGN PAMAAEH
            { Start = 0x1BAB; Last = 0x1BAD; Width = EastAsianWidth.OfText "N"  } // 1BAB..1BAD;N     # Mn     [3] SUNDANESE SIGN VIRAMA..SUNDANESE CONSONANT SIGN PASANGAN WA
            { Start = 0x1BAE; Last = 0x1BAF; Width = EastAsianWidth.OfText "N"  } // 1BAE..1BAF;N     # Lo     [2] SUNDANESE LETTER KHA..SUNDANESE LETTER SYA
            { Start = 0x1BB0; Last = 0x1BB9; Width = EastAsianWidth.OfText "N"  } // 1BB0..1BB9;N     # Nd    [10] SUNDANESE DIGIT ZERO..SUNDANESE DIGIT NINE
            { Start = 0x1BBA; Last = 0x1BBF; Width = EastAsianWidth.OfText "N"  } // 1BBA..1BBF;N     # Lo     [6] SUNDANESE AVAGRAHA..SUNDANESE LETTER FINAL M
            { Start = 0x1BC0; Last = 0x1BE5; Width = EastAsianWidth.OfText "N"  } // 1BC0..1BE5;N     # Lo    [38] BATAK LETTER A..BATAK LETTER U
            { Start = 0x1BE6; Last = 0x1BE6; Width = EastAsianWidth.OfText "N"  } // 1BE6;N           # Mn         BATAK SIGN TOMPI
            { Start = 0x1BE7; Last = 0x1BE7; Width = EastAsianWidth.OfText "N"  } // 1BE7;N           # Mc         BATAK VOWEL SIGN E
            { Start = 0x1BE8; Last = 0x1BE9; Width = EastAsianWidth.OfText "N"  } // 1BE8..1BE9;N     # Mn     [2] BATAK VOWEL SIGN PAKPAK E..BATAK VOWEL SIGN EE
            { Start = 0x1BEA; Last = 0x1BEC; Width = EastAsianWidth.OfText "N"  } // 1BEA..1BEC;N     # Mc     [3] BATAK VOWEL SIGN I..BATAK VOWEL SIGN O
            { Start = 0x1BED; Last = 0x1BED; Width = EastAsianWidth.OfText "N"  } // 1BED;N           # Mn         BATAK VOWEL SIGN KARO O
            { Start = 0x1BEE; Last = 0x1BEE; Width = EastAsianWidth.OfText "N"  } // 1BEE;N           # Mc         BATAK VOWEL SIGN U
            { Start = 0x1BEF; Last = 0x1BF1; Width = EastAsianWidth.OfText "N"  } // 1BEF..1BF1;N     # Mn     [3] BATAK VOWEL SIGN U FOR SIMALUNGUN SA..BATAK CONSONANT SIGN H
            { Start = 0x1BF2; Last = 0x1BF3; Width = EastAsianWidth.OfText "N"  } // 1BF2..1BF3;N     # Mc     [2] BATAK PANGOLAT..BATAK PANONGONAN
            { Start = 0x1BFC; Last = 0x1BFF; Width = EastAsianWidth.OfText "N"  } // 1BFC..1BFF;N     # Po     [4] BATAK SYMBOL BINDU NA METEK..BATAK SYMBOL BINDU PANGOLAT
            { Start = 0x1C00; Last = 0x1C23; Width = EastAsianWidth.OfText "N"  } // 1C00..1C23;N     # Lo    [36] LEPCHA LETTER KA..LEPCHA LETTER A
            { Start = 0x1C24; Last = 0x1C2B; Width = EastAsianWidth.OfText "N"  } // 1C24..1C2B;N     # Mc     [8] LEPCHA SUBJOINED LETTER YA..LEPCHA VOWEL SIGN UU
            { Start = 0x1C2C; Last = 0x1C33; Width = EastAsianWidth.OfText "N"  } // 1C2C..1C33;N     # Mn     [8] LEPCHA VOWEL SIGN E..LEPCHA CONSONANT SIGN T
            { Start = 0x1C34; Last = 0x1C35; Width = EastAsianWidth.OfText "N"  } // 1C34..1C35;N     # Mc     [2] LEPCHA CONSONANT SIGN NYIN-DO..LEPCHA CONSONANT SIGN KANG
            { Start = 0x1C36; Last = 0x1C37; Width = EastAsianWidth.OfText "N"  } // 1C36..1C37;N     # Mn     [2] LEPCHA SIGN RAN..LEPCHA SIGN NUKTA
            { Start = 0x1C3B; Last = 0x1C3F; Width = EastAsianWidth.OfText "N"  } // 1C3B..1C3F;N     # Po     [5] LEPCHA PUNCTUATION TA-ROL..LEPCHA PUNCTUATION TSHOOK
            { Start = 0x1C40; Last = 0x1C49; Width = EastAsianWidth.OfText "N"  } // 1C40..1C49;N     # Nd    [10] LEPCHA DIGIT ZERO..LEPCHA DIGIT NINE
            { Start = 0x1C4D; Last = 0x1C4F; Width = EastAsianWidth.OfText "N"  } // 1C4D..1C4F;N     # Lo     [3] LEPCHA LETTER TTA..LEPCHA LETTER DDA
            { Start = 0x1C50; Last = 0x1C59; Width = EastAsianWidth.OfText "N"  } // 1C50..1C59;N     # Nd    [10] OL CHIKI DIGIT ZERO..OL CHIKI DIGIT NINE
            { Start = 0x1C5A; Last = 0x1C77; Width = EastAsianWidth.OfText "N"  } // 1C5A..1C77;N     # Lo    [30] OL CHIKI LETTER LA..OL CHIKI LETTER OH
            { Start = 0x1C78; Last = 0x1C7D; Width = EastAsianWidth.OfText "N"  } // 1C78..1C7D;N     # Lm     [6] OL CHIKI MU TTUDDAG..OL CHIKI AHAD
            { Start = 0x1C7E; Last = 0x1C7F; Width = EastAsianWidth.OfText "N"  } // 1C7E..1C7F;N     # Po     [2] OL CHIKI PUNCTUATION MUCAAD..OL CHIKI PUNCTUATION DOUBLE MUCAAD
            { Start = 0x1C80; Last = 0x1C88; Width = EastAsianWidth.OfText "N"  } // 1C80..1C88;N     # Ll     [9] CYRILLIC SMALL LETTER ROUNDED VE..CYRILLIC SMALL LETTER UNBLENDED UK
            { Start = 0x1C90; Last = 0x1CBA; Width = EastAsianWidth.OfText "N"  } // 1C90..1CBA;N     # Lu    [43] GEORGIAN MTAVRULI CAPITAL LETTER AN..GEORGIAN MTAVRULI CAPITAL LETTER AIN
            { Start = 0x1CBD; Last = 0x1CBF; Width = EastAsianWidth.OfText "N"  } // 1CBD..1CBF;N     # Lu     [3] GEORGIAN MTAVRULI CAPITAL LETTER AEN..GEORGIAN MTAVRULI CAPITAL LETTER LABIAL SIGN
            { Start = 0x1CC0; Last = 0x1CC7; Width = EastAsianWidth.OfText "N"  } // 1CC0..1CC7;N     # Po     [8] SUNDANESE PUNCTUATION BINDU SURYA..SUNDANESE PUNCTUATION BINDU BA SATANGA
            { Start = 0x1CD0; Last = 0x1CD2; Width = EastAsianWidth.OfText "N"  } // 1CD0..1CD2;N     # Mn     [3] VEDIC TONE KARSHANA..VEDIC TONE PRENKHA
            { Start = 0x1CD3; Last = 0x1CD3; Width = EastAsianWidth.OfText "N"  } // 1CD3;N           # Po         VEDIC SIGN NIHSHVASA
            { Start = 0x1CD4; Last = 0x1CE0; Width = EastAsianWidth.OfText "N"  } // 1CD4..1CE0;N     # Mn    [13] VEDIC SIGN YAJURVEDIC MIDLINE SVARITA..VEDIC TONE RIGVEDIC KASHMIRI INDEPENDENT SVARITA
            { Start = 0x1CE1; Last = 0x1CE1; Width = EastAsianWidth.OfText "N"  } // 1CE1;N           # Mc         VEDIC TONE ATHARVAVEDIC INDEPENDENT SVARITA
            { Start = 0x1CE2; Last = 0x1CE8; Width = EastAsianWidth.OfText "N"  } // 1CE2..1CE8;N     # Mn     [7] VEDIC SIGN VISARGA SVARITA..VEDIC SIGN VISARGA ANUDATTA WITH TAIL
            { Start = 0x1CE9; Last = 0x1CEC; Width = EastAsianWidth.OfText "N"  } // 1CE9..1CEC;N     # Lo     [4] VEDIC SIGN ANUSVARA ANTARGOMUKHA..VEDIC SIGN ANUSVARA VAMAGOMUKHA WITH TAIL
            { Start = 0x1CED; Last = 0x1CED; Width = EastAsianWidth.OfText "N"  } // 1CED;N           # Mn         VEDIC SIGN TIRYAK
            { Start = 0x1CEE; Last = 0x1CF1; Width = EastAsianWidth.OfText "N"  } // 1CEE..1CF1;N     # Lo     [4] VEDIC SIGN HEXIFORM LONG ANUSVARA..VEDIC SIGN ANUSVARA UBHAYATO MUKHA
            { Start = 0x1CF2; Last = 0x1CF3; Width = EastAsianWidth.OfText "N"  } // 1CF2..1CF3;N     # Mc     [2] VEDIC SIGN ARDHAVISARGA..VEDIC SIGN ROTATED ARDHAVISARGA
            { Start = 0x1CF4; Last = 0x1CF4; Width = EastAsianWidth.OfText "N"  } // 1CF4;N           # Mn         VEDIC TONE CANDRA ABOVE
            { Start = 0x1CF5; Last = 0x1CF6; Width = EastAsianWidth.OfText "N"  } // 1CF5..1CF6;N     # Lo     [2] VEDIC SIGN JIHVAMULIYA..VEDIC SIGN UPADHMANIYA
            { Start = 0x1CF7; Last = 0x1CF7; Width = EastAsianWidth.OfText "N"  } // 1CF7;N           # Mc         VEDIC SIGN ATIKRAMA
            { Start = 0x1CF8; Last = 0x1CF9; Width = EastAsianWidth.OfText "N"  } // 1CF8..1CF9;N     # Mn     [2] VEDIC TONE RING ABOVE..VEDIC TONE DOUBLE RING ABOVE
            { Start = 0x1D00; Last = 0x1D2B; Width = EastAsianWidth.OfText "N"  } // 1D00..1D2B;N     # Ll    [44] LATIN LETTER SMALL CAPITAL A..CYRILLIC LETTER SMALL CAPITAL EL
            { Start = 0x1D2C; Last = 0x1D6A; Width = EastAsianWidth.OfText "N"  } // 1D2C..1D6A;N     # Lm    [63] MODIFIER LETTER CAPITAL A..GREEK SUBSCRIPT SMALL LETTER CHI
            { Start = 0x1D6B; Last = 0x1D77; Width = EastAsianWidth.OfText "N"  } // 1D6B..1D77;N     # Ll    [13] LATIN SMALL LETTER UE..LATIN SMALL LETTER TURNED G
            { Start = 0x1D78; Last = 0x1D78; Width = EastAsianWidth.OfText "N"  } // 1D78;N           # Lm         MODIFIER LETTER CYRILLIC EN
            { Start = 0x1D79; Last = 0x1D7F; Width = EastAsianWidth.OfText "N"  } // 1D79..1D7F;N     # Ll     [7] LATIN SMALL LETTER INSULAR G..LATIN SMALL LETTER UPSILON WITH STROKE
            { Start = 0x1D80; Last = 0x1D9A; Width = EastAsianWidth.OfText "N"  } // 1D80..1D9A;N     # Ll    [27] LATIN SMALL LETTER B WITH PALATAL HOOK..LATIN SMALL LETTER EZH WITH RETROFLEX HOOK
            { Start = 0x1D9B; Last = 0x1DBF; Width = EastAsianWidth.OfText "N"  } // 1D9B..1DBF;N     # Lm    [37] MODIFIER LETTER SMALL TURNED ALPHA..MODIFIER LETTER SMALL THETA
            { Start = 0x1DC0; Last = 0x1DF9; Width = EastAsianWidth.OfText "N"  } // 1DC0..1DF9;N     # Mn    [58] COMBINING DOTTED GRAVE ACCENT..COMBINING WIDE INVERTED BRIDGE BELOW
            { Start = 0x1DFB; Last = 0x1DFF; Width = EastAsianWidth.OfText "N"  } // 1DFB..1DFF;N     # Mn     [5] COMBINING DELETION MARK..COMBINING RIGHT ARROWHEAD AND DOWN ARROWHEAD BELOW
            { Start = 0x1E00; Last = 0x1EFF; Width = EastAsianWidth.OfText "N"  } // 1E00..1EFF;N     # L&   [256] LATIN CAPITAL LETTER A WITH RING BELOW..LATIN SMALL LETTER Y WITH LOOP
            { Start = 0x1F00; Last = 0x1F15; Width = EastAsianWidth.OfText "N"  } // 1F00..1F15;N     # L&    [22] GREEK SMALL LETTER ALPHA WITH PSILI..GREEK SMALL LETTER EPSILON WITH DASIA AND OXIA
            { Start = 0x1F18; Last = 0x1F1D; Width = EastAsianWidth.OfText "N"  } // 1F18..1F1D;N     # Lu     [6] GREEK CAPITAL LETTER EPSILON WITH PSILI..GREEK CAPITAL LETTER EPSILON WITH DASIA AND OXIA
            { Start = 0x1F20; Last = 0x1F45; Width = EastAsianWidth.OfText "N"  } // 1F20..1F45;N     # L&    [38] GREEK SMALL LETTER ETA WITH PSILI..GREEK SMALL LETTER OMICRON WITH DASIA AND OXIA
            { Start = 0x1F48; Last = 0x1F4D; Width = EastAsianWidth.OfText "N"  } // 1F48..1F4D;N     # Lu     [6] GREEK CAPITAL LETTER OMICRON WITH PSILI..GREEK CAPITAL LETTER OMICRON WITH DASIA AND OXIA
            { Start = 0x1F50; Last = 0x1F57; Width = EastAsianWidth.OfText "N"  } // 1F50..1F57;N     # Ll     [8] GREEK SMALL LETTER UPSILON WITH PSILI..GREEK SMALL LETTER UPSILON WITH DASIA AND PERISPOMENI
            { Start = 0x1F59; Last = 0x1F59; Width = EastAsianWidth.OfText "N"  } // 1F59;N           # Lu         GREEK CAPITAL LETTER UPSILON WITH DASIA
            { Start = 0x1F5B; Last = 0x1F5B; Width = EastAsianWidth.OfText "N"  } // 1F5B;N           # Lu         GREEK CAPITAL LETTER UPSILON WITH DASIA AND VARIA
            { Start = 0x1F5D; Last = 0x1F5D; Width = EastAsianWidth.OfText "N"  } // 1F5D;N           # Lu         GREEK CAPITAL LETTER UPSILON WITH DASIA AND OXIA
            { Start = 0x1F5F; Last = 0x1F7D; Width = EastAsianWidth.OfText "N"  } // 1F5F..1F7D;N     # L&    [31] GREEK CAPITAL LETTER UPSILON WITH DASIA AND PERISPOMENI..GREEK SMALL LETTER OMEGA WITH OXIA
            { Start = 0x1F80; Last = 0x1FB4; Width = EastAsianWidth.OfText "N"  } // 1F80..1FB4;N     # L&    [53] GREEK SMALL LETTER ALPHA WITH PSILI AND YPOGEGRAMMENI..GREEK SMALL LETTER ALPHA WITH OXIA AND YPOGEGRAMMENI
            { Start = 0x1FB6; Last = 0x1FBC; Width = EastAsianWidth.OfText "N"  } // 1FB6..1FBC;N     # L&     [7] GREEK SMALL LETTER ALPHA WITH PERISPOMENI..GREEK CAPITAL LETTER ALPHA WITH PROSGEGRAMMENI
            { Start = 0x1FBD; Last = 0x1FBD; Width = EastAsianWidth.OfText "N"  } // 1FBD;N           # Sk         GREEK KORONIS
            { Start = 0x1FBE; Last = 0x1FBE; Width = EastAsianWidth.OfText "N"  } // 1FBE;N           # Ll         GREEK PROSGEGRAMMENI
            { Start = 0x1FBF; Last = 0x1FC1; Width = EastAsianWidth.OfText "N"  } // 1FBF..1FC1;N     # Sk     [3] GREEK PSILI..GREEK DIALYTIKA AND PERISPOMENI
            { Start = 0x1FC2; Last = 0x1FC4; Width = EastAsianWidth.OfText "N"  } // 1FC2..1FC4;N     # Ll     [3] GREEK SMALL LETTER ETA WITH VARIA AND YPOGEGRAMMENI..GREEK SMALL LETTER ETA WITH OXIA AND YPOGEGRAMMENI
            { Start = 0x1FC6; Last = 0x1FCC; Width = EastAsianWidth.OfText "N"  } // 1FC6..1FCC;N     # L&     [7] GREEK SMALL LETTER ETA WITH PERISPOMENI..GREEK CAPITAL LETTER ETA WITH PROSGEGRAMMENI
            { Start = 0x1FCD; Last = 0x1FCF; Width = EastAsianWidth.OfText "N"  } // 1FCD..1FCF;N     # Sk     [3] GREEK PSILI AND VARIA..GREEK PSILI AND PERISPOMENI
            { Start = 0x1FD0; Last = 0x1FD3; Width = EastAsianWidth.OfText "N"  } // 1FD0..1FD3;N     # Ll     [4] GREEK SMALL LETTER IOTA WITH VRACHY..GREEK SMALL LETTER IOTA WITH DIALYTIKA AND OXIA
            { Start = 0x1FD6; Last = 0x1FDB; Width = EastAsianWidth.OfText "N"  } // 1FD6..1FDB;N     # L&     [6] GREEK SMALL LETTER IOTA WITH PERISPOMENI..GREEK CAPITAL LETTER IOTA WITH OXIA
            { Start = 0x1FDD; Last = 0x1FDF; Width = EastAsianWidth.OfText "N"  } // 1FDD..1FDF;N     # Sk     [3] GREEK DASIA AND VARIA..GREEK DASIA AND PERISPOMENI
            { Start = 0x1FE0; Last = 0x1FEC; Width = EastAsianWidth.OfText "N"  } // 1FE0..1FEC;N     # L&    [13] GREEK SMALL LETTER UPSILON WITH VRACHY..GREEK CAPITAL LETTER RHO WITH DASIA
            { Start = 0x1FED; Last = 0x1FEF; Width = EastAsianWidth.OfText "N"  } // 1FED..1FEF;N     # Sk     [3] GREEK DIALYTIKA AND VARIA..GREEK VARIA
            { Start = 0x1FF2; Last = 0x1FF4; Width = EastAsianWidth.OfText "N"  } // 1FF2..1FF4;N     # Ll     [3] GREEK SMALL LETTER OMEGA WITH VARIA AND YPOGEGRAMMENI..GREEK SMALL LETTER OMEGA WITH OXIA AND YPOGEGRAMMENI
            { Start = 0x1FF6; Last = 0x1FFC; Width = EastAsianWidth.OfText "N"  } // 1FF6..1FFC;N     # L&     [7] GREEK SMALL LETTER OMEGA WITH PERISPOMENI..GREEK CAPITAL LETTER OMEGA WITH PROSGEGRAMMENI
            { Start = 0x1FFD; Last = 0x1FFE; Width = EastAsianWidth.OfText "N"  } // 1FFD..1FFE;N     # Sk     [2] GREEK OXIA..GREEK DASIA
            { Start = 0x2000; Last = 0x200A; Width = EastAsianWidth.OfText "N"  } // 2000..200A;N     # Zs    [11] EN QUAD..HAIR SPACE
            { Start = 0x200B; Last = 0x200F; Width = EastAsianWidth.OfText "N"  } // 200B..200F;N     # Cf     [5] ZERO WIDTH SPACE..RIGHT-TO-LEFT MARK
            { Start = 0x2010; Last = 0x2010; Width = EastAsianWidth.OfText "A"  } // 2010;A           # Pd         HYPHEN
            { Start = 0x2011; Last = 0x2012; Width = EastAsianWidth.OfText "N"  } // 2011..2012;N     # Pd     [2] NON-BREAKING HYPHEN..FIGURE DASH
            { Start = 0x2013; Last = 0x2015; Width = EastAsianWidth.OfText "A"  } // 2013..2015;A     # Pd     [3] EN DASH..HORIZONTAL BAR
            { Start = 0x2016; Last = 0x2016; Width = EastAsianWidth.OfText "A"  } // 2016;A           # Po         DOUBLE VERTICAL LINE
            { Start = 0x2017; Last = 0x2017; Width = EastAsianWidth.OfText "N"  } // 2017;N           # Po         DOUBLE LOW LINE
            { Start = 0x2018; Last = 0x2018; Width = EastAsianWidth.OfText "A"  } // 2018;A           # Pi         LEFT SINGLE QUOTATION MARK
            { Start = 0x2019; Last = 0x2019; Width = EastAsianWidth.OfText "A"  } // 2019;A           # Pf         RIGHT SINGLE QUOTATION MARK
            { Start = 0x201A; Last = 0x201A; Width = EastAsianWidth.OfText "N"  } // 201A;N           # Ps         SINGLE LOW-9 QUOTATION MARK
            { Start = 0x201B; Last = 0x201B; Width = EastAsianWidth.OfText "N"  } // 201B;N           # Pi         SINGLE HIGH-REVERSED-9 QUOTATION MARK
            { Start = 0x201C; Last = 0x201C; Width = EastAsianWidth.OfText "A"  } // 201C;A           # Pi         LEFT DOUBLE QUOTATION MARK
            { Start = 0x201D; Last = 0x201D; Width = EastAsianWidth.OfText "A"  } // 201D;A           # Pf         RIGHT DOUBLE QUOTATION MARK
            { Start = 0x201E; Last = 0x201E; Width = EastAsianWidth.OfText "N"  } // 201E;N           # Ps         DOUBLE LOW-9 QUOTATION MARK
            { Start = 0x201F; Last = 0x201F; Width = EastAsianWidth.OfText "N"  } // 201F;N           # Pi         DOUBLE HIGH-REVERSED-9 QUOTATION MARK
            { Start = 0x2020; Last = 0x2022; Width = EastAsianWidth.OfText "A"  } // 2020..2022;A     # Po     [3] DAGGER..BULLET
            { Start = 0x2023; Last = 0x2023; Width = EastAsianWidth.OfText "N"  } // 2023;N           # Po         TRIANGULAR BULLET
            { Start = 0x2024; Last = 0x2027; Width = EastAsianWidth.OfText "A"  } // 2024..2027;A     # Po     [4] ONE DOT LEADER..HYPHENATION POINT
            { Start = 0x2028; Last = 0x2028; Width = EastAsianWidth.OfText "N"  } // 2028;N           # Zl         LINE SEPARATOR
            { Start = 0x2029; Last = 0x2029; Width = EastAsianWidth.OfText "N"  } // 2029;N           # Zp         PARAGRAPH SEPARATOR
            { Start = 0x202A; Last = 0x202E; Width = EastAsianWidth.OfText "N"  } // 202A..202E;N     # Cf     [5] LEFT-TO-RIGHT EMBEDDING..RIGHT-TO-LEFT OVERRIDE
            { Start = 0x202F; Last = 0x202F; Width = EastAsianWidth.OfText "N"  } // 202F;N           # Zs         NARROW NO-BREAK SPACE
            { Start = 0x2030; Last = 0x2030; Width = EastAsianWidth.OfText "A"  } // 2030;A           # Po         PER MILLE SIGN
            { Start = 0x2031; Last = 0x2031; Width = EastAsianWidth.OfText "N"  } // 2031;N           # Po         PER TEN THOUSAND SIGN
            { Start = 0x2032; Last = 0x2033; Width = EastAsianWidth.OfText "A"  } // 2032..2033;A     # Po     [2] PRIME..DOUBLE PRIME
            { Start = 0x2034; Last = 0x2034; Width = EastAsianWidth.OfText "N"  } // 2034;N           # Po         TRIPLE PRIME
            { Start = 0x2035; Last = 0x2035; Width = EastAsianWidth.OfText "A"  } // 2035;A           # Po         REVERSED PRIME
            { Start = 0x2036; Last = 0x2038; Width = EastAsianWidth.OfText "N"  } // 2036..2038;N     # Po     [3] REVERSED DOUBLE PRIME..CARET
            { Start = 0x2039; Last = 0x2039; Width = EastAsianWidth.OfText "N"  } // 2039;N           # Pi         SINGLE LEFT-POINTING ANGLE QUOTATION MARK
            { Start = 0x203A; Last = 0x203A; Width = EastAsianWidth.OfText "N"  } // 203A;N           # Pf         SINGLE RIGHT-POINTING ANGLE QUOTATION MARK
            { Start = 0x203B; Last = 0x203B; Width = EastAsianWidth.OfText "A"  } // 203B;A           # Po         REFERENCE MARK
            { Start = 0x203C; Last = 0x203D; Width = EastAsianWidth.OfText "N"  } // 203C..203D;N     # Po     [2] DOUBLE EXCLAMATION MARK..INTERROBANG
            { Start = 0x203E; Last = 0x203E; Width = EastAsianWidth.OfText "A"  } // 203E;A           # Po         OVERLINE
            { Start = 0x203F; Last = 0x2040; Width = EastAsianWidth.OfText "N"  } // 203F..2040;N     # Pc     [2] UNDERTIE..CHARACTER TIE
            { Start = 0x2041; Last = 0x2043; Width = EastAsianWidth.OfText "N"  } // 2041..2043;N     # Po     [3] CARET INSERTION POINT..HYPHEN BULLET
            { Start = 0x2044; Last = 0x2044; Width = EastAsianWidth.OfText "N"  } // 2044;N           # Sm         FRACTION SLASH
            { Start = 0x2045; Last = 0x2045; Width = EastAsianWidth.OfText "N"  } // 2045;N           # Ps         LEFT SQUARE BRACKET WITH QUILL
            { Start = 0x2046; Last = 0x2046; Width = EastAsianWidth.OfText "N"  } // 2046;N           # Pe         RIGHT SQUARE BRACKET WITH QUILL
            { Start = 0x2047; Last = 0x2051; Width = EastAsianWidth.OfText "N"  } // 2047..2051;N     # Po    [11] DOUBLE QUESTION MARK..TWO ASTERISKS ALIGNED VERTICALLY
            { Start = 0x2052; Last = 0x2052; Width = EastAsianWidth.OfText "N"  } // 2052;N           # Sm         COMMERCIAL MINUS SIGN
            { Start = 0x2053; Last = 0x2053; Width = EastAsianWidth.OfText "N"  } // 2053;N           # Po         SWUNG DASH
            { Start = 0x2054; Last = 0x2054; Width = EastAsianWidth.OfText "N"  } // 2054;N           # Pc         INVERTED UNDERTIE
            { Start = 0x2055; Last = 0x205E; Width = EastAsianWidth.OfText "N"  } // 2055..205E;N     # Po    [10] FLOWER PUNCTUATION MARK..VERTICAL FOUR DOTS
            { Start = 0x205F; Last = 0x205F; Width = EastAsianWidth.OfText "N"  } // 205F;N           # Zs         MEDIUM MATHEMATICAL SPACE
            { Start = 0x2060; Last = 0x2064; Width = EastAsianWidth.OfText "N"  } // 2060..2064;N     # Cf     [5] WORD JOINER..INVISIBLE PLUS
            { Start = 0x2066; Last = 0x206F; Width = EastAsianWidth.OfText "N"  } // 2066..206F;N     # Cf    [10] LEFT-TO-RIGHT ISOLATE..NOMINAL DIGIT SHAPES
            { Start = 0x2070; Last = 0x2070; Width = EastAsianWidth.OfText "N"  } // 2070;N           # No         SUPERSCRIPT ZERO
            { Start = 0x2071; Last = 0x2071; Width = EastAsianWidth.OfText "N"  } // 2071;N           # Lm         SUPERSCRIPT LATIN SMALL LETTER I
            { Start = 0x2074; Last = 0x2074; Width = EastAsianWidth.OfText "A"  } // 2074;A           # No         SUPERSCRIPT FOUR
            { Start = 0x2075; Last = 0x2079; Width = EastAsianWidth.OfText "N"  } // 2075..2079;N     # No     [5] SUPERSCRIPT FIVE..SUPERSCRIPT NINE
            { Start = 0x207A; Last = 0x207C; Width = EastAsianWidth.OfText "N"  } // 207A..207C;N     # Sm     [3] SUPERSCRIPT PLUS SIGN..SUPERSCRIPT EQUALS SIGN
            { Start = 0x207D; Last = 0x207D; Width = EastAsianWidth.OfText "N"  } // 207D;N           # Ps         SUPERSCRIPT LEFT PARENTHESIS
            { Start = 0x207E; Last = 0x207E; Width = EastAsianWidth.OfText "N"  } // 207E;N           # Pe         SUPERSCRIPT RIGHT PARENTHESIS
            { Start = 0x207F; Last = 0x207F; Width = EastAsianWidth.OfText "A"  } // 207F;A           # Lm         SUPERSCRIPT LATIN SMALL LETTER N
            { Start = 0x2080; Last = 0x2080; Width = EastAsianWidth.OfText "N"  } // 2080;N           # No         SUBSCRIPT ZERO
            { Start = 0x2081; Last = 0x2084; Width = EastAsianWidth.OfText "A"  } // 2081..2084;A     # No     [4] SUBSCRIPT ONE..SUBSCRIPT FOUR
            { Start = 0x2085; Last = 0x2089; Width = EastAsianWidth.OfText "N"  } // 2085..2089;N     # No     [5] SUBSCRIPT FIVE..SUBSCRIPT NINE
            { Start = 0x208A; Last = 0x208C; Width = EastAsianWidth.OfText "N"  } // 208A..208C;N     # Sm     [3] SUBSCRIPT PLUS SIGN..SUBSCRIPT EQUALS SIGN
            { Start = 0x208D; Last = 0x208D; Width = EastAsianWidth.OfText "N"  } // 208D;N           # Ps         SUBSCRIPT LEFT PARENTHESIS
            { Start = 0x208E; Last = 0x208E; Width = EastAsianWidth.OfText "N"  } // 208E;N           # Pe         SUBSCRIPT RIGHT PARENTHESIS
            { Start = 0x2090; Last = 0x209C; Width = EastAsianWidth.OfText "N"  } // 2090..209C;N     # Lm    [13] LATIN SUBSCRIPT SMALL LETTER A..LATIN SUBSCRIPT SMALL LETTER T
            { Start = 0x20A0; Last = 0x20A8; Width = EastAsianWidth.OfText "N"  } // 20A0..20A8;N     # Sc     [9] EURO-CURRENCY SIGN..RUPEE SIGN
            { Start = 0x20A9; Last = 0x20A9; Width = EastAsianWidth.OfText "H"  } // 20A9;H           # Sc         WON SIGN
            { Start = 0x20AA; Last = 0x20AB; Width = EastAsianWidth.OfText "N"  } // 20AA..20AB;N     # Sc     [2] NEW SHEQEL SIGN..DONG SIGN
            { Start = 0x20AC; Last = 0x20AC; Width = EastAsianWidth.OfText "A"  } // 20AC;A           # Sc         EURO SIGN
            { Start = 0x20AD; Last = 0x20BF; Width = EastAsianWidth.OfText "N"  } // 20AD..20BF;N     # Sc    [19] KIP SIGN..BITCOIN SIGN
            { Start = 0x20D0; Last = 0x20DC; Width = EastAsianWidth.OfText "N"  } // 20D0..20DC;N     # Mn    [13] COMBINING LEFT HARPOON ABOVE..COMBINING FOUR DOTS ABOVE
            { Start = 0x20DD; Last = 0x20E0; Width = EastAsianWidth.OfText "N"  } // 20DD..20E0;N     # Me     [4] COMBINING ENCLOSING CIRCLE..COMBINING ENCLOSING CIRCLE BACKSLASH
            { Start = 0x20E1; Last = 0x20E1; Width = EastAsianWidth.OfText "N"  } // 20E1;N           # Mn         COMBINING LEFT RIGHT ARROW ABOVE
            { Start = 0x20E2; Last = 0x20E4; Width = EastAsianWidth.OfText "N"  } // 20E2..20E4;N     # Me     [3] COMBINING ENCLOSING SCREEN..COMBINING ENCLOSING UPWARD POINTING TRIANGLE
            { Start = 0x20E5; Last = 0x20F0; Width = EastAsianWidth.OfText "N"  } // 20E5..20F0;N     # Mn    [12] COMBINING REVERSE SOLIDUS OVERLAY..COMBINING ASTERISK ABOVE
            { Start = 0x2100; Last = 0x2101; Width = EastAsianWidth.OfText "N"  } // 2100..2101;N     # So     [2] ACCOUNT OF..ADDRESSED TO THE SUBJECT
            { Start = 0x2102; Last = 0x2102; Width = EastAsianWidth.OfText "N"  } // 2102;N           # Lu         DOUBLE-STRUCK CAPITAL C
            { Start = 0x2103; Last = 0x2103; Width = EastAsianWidth.OfText "A"  } // 2103;A           # So         DEGREE CELSIUS
            { Start = 0x2104; Last = 0x2104; Width = EastAsianWidth.OfText "N"  } // 2104;N           # So         CENTRE LINE SYMBOL
            { Start = 0x2105; Last = 0x2105; Width = EastAsianWidth.OfText "A"  } // 2105;A           # So         CARE OF
            { Start = 0x2106; Last = 0x2106; Width = EastAsianWidth.OfText "N"  } // 2106;N           # So         CADA UNA
            { Start = 0x2107; Last = 0x2107; Width = EastAsianWidth.OfText "N"  } // 2107;N           # Lu         EULER CONSTANT
            { Start = 0x2108; Last = 0x2108; Width = EastAsianWidth.OfText "N"  } // 2108;N           # So         SCRUPLE
            { Start = 0x2109; Last = 0x2109; Width = EastAsianWidth.OfText "A"  } // 2109;A           # So         DEGREE FAHRENHEIT
            { Start = 0x210A; Last = 0x2112; Width = EastAsianWidth.OfText "N"  } // 210A..2112;N     # L&     [9] SCRIPT SMALL G..SCRIPT CAPITAL L
            { Start = 0x2113; Last = 0x2113; Width = EastAsianWidth.OfText "A"  } // 2113;A           # Ll         SCRIPT SMALL L
            { Start = 0x2114; Last = 0x2114; Width = EastAsianWidth.OfText "N"  } // 2114;N           # So         L B BAR SYMBOL
            { Start = 0x2115; Last = 0x2115; Width = EastAsianWidth.OfText "N"  } // 2115;N           # Lu         DOUBLE-STRUCK CAPITAL N
            { Start = 0x2116; Last = 0x2116; Width = EastAsianWidth.OfText "A"  } // 2116;A           # So         NUMERO SIGN
            { Start = 0x2117; Last = 0x2117; Width = EastAsianWidth.OfText "N"  } // 2117;N           # So         SOUND RECORDING COPYRIGHT
            { Start = 0x2118; Last = 0x2118; Width = EastAsianWidth.OfText "N"  } // 2118;N           # Sm         SCRIPT CAPITAL P
            { Start = 0x2119; Last = 0x211D; Width = EastAsianWidth.OfText "N"  } // 2119..211D;N     # Lu     [5] DOUBLE-STRUCK CAPITAL P..DOUBLE-STRUCK CAPITAL R
            { Start = 0x211E; Last = 0x2120; Width = EastAsianWidth.OfText "N"  } // 211E..2120;N     # So     [3] PRESCRIPTION TAKE..SERVICE MARK
            { Start = 0x2121; Last = 0x2122; Width = EastAsianWidth.OfText "A"  } // 2121..2122;A     # So     [2] TELEPHONE SIGN..TRADE MARK SIGN
            { Start = 0x2123; Last = 0x2123; Width = EastAsianWidth.OfText "N"  } // 2123;N           # So         VERSICLE
            { Start = 0x2124; Last = 0x2124; Width = EastAsianWidth.OfText "N"  } // 2124;N           # Lu         DOUBLE-STRUCK CAPITAL Z
            { Start = 0x2125; Last = 0x2125; Width = EastAsianWidth.OfText "N"  } // 2125;N           # So         OUNCE SIGN
            { Start = 0x2126; Last = 0x2126; Width = EastAsianWidth.OfText "A"  } // 2126;A           # Lu         OHM SIGN
            { Start = 0x2127; Last = 0x2127; Width = EastAsianWidth.OfText "N"  } // 2127;N           # So         INVERTED OHM SIGN
            { Start = 0x2128; Last = 0x2128; Width = EastAsianWidth.OfText "N"  } // 2128;N           # Lu         BLACK-LETTER CAPITAL Z
            { Start = 0x2129; Last = 0x2129; Width = EastAsianWidth.OfText "N"  } // 2129;N           # So         TURNED GREEK SMALL LETTER IOTA
            { Start = 0x212A; Last = 0x212A; Width = EastAsianWidth.OfText "N"  } // 212A;N           # Lu         KELVIN SIGN
            { Start = 0x212B; Last = 0x212B; Width = EastAsianWidth.OfText "A"  } // 212B;A           # Lu         ANGSTROM SIGN
            { Start = 0x212C; Last = 0x212D; Width = EastAsianWidth.OfText "N"  } // 212C..212D;N     # Lu     [2] SCRIPT CAPITAL B..BLACK-LETTER CAPITAL C
            { Start = 0x212E; Last = 0x212E; Width = EastAsianWidth.OfText "N"  } // 212E;N           # So         ESTIMATED SYMBOL
            { Start = 0x212F; Last = 0x2134; Width = EastAsianWidth.OfText "N"  } // 212F..2134;N     # L&     [6] SCRIPT SMALL E..SCRIPT SMALL O
            { Start = 0x2135; Last = 0x2138; Width = EastAsianWidth.OfText "N"  } // 2135..2138;N     # Lo     [4] ALEF SYMBOL..DALET SYMBOL
            { Start = 0x2139; Last = 0x2139; Width = EastAsianWidth.OfText "N"  } // 2139;N           # Ll         INFORMATION SOURCE
            { Start = 0x213A; Last = 0x213B; Width = EastAsianWidth.OfText "N"  } // 213A..213B;N     # So     [2] ROTATED CAPITAL Q..FACSIMILE SIGN
            { Start = 0x213C; Last = 0x213F; Width = EastAsianWidth.OfText "N"  } // 213C..213F;N     # L&     [4] DOUBLE-STRUCK SMALL PI..DOUBLE-STRUCK CAPITAL PI
            { Start = 0x2140; Last = 0x2144; Width = EastAsianWidth.OfText "N"  } // 2140..2144;N     # Sm     [5] DOUBLE-STRUCK N-ARY SUMMATION..TURNED SANS-SERIF CAPITAL Y
            { Start = 0x2145; Last = 0x2149; Width = EastAsianWidth.OfText "N"  } // 2145..2149;N     # L&     [5] DOUBLE-STRUCK ITALIC CAPITAL D..DOUBLE-STRUCK ITALIC SMALL J
            { Start = 0x214A; Last = 0x214A; Width = EastAsianWidth.OfText "N"  } // 214A;N           # So         PROPERTY LINE
            { Start = 0x214B; Last = 0x214B; Width = EastAsianWidth.OfText "N"  } // 214B;N           # Sm         TURNED AMPERSAND
            { Start = 0x214C; Last = 0x214D; Width = EastAsianWidth.OfText "N"  } // 214C..214D;N     # So     [2] PER SIGN..AKTIESELSKAB
            { Start = 0x214E; Last = 0x214E; Width = EastAsianWidth.OfText "N"  } // 214E;N           # Ll         TURNED SMALL F
            { Start = 0x214F; Last = 0x214F; Width = EastAsianWidth.OfText "N"  } // 214F;N           # So         SYMBOL FOR SAMARITAN SOURCE
            { Start = 0x2150; Last = 0x2152; Width = EastAsianWidth.OfText "N"  } // 2150..2152;N     # No     [3] VULGAR FRACTION ONE SEVENTH..VULGAR FRACTION ONE TENTH
            { Start = 0x2153; Last = 0x2154; Width = EastAsianWidth.OfText "A"  } // 2153..2154;A     # No     [2] VULGAR FRACTION ONE THIRD..VULGAR FRACTION TWO THIRDS
            { Start = 0x2155; Last = 0x215A; Width = EastAsianWidth.OfText "N"  } // 2155..215A;N     # No     [6] VULGAR FRACTION ONE FIFTH..VULGAR FRACTION FIVE SIXTHS
            { Start = 0x215B; Last = 0x215E; Width = EastAsianWidth.OfText "A"  } // 215B..215E;A     # No     [4] VULGAR FRACTION ONE EIGHTH..VULGAR FRACTION SEVEN EIGHTHS
            { Start = 0x215F; Last = 0x215F; Width = EastAsianWidth.OfText "N"  } // 215F;N           # No         FRACTION NUMERATOR ONE
            { Start = 0x2160; Last = 0x216B; Width = EastAsianWidth.OfText "A"  } // 2160..216B;A     # Nl    [12] ROMAN NUMERAL ONE..ROMAN NUMERAL TWELVE
            { Start = 0x216C; Last = 0x216F; Width = EastAsianWidth.OfText "N"  } // 216C..216F;N     # Nl     [4] ROMAN NUMERAL FIFTY..ROMAN NUMERAL ONE THOUSAND
            { Start = 0x2170; Last = 0x2179; Width = EastAsianWidth.OfText "A"  } // 2170..2179;A     # Nl    [10] SMALL ROMAN NUMERAL ONE..SMALL ROMAN NUMERAL TEN
            { Start = 0x217A; Last = 0x2182; Width = EastAsianWidth.OfText "N"  } // 217A..2182;N     # Nl     [9] SMALL ROMAN NUMERAL ELEVEN..ROMAN NUMERAL TEN THOUSAND
            { Start = 0x2183; Last = 0x2184; Width = EastAsianWidth.OfText "N"  } // 2183..2184;N     # L&     [2] ROMAN NUMERAL REVERSED ONE HUNDRED..LATIN SMALL LETTER REVERSED C
            { Start = 0x2185; Last = 0x2188; Width = EastAsianWidth.OfText "N"  } // 2185..2188;N     # Nl     [4] ROMAN NUMERAL SIX LATE FORM..ROMAN NUMERAL ONE HUNDRED THOUSAND
            { Start = 0x2189; Last = 0x2189; Width = EastAsianWidth.OfText "A"  } // 2189;A           # No         VULGAR FRACTION ZERO THIRDS
            { Start = 0x218A; Last = 0x218B; Width = EastAsianWidth.OfText "N"  } // 218A..218B;N     # So     [2] TURNED DIGIT TWO..TURNED DIGIT THREE
            { Start = 0x2190; Last = 0x2194; Width = EastAsianWidth.OfText "A"  } // 2190..2194;A     # Sm     [5] LEFTWARDS ARROW..LEFT RIGHT ARROW
            { Start = 0x2195; Last = 0x2199; Width = EastAsianWidth.OfText "A"  } // 2195..2199;A     # So     [5] UP DOWN ARROW..SOUTH WEST ARROW
            { Start = 0x219A; Last = 0x219B; Width = EastAsianWidth.OfText "N"  } // 219A..219B;N     # Sm     [2] LEFTWARDS ARROW WITH STROKE..RIGHTWARDS ARROW WITH STROKE
            { Start = 0x219C; Last = 0x219F; Width = EastAsianWidth.OfText "N"  } // 219C..219F;N     # So     [4] LEFTWARDS WAVE ARROW..UPWARDS TWO HEADED ARROW
            { Start = 0x21A0; Last = 0x21A0; Width = EastAsianWidth.OfText "N"  } // 21A0;N           # Sm         RIGHTWARDS TWO HEADED ARROW
            { Start = 0x21A1; Last = 0x21A2; Width = EastAsianWidth.OfText "N"  } // 21A1..21A2;N     # So     [2] DOWNWARDS TWO HEADED ARROW..LEFTWARDS ARROW WITH TAIL
            { Start = 0x21A3; Last = 0x21A3; Width = EastAsianWidth.OfText "N"  } // 21A3;N           # Sm         RIGHTWARDS ARROW WITH TAIL
            { Start = 0x21A4; Last = 0x21A5; Width = EastAsianWidth.OfText "N"  } // 21A4..21A5;N     # So     [2] LEFTWARDS ARROW FROM BAR..UPWARDS ARROW FROM BAR
            { Start = 0x21A6; Last = 0x21A6; Width = EastAsianWidth.OfText "N"  } // 21A6;N           # Sm         RIGHTWARDS ARROW FROM BAR
            { Start = 0x21A7; Last = 0x21AD; Width = EastAsianWidth.OfText "N"  } // 21A7..21AD;N     # So     [7] DOWNWARDS ARROW FROM BAR..LEFT RIGHT WAVE ARROW
            { Start = 0x21AE; Last = 0x21AE; Width = EastAsianWidth.OfText "N"  } // 21AE;N           # Sm         LEFT RIGHT ARROW WITH STROKE
            { Start = 0x21AF; Last = 0x21B7; Width = EastAsianWidth.OfText "N"  } // 21AF..21B7;N     # So     [9] DOWNWARDS ZIGZAG ARROW..CLOCKWISE TOP SEMICIRCLE ARROW
            { Start = 0x21B8; Last = 0x21B9; Width = EastAsianWidth.OfText "A"  } // 21B8..21B9;A     # So     [2] NORTH WEST ARROW TO LONG BAR..LEFTWARDS ARROW TO BAR OVER RIGHTWARDS ARROW TO BAR
            { Start = 0x21BA; Last = 0x21CD; Width = EastAsianWidth.OfText "N"  } // 21BA..21CD;N     # So    [20] ANTICLOCKWISE OPEN CIRCLE ARROW..LEFTWARDS DOUBLE ARROW WITH STROKE
            { Start = 0x21CE; Last = 0x21CF; Width = EastAsianWidth.OfText "N"  } // 21CE..21CF;N     # Sm     [2] LEFT RIGHT DOUBLE ARROW WITH STROKE..RIGHTWARDS DOUBLE ARROW WITH STROKE
            { Start = 0x21D0; Last = 0x21D1; Width = EastAsianWidth.OfText "N"  } // 21D0..21D1;N     # So     [2] LEFTWARDS DOUBLE ARROW..UPWARDS DOUBLE ARROW
            { Start = 0x21D2; Last = 0x21D2; Width = EastAsianWidth.OfText "A"  } // 21D2;A           # Sm         RIGHTWARDS DOUBLE ARROW
            { Start = 0x21D3; Last = 0x21D3; Width = EastAsianWidth.OfText "N"  } // 21D3;N           # So         DOWNWARDS DOUBLE ARROW
            { Start = 0x21D4; Last = 0x21D4; Width = EastAsianWidth.OfText "A"  } // 21D4;A           # Sm         LEFT RIGHT DOUBLE ARROW
            { Start = 0x21D5; Last = 0x21E6; Width = EastAsianWidth.OfText "N"  } // 21D5..21E6;N     # So    [18] UP DOWN DOUBLE ARROW..LEFTWARDS WHITE ARROW
            { Start = 0x21E7; Last = 0x21E7; Width = EastAsianWidth.OfText "A"  } // 21E7;A           # So         UPWARDS WHITE ARROW
            { Start = 0x21E8; Last = 0x21F3; Width = EastAsianWidth.OfText "N"  } // 21E8..21F3;N     # So    [12] RIGHTWARDS WHITE ARROW..UP DOWN WHITE ARROW
            { Start = 0x21F4; Last = 0x21FF; Width = EastAsianWidth.OfText "N"  } // 21F4..21FF;N     # Sm    [12] RIGHT ARROW WITH SMALL CIRCLE..LEFT RIGHT OPEN-HEADED ARROW
            { Start = 0x2200; Last = 0x2200; Width = EastAsianWidth.OfText "A"  } // 2200;A           # Sm         FOR ALL
            { Start = 0x2201; Last = 0x2201; Width = EastAsianWidth.OfText "N"  } // 2201;N           # Sm         COMPLEMENT
            { Start = 0x2202; Last = 0x2203; Width = EastAsianWidth.OfText "A"  } // 2202..2203;A     # Sm     [2] PARTIAL DIFFERENTIAL..THERE EXISTS
            { Start = 0x2204; Last = 0x2206; Width = EastAsianWidth.OfText "N"  } // 2204..2206;N     # Sm     [3] THERE DOES NOT EXIST..INCREMENT
            { Start = 0x2207; Last = 0x2208; Width = EastAsianWidth.OfText "A"  } // 2207..2208;A     # Sm     [2] NABLA..ELEMENT OF
            { Start = 0x2209; Last = 0x220A; Width = EastAsianWidth.OfText "N"  } // 2209..220A;N     # Sm     [2] NOT AN ELEMENT OF..SMALL ELEMENT OF
            { Start = 0x220B; Last = 0x220B; Width = EastAsianWidth.OfText "A"  } // 220B;A           # Sm         CONTAINS AS MEMBER
            { Start = 0x220C; Last = 0x220E; Width = EastAsianWidth.OfText "N"  } // 220C..220E;N     # Sm     [3] DOES NOT CONTAIN AS MEMBER..END OF PROOF
            { Start = 0x220F; Last = 0x220F; Width = EastAsianWidth.OfText "A"  } // 220F;A           # Sm         N-ARY PRODUCT
            { Start = 0x2210; Last = 0x2210; Width = EastAsianWidth.OfText "N"  } // 2210;N           # Sm         N-ARY COPRODUCT
            { Start = 0x2211; Last = 0x2211; Width = EastAsianWidth.OfText "A"  } // 2211;A           # Sm         N-ARY SUMMATION
            { Start = 0x2212; Last = 0x2214; Width = EastAsianWidth.OfText "N"  } // 2212..2214;N     # Sm     [3] MINUS SIGN..DOT PLUS
            { Start = 0x2215; Last = 0x2215; Width = EastAsianWidth.OfText "A"  } // 2215;A           # Sm         DIVISION SLASH
            { Start = 0x2216; Last = 0x2219; Width = EastAsianWidth.OfText "N"  } // 2216..2219;N     # Sm     [4] SET MINUS..BULLET OPERATOR
            { Start = 0x221A; Last = 0x221A; Width = EastAsianWidth.OfText "A"  } // 221A;A           # Sm         SQUARE ROOT
            { Start = 0x221B; Last = 0x221C; Width = EastAsianWidth.OfText "N"  } // 221B..221C;N     # Sm     [2] CUBE ROOT..FOURTH ROOT
            { Start = 0x221D; Last = 0x2220; Width = EastAsianWidth.OfText "A"  } // 221D..2220;A     # Sm     [4] PROPORTIONAL TO..ANGLE
            { Start = 0x2221; Last = 0x2222; Width = EastAsianWidth.OfText "N"  } // 2221..2222;N     # Sm     [2] MEASURED ANGLE..SPHERICAL ANGLE
            { Start = 0x2223; Last = 0x2223; Width = EastAsianWidth.OfText "A"  } // 2223;A           # Sm         DIVIDES
            { Start = 0x2224; Last = 0x2224; Width = EastAsianWidth.OfText "N"  } // 2224;N           # Sm         DOES NOT DIVIDE
            { Start = 0x2225; Last = 0x2225; Width = EastAsianWidth.OfText "A"  } // 2225;A           # Sm         PARALLEL TO
            { Start = 0x2226; Last = 0x2226; Width = EastAsianWidth.OfText "N"  } // 2226;N           # Sm         NOT PARALLEL TO
            { Start = 0x2227; Last = 0x222C; Width = EastAsianWidth.OfText "A"  } // 2227..222C;A     # Sm     [6] LOGICAL AND..DOUBLE INTEGRAL
            { Start = 0x222D; Last = 0x222D; Width = EastAsianWidth.OfText "N"  } // 222D;N           # Sm         TRIPLE INTEGRAL
            { Start = 0x222E; Last = 0x222E; Width = EastAsianWidth.OfText "A"  } // 222E;A           # Sm         CONTOUR INTEGRAL
            { Start = 0x222F; Last = 0x2233; Width = EastAsianWidth.OfText "N"  } // 222F..2233;N     # Sm     [5] SURFACE INTEGRAL..ANTICLOCKWISE CONTOUR INTEGRAL
            { Start = 0x2234; Last = 0x2237; Width = EastAsianWidth.OfText "A"  } // 2234..2237;A     # Sm     [4] THEREFORE..PROPORTION
            { Start = 0x2238; Last = 0x223B; Width = EastAsianWidth.OfText "N"  } // 2238..223B;N     # Sm     [4] DOT MINUS..HOMOTHETIC
            { Start = 0x223C; Last = 0x223D; Width = EastAsianWidth.OfText "A"  } // 223C..223D;A     # Sm     [2] TILDE OPERATOR..REVERSED TILDE
            { Start = 0x223E; Last = 0x2247; Width = EastAsianWidth.OfText "N"  } // 223E..2247;N     # Sm    [10] INVERTED LAZY S..NEITHER APPROXIMATELY NOR ACTUALLY EQUAL TO
            { Start = 0x2248; Last = 0x2248; Width = EastAsianWidth.OfText "A"  } // 2248;A           # Sm         ALMOST EQUAL TO
            { Start = 0x2249; Last = 0x224B; Width = EastAsianWidth.OfText "N"  } // 2249..224B;N     # Sm     [3] NOT ALMOST EQUAL TO..TRIPLE TILDE
            { Start = 0x224C; Last = 0x224C; Width = EastAsianWidth.OfText "A"  } // 224C;A           # Sm         ALL EQUAL TO
            { Start = 0x224D; Last = 0x2251; Width = EastAsianWidth.OfText "N"  } // 224D..2251;N     # Sm     [5] EQUIVALENT TO..GEOMETRICALLY EQUAL TO
            { Start = 0x2252; Last = 0x2252; Width = EastAsianWidth.OfText "A"  } // 2252;A           # Sm         APPROXIMATELY EQUAL TO OR THE IMAGE OF
            { Start = 0x2253; Last = 0x225F; Width = EastAsianWidth.OfText "N"  } // 2253..225F;N     # Sm    [13] IMAGE OF OR APPROXIMATELY EQUAL TO..QUESTIONED EQUAL TO
            { Start = 0x2260; Last = 0x2261; Width = EastAsianWidth.OfText "A"  } // 2260..2261;A     # Sm     [2] NOT EQUAL TO..IDENTICAL TO
            { Start = 0x2262; Last = 0x2263; Width = EastAsianWidth.OfText "N"  } // 2262..2263;N     # Sm     [2] NOT IDENTICAL TO..STRICTLY EQUIVALENT TO
            { Start = 0x2264; Last = 0x2267; Width = EastAsianWidth.OfText "A"  } // 2264..2267;A     # Sm     [4] LESS-THAN OR EQUAL TO..GREATER-THAN OVER EQUAL TO
            { Start = 0x2268; Last = 0x2269; Width = EastAsianWidth.OfText "N"  } // 2268..2269;N     # Sm     [2] LESS-THAN BUT NOT EQUAL TO..GREATER-THAN BUT NOT EQUAL TO
            { Start = 0x226A; Last = 0x226B; Width = EastAsianWidth.OfText "A"  } // 226A..226B;A     # Sm     [2] MUCH LESS-THAN..MUCH GREATER-THAN
            { Start = 0x226C; Last = 0x226D; Width = EastAsianWidth.OfText "N"  } // 226C..226D;N     # Sm     [2] BETWEEN..NOT EQUIVALENT TO
            { Start = 0x226E; Last = 0x226F; Width = EastAsianWidth.OfText "A"  } // 226E..226F;A     # Sm     [2] NOT LESS-THAN..NOT GREATER-THAN
            { Start = 0x2270; Last = 0x2281; Width = EastAsianWidth.OfText "N"  } // 2270..2281;N     # Sm    [18] NEITHER LESS-THAN NOR EQUAL TO..DOES NOT SUCCEED
            { Start = 0x2282; Last = 0x2283; Width = EastAsianWidth.OfText "A"  } // 2282..2283;A     # Sm     [2] SUBSET OF..SUPERSET OF
            { Start = 0x2284; Last = 0x2285; Width = EastAsianWidth.OfText "N"  } // 2284..2285;N     # Sm     [2] NOT A SUBSET OF..NOT A SUPERSET OF
            { Start = 0x2286; Last = 0x2287; Width = EastAsianWidth.OfText "A"  } // 2286..2287;A     # Sm     [2] SUBSET OF OR EQUAL TO..SUPERSET OF OR EQUAL TO
            { Start = 0x2288; Last = 0x2294; Width = EastAsianWidth.OfText "N"  } // 2288..2294;N     # Sm    [13] NEITHER A SUBSET OF NOR EQUAL TO..SQUARE CUP
            { Start = 0x2295; Last = 0x2295; Width = EastAsianWidth.OfText "A"  } // 2295;A           # Sm         CIRCLED PLUS
            { Start = 0x2296; Last = 0x2298; Width = EastAsianWidth.OfText "N"  } // 2296..2298;N     # Sm     [3] CIRCLED MINUS..CIRCLED DIVISION SLASH
            { Start = 0x2299; Last = 0x2299; Width = EastAsianWidth.OfText "A"  } // 2299;A           # Sm         CIRCLED DOT OPERATOR
            { Start = 0x229A; Last = 0x22A4; Width = EastAsianWidth.OfText "N"  } // 229A..22A4;N     # Sm    [11] CIRCLED RING OPERATOR..DOWN TACK
            { Start = 0x22A5; Last = 0x22A5; Width = EastAsianWidth.OfText "A"  } // 22A5;A           # Sm         UP TACK
            { Start = 0x22A6; Last = 0x22BE; Width = EastAsianWidth.OfText "N"  } // 22A6..22BE;N     # Sm    [25] ASSERTION..RIGHT ANGLE WITH ARC
            { Start = 0x22BF; Last = 0x22BF; Width = EastAsianWidth.OfText "A"  } // 22BF;A           # Sm         RIGHT TRIANGLE
            { Start = 0x22C0; Last = 0x22FF; Width = EastAsianWidth.OfText "N"  } // 22C0..22FF;N     # Sm    [64] N-ARY LOGICAL AND..Z NOTATION BAG MEMBERSHIP
            { Start = 0x2300; Last = 0x2307; Width = EastAsianWidth.OfText "N"  } // 2300..2307;N     # So     [8] DIAMETER SIGN..WAVY LINE
            { Start = 0x2308; Last = 0x2308; Width = EastAsianWidth.OfText "N"  } // 2308;N           # Ps         LEFT CEILING
            { Start = 0x2309; Last = 0x2309; Width = EastAsianWidth.OfText "N"  } // 2309;N           # Pe         RIGHT CEILING
            { Start = 0x230A; Last = 0x230A; Width = EastAsianWidth.OfText "N"  } // 230A;N           # Ps         LEFT FLOOR
            { Start = 0x230B; Last = 0x230B; Width = EastAsianWidth.OfText "N"  } // 230B;N           # Pe         RIGHT FLOOR
            { Start = 0x230C; Last = 0x2311; Width = EastAsianWidth.OfText "N"  } // 230C..2311;N     # So     [6] BOTTOM RIGHT CROP..SQUARE LOZENGE
            { Start = 0x2312; Last = 0x2312; Width = EastAsianWidth.OfText "A"  } // 2312;A           # So         ARC
            { Start = 0x2313; Last = 0x2319; Width = EastAsianWidth.OfText "N"  } // 2313..2319;N     # So     [7] SEGMENT..TURNED NOT SIGN
            { Start = 0x231A; Last = 0x231B; Width = EastAsianWidth.OfText "W"  } // 231A..231B;W     # So     [2] WATCH..HOURGLASS
            { Start = 0x231C; Last = 0x231F; Width = EastAsianWidth.OfText "N"  } // 231C..231F;N     # So     [4] TOP LEFT CORNER..BOTTOM RIGHT CORNER
            { Start = 0x2320; Last = 0x2321; Width = EastAsianWidth.OfText "N"  } // 2320..2321;N     # Sm     [2] TOP HALF INTEGRAL..BOTTOM HALF INTEGRAL
            { Start = 0x2322; Last = 0x2328; Width = EastAsianWidth.OfText "N"  } // 2322..2328;N     # So     [7] FROWN..KEYBOARD
            { Start = 0x2329; Last = 0x2329; Width = EastAsianWidth.OfText "W"  } // 2329;W           # Ps         LEFT-POINTING ANGLE BRACKET
            { Start = 0x232A; Last = 0x232A; Width = EastAsianWidth.OfText "W"  } // 232A;W           # Pe         RIGHT-POINTING ANGLE BRACKET
            { Start = 0x232B; Last = 0x237B; Width = EastAsianWidth.OfText "N"  } // 232B..237B;N     # So    [81] ERASE TO THE LEFT..NOT CHECK MARK
            { Start = 0x237C; Last = 0x237C; Width = EastAsianWidth.OfText "N"  } // 237C;N           # Sm         RIGHT ANGLE WITH DOWNWARDS ZIGZAG ARROW
            { Start = 0x237D; Last = 0x239A; Width = EastAsianWidth.OfText "N"  } // 237D..239A;N     # So    [30] SHOULDERED OPEN BOX..CLEAR SCREEN SYMBOL
            { Start = 0x239B; Last = 0x23B3; Width = EastAsianWidth.OfText "N"  } // 239B..23B3;N     # Sm    [25] LEFT PARENTHESIS UPPER HOOK..SUMMATION BOTTOM
            { Start = 0x23B4; Last = 0x23DB; Width = EastAsianWidth.OfText "N"  } // 23B4..23DB;N     # So    [40] TOP SQUARE BRACKET..FUSE
            { Start = 0x23DC; Last = 0x23E1; Width = EastAsianWidth.OfText "N"  } // 23DC..23E1;N     # Sm     [6] TOP PARENTHESIS..BOTTOM TORTOISE SHELL BRACKET
            { Start = 0x23E2; Last = 0x23E8; Width = EastAsianWidth.OfText "N"  } // 23E2..23E8;N     # So     [7] WHITE TRAPEZIUM..DECIMAL EXPONENT SYMBOL
            { Start = 0x23E9; Last = 0x23EC; Width = EastAsianWidth.OfText "W"  } // 23E9..23EC;W     # So     [4] BLACK RIGHT-POINTING DOUBLE TRIANGLE..BLACK DOWN-POINTING DOUBLE TRIANGLE
            { Start = 0x23ED; Last = 0x23EF; Width = EastAsianWidth.OfText "N"  } // 23ED..23EF;N     # So     [3] BLACK RIGHT-POINTING DOUBLE TRIANGLE WITH VERTICAL BAR..BLACK RIGHT-POINTING TRIANGLE WITH DOUBLE VERTICAL BAR
            { Start = 0x23F0; Last = 0x23F0; Width = EastAsianWidth.OfText "W"  } // 23F0;W           # So         ALARM CLOCK
            { Start = 0x23F1; Last = 0x23F2; Width = EastAsianWidth.OfText "N"  } // 23F1..23F2;N     # So     [2] STOPWATCH..TIMER CLOCK
            { Start = 0x23F3; Last = 0x23F3; Width = EastAsianWidth.OfText "W"  } // 23F3;W           # So         HOURGLASS WITH FLOWING SAND
            { Start = 0x23F4; Last = 0x23FF; Width = EastAsianWidth.OfText "N"  } // 23F4..23FF;N     # So    [12] BLACK MEDIUM LEFT-POINTING TRIANGLE..OBSERVER EYE SYMBOL
            { Start = 0x2400; Last = 0x2426; Width = EastAsianWidth.OfText "N"  } // 2400..2426;N     # So    [39] SYMBOL FOR NULL..SYMBOL FOR SUBSTITUTE FORM TWO
            { Start = 0x2440; Last = 0x244A; Width = EastAsianWidth.OfText "N"  } // 2440..244A;N     # So    [11] OCR HOOK..OCR DOUBLE BACKSLASH
            { Start = 0x2460; Last = 0x249B; Width = EastAsianWidth.OfText "A"  } // 2460..249B;A     # No    [60] CIRCLED DIGIT ONE..NUMBER TWENTY FULL STOP
            { Start = 0x249C; Last = 0x24E9; Width = EastAsianWidth.OfText "A"  } // 249C..24E9;A     # So    [78] PARENTHESIZED LATIN SMALL LETTER A..CIRCLED LATIN SMALL LETTER Z
            { Start = 0x24EA; Last = 0x24EA; Width = EastAsianWidth.OfText "N"  } // 24EA;N           # No         CIRCLED DIGIT ZERO
            { Start = 0x24EB; Last = 0x24FF; Width = EastAsianWidth.OfText "A"  } // 24EB..24FF;A     # No    [21] NEGATIVE CIRCLED NUMBER ELEVEN..NEGATIVE CIRCLED DIGIT ZERO
            { Start = 0x2500; Last = 0x254B; Width = EastAsianWidth.OfText "A"  } // 2500..254B;A     # So    [76] BOX DRAWINGS LIGHT HORIZONTAL..BOX DRAWINGS HEAVY VERTICAL AND HORIZONTAL
            { Start = 0x254C; Last = 0x254F; Width = EastAsianWidth.OfText "N"  } // 254C..254F;N     # So     [4] BOX DRAWINGS LIGHT DOUBLE DASH HORIZONTAL..BOX DRAWINGS HEAVY DOUBLE DASH VERTICAL
            { Start = 0x2550; Last = 0x2573; Width = EastAsianWidth.OfText "A"  } // 2550..2573;A     # So    [36] BOX DRAWINGS DOUBLE HORIZONTAL..BOX DRAWINGS LIGHT DIAGONAL CROSS
            { Start = 0x2574; Last = 0x257F; Width = EastAsianWidth.OfText "N"  } // 2574..257F;N     # So    [12] BOX DRAWINGS LIGHT LEFT..BOX DRAWINGS HEAVY UP AND LIGHT DOWN
            { Start = 0x2580; Last = 0x258F; Width = EastAsianWidth.OfText "A"  } // 2580..258F;A     # So    [16] UPPER HALF BLOCK..LEFT ONE EIGHTH BLOCK
            { Start = 0x2590; Last = 0x2591; Width = EastAsianWidth.OfText "N"  } // 2590..2591;N     # So     [2] RIGHT HALF BLOCK..LIGHT SHADE
            { Start = 0x2592; Last = 0x2595; Width = EastAsianWidth.OfText "A"  } // 2592..2595;A     # So     [4] MEDIUM SHADE..RIGHT ONE EIGHTH BLOCK
            { Start = 0x2596; Last = 0x259F; Width = EastAsianWidth.OfText "N"  } // 2596..259F;N     # So    [10] QUADRANT LOWER LEFT..QUADRANT UPPER RIGHT AND LOWER LEFT AND LOWER RIGHT
            { Start = 0x25A0; Last = 0x25A1; Width = EastAsianWidth.OfText "A"  } // 25A0..25A1;A     # So     [2] BLACK SQUARE..WHITE SQUARE
            { Start = 0x25A2; Last = 0x25A2; Width = EastAsianWidth.OfText "N"  } // 25A2;N           # So         WHITE SQUARE WITH ROUNDED CORNERS
            { Start = 0x25A3; Last = 0x25A9; Width = EastAsianWidth.OfText "A"  } // 25A3..25A9;A     # So     [7] WHITE SQUARE CONTAINING BLACK SMALL SQUARE..SQUARE WITH DIAGONAL CROSSHATCH FILL
            { Start = 0x25AA; Last = 0x25B1; Width = EastAsianWidth.OfText "N"  } // 25AA..25B1;N     # So     [8] BLACK SMALL SQUARE..WHITE PARALLELOGRAM
            { Start = 0x25B2; Last = 0x25B3; Width = EastAsianWidth.OfText "A"  } // 25B2..25B3;A     # So     [2] BLACK UP-POINTING TRIANGLE..WHITE UP-POINTING TRIANGLE
            { Start = 0x25B4; Last = 0x25B5; Width = EastAsianWidth.OfText "N"  } // 25B4..25B5;N     # So     [2] BLACK UP-POINTING SMALL TRIANGLE..WHITE UP-POINTING SMALL TRIANGLE
            { Start = 0x25B6; Last = 0x25B6; Width = EastAsianWidth.OfText "A"  } // 25B6;A           # So         BLACK RIGHT-POINTING TRIANGLE
            { Start = 0x25B7; Last = 0x25B7; Width = EastAsianWidth.OfText "A"  } // 25B7;A           # Sm         WHITE RIGHT-POINTING TRIANGLE
            { Start = 0x25B8; Last = 0x25BB; Width = EastAsianWidth.OfText "N"  } // 25B8..25BB;N     # So     [4] BLACK RIGHT-POINTING SMALL TRIANGLE..WHITE RIGHT-POINTING POINTER
            { Start = 0x25BC; Last = 0x25BD; Width = EastAsianWidth.OfText "A"  } // 25BC..25BD;A     # So     [2] BLACK DOWN-POINTING TRIANGLE..WHITE DOWN-POINTING TRIANGLE
            { Start = 0x25BE; Last = 0x25BF; Width = EastAsianWidth.OfText "N"  } // 25BE..25BF;N     # So     [2] BLACK DOWN-POINTING SMALL TRIANGLE..WHITE DOWN-POINTING SMALL TRIANGLE
            { Start = 0x25C0; Last = 0x25C0; Width = EastAsianWidth.OfText "A"  } // 25C0;A           # So         BLACK LEFT-POINTING TRIANGLE
            { Start = 0x25C1; Last = 0x25C1; Width = EastAsianWidth.OfText "A"  } // 25C1;A           # Sm         WHITE LEFT-POINTING TRIANGLE
            { Start = 0x25C2; Last = 0x25C5; Width = EastAsianWidth.OfText "N"  } // 25C2..25C5;N     # So     [4] BLACK LEFT-POINTING SMALL TRIANGLE..WHITE LEFT-POINTING POINTER
            { Start = 0x25C6; Last = 0x25C8; Width = EastAsianWidth.OfText "A"  } // 25C6..25C8;A     # So     [3] BLACK DIAMOND..WHITE DIAMOND CONTAINING BLACK SMALL DIAMOND
            { Start = 0x25C9; Last = 0x25CA; Width = EastAsianWidth.OfText "N"  } // 25C9..25CA;N     # So     [2] FISHEYE..LOZENGE
            { Start = 0x25CB; Last = 0x25CB; Width = EastAsianWidth.OfText "A"  } // 25CB;A           # So         WHITE CIRCLE
            { Start = 0x25CC; Last = 0x25CD; Width = EastAsianWidth.OfText "N"  } // 25CC..25CD;N     # So     [2] DOTTED CIRCLE..CIRCLE WITH VERTICAL FILL
            { Start = 0x25CE; Last = 0x25D1; Width = EastAsianWidth.OfText "A"  } // 25CE..25D1;A     # So     [4] BULLSEYE..CIRCLE WITH RIGHT HALF BLACK
            { Start = 0x25D2; Last = 0x25E1; Width = EastAsianWidth.OfText "N"  } // 25D2..25E1;N     # So    [16] CIRCLE WITH LOWER HALF BLACK..LOWER HALF CIRCLE
            { Start = 0x25E2; Last = 0x25E5; Width = EastAsianWidth.OfText "A"  } // 25E2..25E5;A     # So     [4] BLACK LOWER RIGHT TRIANGLE..BLACK UPPER RIGHT TRIANGLE
            { Start = 0x25E6; Last = 0x25EE; Width = EastAsianWidth.OfText "N"  } // 25E6..25EE;N     # So     [9] WHITE BULLET..UP-POINTING TRIANGLE WITH RIGHT HALF BLACK
            { Start = 0x25EF; Last = 0x25EF; Width = EastAsianWidth.OfText "A"  } // 25EF;A           # So         LARGE CIRCLE
            { Start = 0x25F0; Last = 0x25F7; Width = EastAsianWidth.OfText "N"  } // 25F0..25F7;N     # So     [8] WHITE SQUARE WITH UPPER LEFT QUADRANT..WHITE CIRCLE WITH UPPER RIGHT QUADRANT
            { Start = 0x25F8; Last = 0x25FC; Width = EastAsianWidth.OfText "N"  } // 25F8..25FC;N     # Sm     [5] UPPER LEFT TRIANGLE..BLACK MEDIUM SQUARE
            { Start = 0x25FD; Last = 0x25FE; Width = EastAsianWidth.OfText "W"  } // 25FD..25FE;W     # Sm     [2] WHITE MEDIUM SMALL SQUARE..BLACK MEDIUM SMALL SQUARE
            { Start = 0x25FF; Last = 0x25FF; Width = EastAsianWidth.OfText "N"  } // 25FF;N           # Sm         LOWER RIGHT TRIANGLE
            { Start = 0x2600; Last = 0x2604; Width = EastAsianWidth.OfText "N"  } // 2600..2604;N     # So     [5] BLACK SUN WITH RAYS..COMET
            { Start = 0x2605; Last = 0x2606; Width = EastAsianWidth.OfText "A"  } // 2605..2606;A     # So     [2] BLACK STAR..WHITE STAR
            { Start = 0x2607; Last = 0x2608; Width = EastAsianWidth.OfText "N"  } // 2607..2608;N     # So     [2] LIGHTNING..THUNDERSTORM
            { Start = 0x2609; Last = 0x2609; Width = EastAsianWidth.OfText "A"  } // 2609;A           # So         SUN
            { Start = 0x260A; Last = 0x260D; Width = EastAsianWidth.OfText "N"  } // 260A..260D;N     # So     [4] ASCENDING NODE..OPPOSITION
            { Start = 0x260E; Last = 0x260F; Width = EastAsianWidth.OfText "A"  } // 260E..260F;A     # So     [2] BLACK TELEPHONE..WHITE TELEPHONE
            { Start = 0x2610; Last = 0x2613; Width = EastAsianWidth.OfText "N"  } // 2610..2613;N     # So     [4] BALLOT BOX..SALTIRE
            { Start = 0x2614; Last = 0x2615; Width = EastAsianWidth.OfText "W"  } // 2614..2615;W     # So     [2] UMBRELLA WITH RAIN DROPS..HOT BEVERAGE
            { Start = 0x2616; Last = 0x261B; Width = EastAsianWidth.OfText "N"  } // 2616..261B;N     # So     [6] WHITE SHOGI PIECE..BLACK RIGHT POINTING INDEX
            { Start = 0x261C; Last = 0x261C; Width = EastAsianWidth.OfText "A"  } // 261C;A           # So         WHITE LEFT POINTING INDEX
            { Start = 0x261D; Last = 0x261D; Width = EastAsianWidth.OfText "N"  } // 261D;N           # So         WHITE UP POINTING INDEX
            { Start = 0x261E; Last = 0x261E; Width = EastAsianWidth.OfText "A"  } // 261E;A           # So         WHITE RIGHT POINTING INDEX
            { Start = 0x261F; Last = 0x263F; Width = EastAsianWidth.OfText "N"  } // 261F..263F;N     # So    [33] WHITE DOWN POINTING INDEX..MERCURY
            { Start = 0x2640; Last = 0x2640; Width = EastAsianWidth.OfText "A"  } // 2640;A           # So         FEMALE SIGN
            { Start = 0x2641; Last = 0x2641; Width = EastAsianWidth.OfText "N"  } // 2641;N           # So         EARTH
            { Start = 0x2642; Last = 0x2642; Width = EastAsianWidth.OfText "A"  } // 2642;A           # So         MALE SIGN
            { Start = 0x2643; Last = 0x2647; Width = EastAsianWidth.OfText "N"  } // 2643..2647;N     # So     [5] JUPITER..PLUTO
            { Start = 0x2648; Last = 0x2653; Width = EastAsianWidth.OfText "W"  } // 2648..2653;W     # So    [12] ARIES..PISCES
            { Start = 0x2654; Last = 0x265F; Width = EastAsianWidth.OfText "N"  } // 2654..265F;N     # So    [12] WHITE CHESS KING..BLACK CHESS PAWN
            { Start = 0x2660; Last = 0x2661; Width = EastAsianWidth.OfText "A"  } // 2660..2661;A     # So     [2] BLACK SPADE SUIT..WHITE HEART SUIT
            { Start = 0x2662; Last = 0x2662; Width = EastAsianWidth.OfText "N"  } // 2662;N           # So         WHITE DIAMOND SUIT
            { Start = 0x2663; Last = 0x2665; Width = EastAsianWidth.OfText "A"  } // 2663..2665;A     # So     [3] BLACK CLUB SUIT..BLACK HEART SUIT
            { Start = 0x2666; Last = 0x2666; Width = EastAsianWidth.OfText "N"  } // 2666;N           # So         BLACK DIAMOND SUIT
            { Start = 0x2667; Last = 0x266A; Width = EastAsianWidth.OfText "A"  } // 2667..266A;A     # So     [4] WHITE CLUB SUIT..EIGHTH NOTE
            { Start = 0x266B; Last = 0x266B; Width = EastAsianWidth.OfText "N"  } // 266B;N           # So         BEAMED EIGHTH NOTES
            { Start = 0x266C; Last = 0x266D; Width = EastAsianWidth.OfText "A"  } // 266C..266D;A     # So     [2] BEAMED SIXTEENTH NOTES..MUSIC FLAT SIGN
            { Start = 0x266E; Last = 0x266E; Width = EastAsianWidth.OfText "N"  } // 266E;N           # So         MUSIC NATURAL SIGN
            { Start = 0x266F; Last = 0x266F; Width = EastAsianWidth.OfText "A"  } // 266F;A           # Sm         MUSIC SHARP SIGN
            { Start = 0x2670; Last = 0x267E; Width = EastAsianWidth.OfText "N"  } // 2670..267E;N     # So    [15] WEST SYRIAC CROSS..PERMANENT PAPER SIGN
            { Start = 0x267F; Last = 0x267F; Width = EastAsianWidth.OfText "W"  } // 267F;W           # So         WHEELCHAIR SYMBOL
            { Start = 0x2680; Last = 0x2692; Width = EastAsianWidth.OfText "N"  } // 2680..2692;N     # So    [19] DIE FACE-1..HAMMER AND PICK
            { Start = 0x2693; Last = 0x2693; Width = EastAsianWidth.OfText "W"  } // 2693;W           # So         ANCHOR
            { Start = 0x2694; Last = 0x269D; Width = EastAsianWidth.OfText "N"  } // 2694..269D;N     # So    [10] CROSSED SWORDS..OUTLINED WHITE STAR
            { Start = 0x269E; Last = 0x269F; Width = EastAsianWidth.OfText "A"  } // 269E..269F;A     # So     [2] THREE LINES CONVERGING RIGHT..THREE LINES CONVERGING LEFT
            { Start = 0x26A0; Last = 0x26A0; Width = EastAsianWidth.OfText "N"  } // 26A0;N           # So         WARNING SIGN
            { Start = 0x26A1; Last = 0x26A1; Width = EastAsianWidth.OfText "W"  } // 26A1;W           # So         HIGH VOLTAGE SIGN
            { Start = 0x26A2; Last = 0x26A9; Width = EastAsianWidth.OfText "N"  } // 26A2..26A9;N     # So     [8] DOUBLED FEMALE SIGN..HORIZONTAL MALE WITH STROKE SIGN
            { Start = 0x26AA; Last = 0x26AB; Width = EastAsianWidth.OfText "W"  } // 26AA..26AB;W     # So     [2] MEDIUM WHITE CIRCLE..MEDIUM BLACK CIRCLE
            { Start = 0x26AC; Last = 0x26BC; Width = EastAsianWidth.OfText "N"  } // 26AC..26BC;N     # So    [17] MEDIUM SMALL WHITE CIRCLE..SESQUIQUADRATE
            { Start = 0x26BD; Last = 0x26BE; Width = EastAsianWidth.OfText "W"  } // 26BD..26BE;W     # So     [2] SOCCER BALL..BASEBALL
            { Start = 0x26BF; Last = 0x26BF; Width = EastAsianWidth.OfText "A"  } // 26BF;A           # So         SQUARED KEY
            { Start = 0x26C0; Last = 0x26C3; Width = EastAsianWidth.OfText "N"  } // 26C0..26C3;N     # So     [4] WHITE DRAUGHTS MAN..BLACK DRAUGHTS KING
            { Start = 0x26C4; Last = 0x26C5; Width = EastAsianWidth.OfText "W"  } // 26C4..26C5;W     # So     [2] SNOWMAN WITHOUT SNOW..SUN BEHIND CLOUD
            { Start = 0x26C6; Last = 0x26CD; Width = EastAsianWidth.OfText "A"  } // 26C6..26CD;A     # So     [8] RAIN..DISABLED CAR
            { Start = 0x26CE; Last = 0x26CE; Width = EastAsianWidth.OfText "W"  } // 26CE;W           # So         OPHIUCHUS
            { Start = 0x26CF; Last = 0x26D3; Width = EastAsianWidth.OfText "A"  } // 26CF..26D3;A     # So     [5] PICK..CHAINS
            { Start = 0x26D4; Last = 0x26D4; Width = EastAsianWidth.OfText "W"  } // 26D4;W           # So         NO ENTRY
            { Start = 0x26D5; Last = 0x26E1; Width = EastAsianWidth.OfText "A"  } // 26D5..26E1;A     # So    [13] ALTERNATE ONE-WAY LEFT WAY TRAFFIC..RESTRICTED LEFT ENTRY-2
            { Start = 0x26E2; Last = 0x26E2; Width = EastAsianWidth.OfText "N"  } // 26E2;N           # So         ASTRONOMICAL SYMBOL FOR URANUS
            { Start = 0x26E3; Last = 0x26E3; Width = EastAsianWidth.OfText "A"  } // 26E3;A           # So         HEAVY CIRCLE WITH STROKE AND TWO DOTS ABOVE
            { Start = 0x26E4; Last = 0x26E7; Width = EastAsianWidth.OfText "N"  } // 26E4..26E7;N     # So     [4] PENTAGRAM..INVERTED PENTAGRAM
            { Start = 0x26E8; Last = 0x26E9; Width = EastAsianWidth.OfText "A"  } // 26E8..26E9;A     # So     [2] BLACK CROSS ON SHIELD..SHINTO SHRINE
            { Start = 0x26EA; Last = 0x26EA; Width = EastAsianWidth.OfText "W"  } // 26EA;W           # So         CHURCH
            { Start = 0x26EB; Last = 0x26F1; Width = EastAsianWidth.OfText "A"  } // 26EB..26F1;A     # So     [7] CASTLE..UMBRELLA ON GROUND
            { Start = 0x26F2; Last = 0x26F3; Width = EastAsianWidth.OfText "W"  } // 26F2..26F3;W     # So     [2] FOUNTAIN..FLAG IN HOLE
            { Start = 0x26F4; Last = 0x26F4; Width = EastAsianWidth.OfText "A"  } // 26F4;A           # So         FERRY
            { Start = 0x26F5; Last = 0x26F5; Width = EastAsianWidth.OfText "W"  } // 26F5;W           # So         SAILBOAT
            { Start = 0x26F6; Last = 0x26F9; Width = EastAsianWidth.OfText "A"  } // 26F6..26F9;A     # So     [4] SQUARE FOUR CORNERS..PERSON WITH BALL
            { Start = 0x26FA; Last = 0x26FA; Width = EastAsianWidth.OfText "W"  } // 26FA;W           # So         TENT
            { Start = 0x26FB; Last = 0x26FC; Width = EastAsianWidth.OfText "A"  } // 26FB..26FC;A     # So     [2] JAPANESE BANK SYMBOL..HEADSTONE GRAVEYARD SYMBOL
            { Start = 0x26FD; Last = 0x26FD; Width = EastAsianWidth.OfText "W"  } // 26FD;W           # So         FUEL PUMP
            { Start = 0x26FE; Last = 0x26FF; Width = EastAsianWidth.OfText "A"  } // 26FE..26FF;A     # So     [2] CUP ON BLACK SQUARE..WHITE FLAG WITH HORIZONTAL MIDDLE BLACK STRIPE
            { Start = 0x2700; Last = 0x2704; Width = EastAsianWidth.OfText "N"  } // 2700..2704;N     # So     [5] BLACK SAFETY SCISSORS..WHITE SCISSORS
            { Start = 0x2705; Last = 0x2705; Width = EastAsianWidth.OfText "W"  } // 2705;W           # So         WHITE HEAVY CHECK MARK
            { Start = 0x2706; Last = 0x2709; Width = EastAsianWidth.OfText "N"  } // 2706..2709;N     # So     [4] TELEPHONE LOCATION SIGN..ENVELOPE
            { Start = 0x270A; Last = 0x270B; Width = EastAsianWidth.OfText "W"  } // 270A..270B;W     # So     [2] RAISED FIST..RAISED HAND
            { Start = 0x270C; Last = 0x2727; Width = EastAsianWidth.OfText "N"  } // 270C..2727;N     # So    [28] VICTORY HAND..WHITE FOUR POINTED STAR
            { Start = 0x2728; Last = 0x2728; Width = EastAsianWidth.OfText "W"  } // 2728;W           # So         SPARKLES
            { Start = 0x2729; Last = 0x273C; Width = EastAsianWidth.OfText "N"  } // 2729..273C;N     # So    [20] STRESS OUTLINED WHITE STAR..OPEN CENTRE TEARDROP-SPOKED ASTERISK
            { Start = 0x273D; Last = 0x273D; Width = EastAsianWidth.OfText "A"  } // 273D;A           # So         HEAVY TEARDROP-SPOKED ASTERISK
            { Start = 0x273E; Last = 0x274B; Width = EastAsianWidth.OfText "N"  } // 273E..274B;N     # So    [14] SIX PETALLED BLACK AND WHITE FLORETTE..HEAVY EIGHT TEARDROP-SPOKED PROPELLER ASTERISK
            { Start = 0x274C; Last = 0x274C; Width = EastAsianWidth.OfText "W"  } // 274C;W           # So         CROSS MARK
            { Start = 0x274D; Last = 0x274D; Width = EastAsianWidth.OfText "N"  } // 274D;N           # So         SHADOWED WHITE CIRCLE
            { Start = 0x274E; Last = 0x274E; Width = EastAsianWidth.OfText "W"  } // 274E;W           # So         NEGATIVE SQUARED CROSS MARK
            { Start = 0x274F; Last = 0x2752; Width = EastAsianWidth.OfText "N"  } // 274F..2752;N     # So     [4] LOWER RIGHT DROP-SHADOWED WHITE SQUARE..UPPER RIGHT SHADOWED WHITE SQUARE
            { Start = 0x2753; Last = 0x2755; Width = EastAsianWidth.OfText "W"  } // 2753..2755;W     # So     [3] BLACK QUESTION MARK ORNAMENT..WHITE EXCLAMATION MARK ORNAMENT
            { Start = 0x2756; Last = 0x2756; Width = EastAsianWidth.OfText "N"  } // 2756;N           # So         BLACK DIAMOND MINUS WHITE X
            { Start = 0x2757; Last = 0x2757; Width = EastAsianWidth.OfText "W"  } // 2757;W           # So         HEAVY EXCLAMATION MARK SYMBOL
            { Start = 0x2758; Last = 0x2767; Width = EastAsianWidth.OfText "N"  } // 2758..2767;N     # So    [16] LIGHT VERTICAL BAR..ROTATED FLORAL HEART BULLET
            { Start = 0x2768; Last = 0x2768; Width = EastAsianWidth.OfText "N"  } // 2768;N           # Ps         MEDIUM LEFT PARENTHESIS ORNAMENT
            { Start = 0x2769; Last = 0x2769; Width = EastAsianWidth.OfText "N"  } // 2769;N           # Pe         MEDIUM RIGHT PARENTHESIS ORNAMENT
            { Start = 0x276A; Last = 0x276A; Width = EastAsianWidth.OfText "N"  } // 276A;N           # Ps         MEDIUM FLATTENED LEFT PARENTHESIS ORNAMENT
            { Start = 0x276B; Last = 0x276B; Width = EastAsianWidth.OfText "N"  } // 276B;N           # Pe         MEDIUM FLATTENED RIGHT PARENTHESIS ORNAMENT
            { Start = 0x276C; Last = 0x276C; Width = EastAsianWidth.OfText "N"  } // 276C;N           # Ps         MEDIUM LEFT-POINTING ANGLE BRACKET ORNAMENT
            { Start = 0x276D; Last = 0x276D; Width = EastAsianWidth.OfText "N"  } // 276D;N           # Pe         MEDIUM RIGHT-POINTING ANGLE BRACKET ORNAMENT
            { Start = 0x276E; Last = 0x276E; Width = EastAsianWidth.OfText "N"  } // 276E;N           # Ps         HEAVY LEFT-POINTING ANGLE QUOTATION MARK ORNAMENT
            { Start = 0x276F; Last = 0x276F; Width = EastAsianWidth.OfText "N"  } // 276F;N           # Pe         HEAVY RIGHT-POINTING ANGLE QUOTATION MARK ORNAMENT
            { Start = 0x2770; Last = 0x2770; Width = EastAsianWidth.OfText "N"  } // 2770;N           # Ps         HEAVY LEFT-POINTING ANGLE BRACKET ORNAMENT
            { Start = 0x2771; Last = 0x2771; Width = EastAsianWidth.OfText "N"  } // 2771;N           # Pe         HEAVY RIGHT-POINTING ANGLE BRACKET ORNAMENT
            { Start = 0x2772; Last = 0x2772; Width = EastAsianWidth.OfText "N"  } // 2772;N           # Ps         LIGHT LEFT TORTOISE SHELL BRACKET ORNAMENT
            { Start = 0x2773; Last = 0x2773; Width = EastAsianWidth.OfText "N"  } // 2773;N           # Pe         LIGHT RIGHT TORTOISE SHELL BRACKET ORNAMENT
            { Start = 0x2774; Last = 0x2774; Width = EastAsianWidth.OfText "N"  } // 2774;N           # Ps         MEDIUM LEFT CURLY BRACKET ORNAMENT
            { Start = 0x2775; Last = 0x2775; Width = EastAsianWidth.OfText "N"  } // 2775;N           # Pe         MEDIUM RIGHT CURLY BRACKET ORNAMENT
            { Start = 0x2776; Last = 0x277F; Width = EastAsianWidth.OfText "A"  } // 2776..277F;A     # No    [10] DINGBAT NEGATIVE CIRCLED DIGIT ONE..DINGBAT NEGATIVE CIRCLED NUMBER TEN
            { Start = 0x2780; Last = 0x2793; Width = EastAsianWidth.OfText "N"  } // 2780..2793;N     # No    [20] DINGBAT CIRCLED SANS-SERIF DIGIT ONE..DINGBAT NEGATIVE CIRCLED SANS-SERIF NUMBER TEN
            { Start = 0x2794; Last = 0x2794; Width = EastAsianWidth.OfText "N"  } // 2794;N           # So         HEAVY WIDE-HEADED RIGHTWARDS ARROW
            { Start = 0x2795; Last = 0x2797; Width = EastAsianWidth.OfText "W"  } // 2795..2797;W     # So     [3] HEAVY PLUS SIGN..HEAVY DIVISION SIGN
            { Start = 0x2798; Last = 0x27AF; Width = EastAsianWidth.OfText "N"  } // 2798..27AF;N     # So    [24] HEAVY SOUTH EAST ARROW..NOTCHED LOWER RIGHT-SHADOWED WHITE RIGHTWARDS ARROW
            { Start = 0x27B0; Last = 0x27B0; Width = EastAsianWidth.OfText "W"  } // 27B0;W           # So         CURLY LOOP
            { Start = 0x27B1; Last = 0x27BE; Width = EastAsianWidth.OfText "N"  } // 27B1..27BE;N     # So    [14] NOTCHED UPPER RIGHT-SHADOWED WHITE RIGHTWARDS ARROW..OPEN-OUTLINED RIGHTWARDS ARROW
            { Start = 0x27BF; Last = 0x27BF; Width = EastAsianWidth.OfText "W"  } // 27BF;W           # So         DOUBLE CURLY LOOP
            { Start = 0x27C0; Last = 0x27C4; Width = EastAsianWidth.OfText "N"  } // 27C0..27C4;N     # Sm     [5] THREE DIMENSIONAL ANGLE..OPEN SUPERSET
            { Start = 0x27C5; Last = 0x27C5; Width = EastAsianWidth.OfText "N"  } // 27C5;N           # Ps         LEFT S-SHAPED BAG DELIMITER
            { Start = 0x27C6; Last = 0x27C6; Width = EastAsianWidth.OfText "N"  } // 27C6;N           # Pe         RIGHT S-SHAPED BAG DELIMITER
            { Start = 0x27C7; Last = 0x27E5; Width = EastAsianWidth.OfText "N"  } // 27C7..27E5;N     # Sm    [31] OR WITH DOT INSIDE..WHITE SQUARE WITH RIGHTWARDS TICK
            { Start = 0x27E6; Last = 0x27E6; Width = EastAsianWidth.OfText "Na" } // 27E6;Na          # Ps         MATHEMATICAL LEFT WHITE SQUARE BRACKET
            { Start = 0x27E7; Last = 0x27E7; Width = EastAsianWidth.OfText "Na" } // 27E7;Na          # Pe         MATHEMATICAL RIGHT WHITE SQUARE BRACKET
            { Start = 0x27E8; Last = 0x27E8; Width = EastAsianWidth.OfText "Na" } // 27E8;Na          # Ps         MATHEMATICAL LEFT ANGLE BRACKET
            { Start = 0x27E9; Last = 0x27E9; Width = EastAsianWidth.OfText "Na" } // 27E9;Na          # Pe         MATHEMATICAL RIGHT ANGLE BRACKET
            { Start = 0x27EA; Last = 0x27EA; Width = EastAsianWidth.OfText "Na" } // 27EA;Na          # Ps         MATHEMATICAL LEFT DOUBLE ANGLE BRACKET
            { Start = 0x27EB; Last = 0x27EB; Width = EastAsianWidth.OfText "Na" } // 27EB;Na          # Pe         MATHEMATICAL RIGHT DOUBLE ANGLE BRACKET
            { Start = 0x27EC; Last = 0x27EC; Width = EastAsianWidth.OfText "Na" } // 27EC;Na          # Ps         MATHEMATICAL LEFT WHITE TORTOISE SHELL BRACKET
            { Start = 0x27ED; Last = 0x27ED; Width = EastAsianWidth.OfText "Na" } // 27ED;Na          # Pe         MATHEMATICAL RIGHT WHITE TORTOISE SHELL BRACKET
            { Start = 0x27EE; Last = 0x27EE; Width = EastAsianWidth.OfText "N"  } // 27EE;N           # Ps         MATHEMATICAL LEFT FLATTENED PARENTHESIS
            { Start = 0x27EF; Last = 0x27EF; Width = EastAsianWidth.OfText "N"  } // 27EF;N           # Pe         MATHEMATICAL RIGHT FLATTENED PARENTHESIS
            { Start = 0x27F0; Last = 0x27FF; Width = EastAsianWidth.OfText "N"  } // 27F0..27FF;N     # Sm    [16] UPWARDS QUADRUPLE ARROW..LONG RIGHTWARDS SQUIGGLE ARROW
            { Start = 0x2800; Last = 0x28FF; Width = EastAsianWidth.OfText "N"  } // 2800..28FF;N     # So   [256] BRAILLE PATTERN BLANK..BRAILLE PATTERN DOTS-12345678
            { Start = 0x2900; Last = 0x297F; Width = EastAsianWidth.OfText "N"  } // 2900..297F;N     # Sm   [128] RIGHTWARDS TWO-HEADED ARROW WITH VERTICAL STROKE..DOWN FISH TAIL
            { Start = 0x2980; Last = 0x2982; Width = EastAsianWidth.OfText "N"  } // 2980..2982;N     # Sm     [3] TRIPLE VERTICAL BAR DELIMITER..Z NOTATION TYPE COLON
            { Start = 0x2983; Last = 0x2983; Width = EastAsianWidth.OfText "N"  } // 2983;N           # Ps         LEFT WHITE CURLY BRACKET
            { Start = 0x2984; Last = 0x2984; Width = EastAsianWidth.OfText "N"  } // 2984;N           # Pe         RIGHT WHITE CURLY BRACKET
            { Start = 0x2985; Last = 0x2985; Width = EastAsianWidth.OfText "Na" } // 2985;Na          # Ps         LEFT WHITE PARENTHESIS
            { Start = 0x2986; Last = 0x2986; Width = EastAsianWidth.OfText "Na" } // 2986;Na          # Pe         RIGHT WHITE PARENTHESIS
            { Start = 0x2987; Last = 0x2987; Width = EastAsianWidth.OfText "N"  } // 2987;N           # Ps         Z NOTATION LEFT IMAGE BRACKET
            { Start = 0x2988; Last = 0x2988; Width = EastAsianWidth.OfText "N"  } // 2988;N           # Pe         Z NOTATION RIGHT IMAGE BRACKET
            { Start = 0x2989; Last = 0x2989; Width = EastAsianWidth.OfText "N"  } // 2989;N           # Ps         Z NOTATION LEFT BINDING BRACKET
            { Start = 0x298A; Last = 0x298A; Width = EastAsianWidth.OfText "N"  } // 298A;N           # Pe         Z NOTATION RIGHT BINDING BRACKET
            { Start = 0x298B; Last = 0x298B; Width = EastAsianWidth.OfText "N"  } // 298B;N           # Ps         LEFT SQUARE BRACKET WITH UNDERBAR
            { Start = 0x298C; Last = 0x298C; Width = EastAsianWidth.OfText "N"  } // 298C;N           # Pe         RIGHT SQUARE BRACKET WITH UNDERBAR
            { Start = 0x298D; Last = 0x298D; Width = EastAsianWidth.OfText "N"  } // 298D;N           # Ps         LEFT SQUARE BRACKET WITH TICK IN TOP CORNER
            { Start = 0x298E; Last = 0x298E; Width = EastAsianWidth.OfText "N"  } // 298E;N           # Pe         RIGHT SQUARE BRACKET WITH TICK IN BOTTOM CORNER
            { Start = 0x298F; Last = 0x298F; Width = EastAsianWidth.OfText "N"  } // 298F;N           # Ps         LEFT SQUARE BRACKET WITH TICK IN BOTTOM CORNER
            { Start = 0x2990; Last = 0x2990; Width = EastAsianWidth.OfText "N"  } // 2990;N           # Pe         RIGHT SQUARE BRACKET WITH TICK IN TOP CORNER
            { Start = 0x2991; Last = 0x2991; Width = EastAsianWidth.OfText "N"  } // 2991;N           # Ps         LEFT ANGLE BRACKET WITH DOT
            { Start = 0x2992; Last = 0x2992; Width = EastAsianWidth.OfText "N"  } // 2992;N           # Pe         RIGHT ANGLE BRACKET WITH DOT
            { Start = 0x2993; Last = 0x2993; Width = EastAsianWidth.OfText "N"  } // 2993;N           # Ps         LEFT ARC LESS-THAN BRACKET
            { Start = 0x2994; Last = 0x2994; Width = EastAsianWidth.OfText "N"  } // 2994;N           # Pe         RIGHT ARC GREATER-THAN BRACKET
            { Start = 0x2995; Last = 0x2995; Width = EastAsianWidth.OfText "N"  } // 2995;N           # Ps         DOUBLE LEFT ARC GREATER-THAN BRACKET
            { Start = 0x2996; Last = 0x2996; Width = EastAsianWidth.OfText "N"  } // 2996;N           # Pe         DOUBLE RIGHT ARC LESS-THAN BRACKET
            { Start = 0x2997; Last = 0x2997; Width = EastAsianWidth.OfText "N"  } // 2997;N           # Ps         LEFT BLACK TORTOISE SHELL BRACKET
            { Start = 0x2998; Last = 0x2998; Width = EastAsianWidth.OfText "N"  } // 2998;N           # Pe         RIGHT BLACK TORTOISE SHELL BRACKET
            { Start = 0x2999; Last = 0x29D7; Width = EastAsianWidth.OfText "N"  } // 2999..29D7;N     # Sm    [63] DOTTED FENCE..BLACK HOURGLASS
            { Start = 0x29D8; Last = 0x29D8; Width = EastAsianWidth.OfText "N"  } // 29D8;N           # Ps         LEFT WIGGLY FENCE
            { Start = 0x29D9; Last = 0x29D9; Width = EastAsianWidth.OfText "N"  } // 29D9;N           # Pe         RIGHT WIGGLY FENCE
            { Start = 0x29DA; Last = 0x29DA; Width = EastAsianWidth.OfText "N"  } // 29DA;N           # Ps         LEFT DOUBLE WIGGLY FENCE
            { Start = 0x29DB; Last = 0x29DB; Width = EastAsianWidth.OfText "N"  } // 29DB;N           # Pe         RIGHT DOUBLE WIGGLY FENCE
            { Start = 0x29DC; Last = 0x29FB; Width = EastAsianWidth.OfText "N"  } // 29DC..29FB;N     # Sm    [32] INCOMPLETE INFINITY..TRIPLE PLUS
            { Start = 0x29FC; Last = 0x29FC; Width = EastAsianWidth.OfText "N"  } // 29FC;N           # Ps         LEFT-POINTING CURVED ANGLE BRACKET
            { Start = 0x29FD; Last = 0x29FD; Width = EastAsianWidth.OfText "N"  } // 29FD;N           # Pe         RIGHT-POINTING CURVED ANGLE BRACKET
            { Start = 0x29FE; Last = 0x29FF; Width = EastAsianWidth.OfText "N"  } // 29FE..29FF;N     # Sm     [2] TINY..MINY
            { Start = 0x2A00; Last = 0x2AFF; Width = EastAsianWidth.OfText "N"  } // 2A00..2AFF;N     # Sm   [256] N-ARY CIRCLED DOT OPERATOR..N-ARY WHITE VERTICAL BAR
            { Start = 0x2B00; Last = 0x2B1A; Width = EastAsianWidth.OfText "N"  } // 2B00..2B1A;N     # So    [27] NORTH EAST WHITE ARROW..DOTTED SQUARE
            { Start = 0x2B1B; Last = 0x2B1C; Width = EastAsianWidth.OfText "W"  } // 2B1B..2B1C;W     # So     [2] BLACK LARGE SQUARE..WHITE LARGE SQUARE
            { Start = 0x2B1D; Last = 0x2B2F; Width = EastAsianWidth.OfText "N"  } // 2B1D..2B2F;N     # So    [19] BLACK VERY SMALL SQUARE..WHITE VERTICAL ELLIPSE
            { Start = 0x2B30; Last = 0x2B44; Width = EastAsianWidth.OfText "N"  } // 2B30..2B44;N     # Sm    [21] LEFT ARROW WITH SMALL CIRCLE..RIGHTWARDS ARROW THROUGH SUPERSET
            { Start = 0x2B45; Last = 0x2B46; Width = EastAsianWidth.OfText "N"  } // 2B45..2B46;N     # So     [2] LEFTWARDS QUADRUPLE ARROW..RIGHTWARDS QUADRUPLE ARROW
            { Start = 0x2B47; Last = 0x2B4C; Width = EastAsianWidth.OfText "N"  } // 2B47..2B4C;N     # Sm     [6] REVERSE TILDE OPERATOR ABOVE RIGHTWARDS ARROW..RIGHTWARDS ARROW ABOVE REVERSE TILDE OPERATOR
            { Start = 0x2B4D; Last = 0x2B4F; Width = EastAsianWidth.OfText "N"  } // 2B4D..2B4F;N     # So     [3] DOWNWARDS TRIANGLE-HEADED ZIGZAG ARROW..SHORT BACKSLANTED SOUTH ARROW
            { Start = 0x2B50; Last = 0x2B50; Width = EastAsianWidth.OfText "W"  } // 2B50;W           # So         WHITE MEDIUM STAR
            { Start = 0x2B51; Last = 0x2B54; Width = EastAsianWidth.OfText "N"  } // 2B51..2B54;N     # So     [4] BLACK SMALL STAR..WHITE RIGHT-POINTING PENTAGON
            { Start = 0x2B55; Last = 0x2B55; Width = EastAsianWidth.OfText "W"  } // 2B55;W           # So         HEAVY LARGE CIRCLE
            { Start = 0x2B56; Last = 0x2B59; Width = EastAsianWidth.OfText "A"  } // 2B56..2B59;A     # So     [4] HEAVY OVAL WITH OVAL INSIDE..HEAVY CIRCLED SALTIRE
            { Start = 0x2B5A; Last = 0x2B73; Width = EastAsianWidth.OfText "N"  } // 2B5A..2B73;N     # So    [26] SLANTED NORTH ARROW WITH HOOKED HEAD..DOWNWARDS TRIANGLE-HEADED ARROW TO BAR
            { Start = 0x2B76; Last = 0x2B95; Width = EastAsianWidth.OfText "N"  } // 2B76..2B95;N     # So    [32] NORTH WEST TRIANGLE-HEADED ARROW TO BAR..RIGHTWARDS BLACK ARROW
            { Start = 0x2B98; Last = 0x2BC8; Width = EastAsianWidth.OfText "N"  } // 2B98..2BC8;N     # So    [49] THREE-D TOP-LIGHTED LEFTWARDS EQUILATERAL ARROWHEAD..BLACK MEDIUM RIGHT-POINTING TRIANGLE CENTRED
            { Start = 0x2BCA; Last = 0x2BFE; Width = EastAsianWidth.OfText "N"  } // 2BCA..2BFE;N     # So    [53] TOP HALF BLACK CIRCLE..REVERSED RIGHT ANGLE
            { Start = 0x2C00; Last = 0x2C2E; Width = EastAsianWidth.OfText "N"  } // 2C00..2C2E;N     # Lu    [47] GLAGOLITIC CAPITAL LETTER AZU..GLAGOLITIC CAPITAL LETTER LATINATE MYSLITE
            { Start = 0x2C30; Last = 0x2C5E; Width = EastAsianWidth.OfText "N"  } // 2C30..2C5E;N     # Ll    [47] GLAGOLITIC SMALL LETTER AZU..GLAGOLITIC SMALL LETTER LATINATE MYSLITE
            { Start = 0x2C60; Last = 0x2C7B; Width = EastAsianWidth.OfText "N"  } // 2C60..2C7B;N     # L&    [28] LATIN CAPITAL LETTER L WITH DOUBLE BAR..LATIN LETTER SMALL CAPITAL TURNED E
            { Start = 0x2C7C; Last = 0x2C7D; Width = EastAsianWidth.OfText "N"  } // 2C7C..2C7D;N     # Lm     [2] LATIN SUBSCRIPT SMALL LETTER J..MODIFIER LETTER CAPITAL V
            { Start = 0x2C7E; Last = 0x2C7F; Width = EastAsianWidth.OfText "N"  } // 2C7E..2C7F;N     # Lu     [2] LATIN CAPITAL LETTER S WITH SWASH TAIL..LATIN CAPITAL LETTER Z WITH SWASH TAIL
            { Start = 0x2C80; Last = 0x2CE4; Width = EastAsianWidth.OfText "N"  } // 2C80..2CE4;N     # L&   [101] COPTIC CAPITAL LETTER ALFA..COPTIC SYMBOL KAI
            { Start = 0x2CE5; Last = 0x2CEA; Width = EastAsianWidth.OfText "N"  } // 2CE5..2CEA;N     # So     [6] COPTIC SYMBOL MI RO..COPTIC SYMBOL SHIMA SIMA
            { Start = 0x2CEB; Last = 0x2CEE; Width = EastAsianWidth.OfText "N"  } // 2CEB..2CEE;N     # L&     [4] COPTIC CAPITAL LETTER CRYPTOGRAMMIC SHEI..COPTIC SMALL LETTER CRYPTOGRAMMIC GANGIA
            { Start = 0x2CEF; Last = 0x2CF1; Width = EastAsianWidth.OfText "N"  } // 2CEF..2CF1;N     # Mn     [3] COPTIC COMBINING NI ABOVE..COPTIC COMBINING SPIRITUS LENIS
            { Start = 0x2CF2; Last = 0x2CF3; Width = EastAsianWidth.OfText "N"  } // 2CF2..2CF3;N     # L&     [2] COPTIC CAPITAL LETTER BOHAIRIC KHEI..COPTIC SMALL LETTER BOHAIRIC KHEI
            { Start = 0x2CF9; Last = 0x2CFC; Width = EastAsianWidth.OfText "N"  } // 2CF9..2CFC;N     # Po     [4] COPTIC OLD NUBIAN FULL STOP..COPTIC OLD NUBIAN VERSE DIVIDER
            { Start = 0x2CFD; Last = 0x2CFD; Width = EastAsianWidth.OfText "N"  } // 2CFD;N           # No         COPTIC FRACTION ONE HALF
            { Start = 0x2CFE; Last = 0x2CFF; Width = EastAsianWidth.OfText "N"  } // 2CFE..2CFF;N     # Po     [2] COPTIC FULL STOP..COPTIC MORPHOLOGICAL DIVIDER
            { Start = 0x2D00; Last = 0x2D25; Width = EastAsianWidth.OfText "N"  } // 2D00..2D25;N     # Ll    [38] GEORGIAN SMALL LETTER AN..GEORGIAN SMALL LETTER HOE
            { Start = 0x2D27; Last = 0x2D27; Width = EastAsianWidth.OfText "N"  } // 2D27;N           # Ll         GEORGIAN SMALL LETTER YN
            { Start = 0x2D2D; Last = 0x2D2D; Width = EastAsianWidth.OfText "N"  } // 2D2D;N           # Ll         GEORGIAN SMALL LETTER AEN
            { Start = 0x2D30; Last = 0x2D67; Width = EastAsianWidth.OfText "N"  } // 2D30..2D67;N     # Lo    [56] TIFINAGH LETTER YA..TIFINAGH LETTER YO
            { Start = 0x2D6F; Last = 0x2D6F; Width = EastAsianWidth.OfText "N"  } // 2D6F;N           # Lm         TIFINAGH MODIFIER LETTER LABIALIZATION MARK
            { Start = 0x2D70; Last = 0x2D70; Width = EastAsianWidth.OfText "N"  } // 2D70;N           # Po         TIFINAGH SEPARATOR MARK
            { Start = 0x2D7F; Last = 0x2D7F; Width = EastAsianWidth.OfText "N"  } // 2D7F;N           # Mn         TIFINAGH CONSONANT JOINER
            { Start = 0x2D80; Last = 0x2D96; Width = EastAsianWidth.OfText "N"  } // 2D80..2D96;N     # Lo    [23] ETHIOPIC SYLLABLE LOA..ETHIOPIC SYLLABLE GGWE
            { Start = 0x2DA0; Last = 0x2DA6; Width = EastAsianWidth.OfText "N"  } // 2DA0..2DA6;N     # Lo     [7] ETHIOPIC SYLLABLE SSA..ETHIOPIC SYLLABLE SSO
            { Start = 0x2DA8; Last = 0x2DAE; Width = EastAsianWidth.OfText "N"  } // 2DA8..2DAE;N     # Lo     [7] ETHIOPIC SYLLABLE CCA..ETHIOPIC SYLLABLE CCO
            { Start = 0x2DB0; Last = 0x2DB6; Width = EastAsianWidth.OfText "N"  } // 2DB0..2DB6;N     # Lo     [7] ETHIOPIC SYLLABLE ZZA..ETHIOPIC SYLLABLE ZZO
            { Start = 0x2DB8; Last = 0x2DBE; Width = EastAsianWidth.OfText "N"  } // 2DB8..2DBE;N     # Lo     [7] ETHIOPIC SYLLABLE CCHA..ETHIOPIC SYLLABLE CCHO
            { Start = 0x2DC0; Last = 0x2DC6; Width = EastAsianWidth.OfText "N"  } // 2DC0..2DC6;N     # Lo     [7] ETHIOPIC SYLLABLE QYA..ETHIOPIC SYLLABLE QYO
            { Start = 0x2DC8; Last = 0x2DCE; Width = EastAsianWidth.OfText "N"  } // 2DC8..2DCE;N     # Lo     [7] ETHIOPIC SYLLABLE KYA..ETHIOPIC SYLLABLE KYO
            { Start = 0x2DD0; Last = 0x2DD6; Width = EastAsianWidth.OfText "N"  } // 2DD0..2DD6;N     # Lo     [7] ETHIOPIC SYLLABLE XYA..ETHIOPIC SYLLABLE XYO
            { Start = 0x2DD8; Last = 0x2DDE; Width = EastAsianWidth.OfText "N"  } // 2DD8..2DDE;N     # Lo     [7] ETHIOPIC SYLLABLE GYA..ETHIOPIC SYLLABLE GYO
            { Start = 0x2DE0; Last = 0x2DFF; Width = EastAsianWidth.OfText "N"  } // 2DE0..2DFF;N     # Mn    [32] COMBINING CYRILLIC LETTER BE..COMBINING CYRILLIC LETTER IOTIFIED BIG YUS
            { Start = 0x2E00; Last = 0x2E01; Width = EastAsianWidth.OfText "N"  } // 2E00..2E01;N     # Po     [2] RIGHT ANGLE SUBSTITUTION MARKER..RIGHT ANGLE DOTTED SUBSTITUTION MARKER
            { Start = 0x2E02; Last = 0x2E02; Width = EastAsianWidth.OfText "N"  } // 2E02;N           # Pi         LEFT SUBSTITUTION BRACKET
            { Start = 0x2E03; Last = 0x2E03; Width = EastAsianWidth.OfText "N"  } // 2E03;N           # Pf         RIGHT SUBSTITUTION BRACKET
            { Start = 0x2E04; Last = 0x2E04; Width = EastAsianWidth.OfText "N"  } // 2E04;N           # Pi         LEFT DOTTED SUBSTITUTION BRACKET
            { Start = 0x2E05; Last = 0x2E05; Width = EastAsianWidth.OfText "N"  } // 2E05;N           # Pf         RIGHT DOTTED SUBSTITUTION BRACKET
            { Start = 0x2E06; Last = 0x2E08; Width = EastAsianWidth.OfText "N"  } // 2E06..2E08;N     # Po     [3] RAISED INTERPOLATION MARKER..DOTTED TRANSPOSITION MARKER
            { Start = 0x2E09; Last = 0x2E09; Width = EastAsianWidth.OfText "N"  } // 2E09;N           # Pi         LEFT TRANSPOSITION BRACKET
            { Start = 0x2E0A; Last = 0x2E0A; Width = EastAsianWidth.OfText "N"  } // 2E0A;N           # Pf         RIGHT TRANSPOSITION BRACKET
            { Start = 0x2E0B; Last = 0x2E0B; Width = EastAsianWidth.OfText "N"  } // 2E0B;N           # Po         RAISED SQUARE
            { Start = 0x2E0C; Last = 0x2E0C; Width = EastAsianWidth.OfText "N"  } // 2E0C;N           # Pi         LEFT RAISED OMISSION BRACKET
            { Start = 0x2E0D; Last = 0x2E0D; Width = EastAsianWidth.OfText "N"  } // 2E0D;N           # Pf         RIGHT RAISED OMISSION BRACKET
            { Start = 0x2E0E; Last = 0x2E16; Width = EastAsianWidth.OfText "N"  } // 2E0E..2E16;N     # Po     [9] EDITORIAL CORONIS..DOTTED RIGHT-POINTING ANGLE
            { Start = 0x2E17; Last = 0x2E17; Width = EastAsianWidth.OfText "N"  } // 2E17;N           # Pd         DOUBLE OBLIQUE HYPHEN
            { Start = 0x2E18; Last = 0x2E19; Width = EastAsianWidth.OfText "N"  } // 2E18..2E19;N     # Po     [2] INVERTED INTERROBANG..PALM BRANCH
            { Start = 0x2E1A; Last = 0x2E1A; Width = EastAsianWidth.OfText "N"  } // 2E1A;N           # Pd         HYPHEN WITH DIAERESIS
            { Start = 0x2E1B; Last = 0x2E1B; Width = EastAsianWidth.OfText "N"  } // 2E1B;N           # Po         TILDE WITH RING ABOVE
            { Start = 0x2E1C; Last = 0x2E1C; Width = EastAsianWidth.OfText "N"  } // 2E1C;N           # Pi         LEFT LOW PARAPHRASE BRACKET
            { Start = 0x2E1D; Last = 0x2E1D; Width = EastAsianWidth.OfText "N"  } // 2E1D;N           # Pf         RIGHT LOW PARAPHRASE BRACKET
            { Start = 0x2E1E; Last = 0x2E1F; Width = EastAsianWidth.OfText "N"  } // 2E1E..2E1F;N     # Po     [2] TILDE WITH DOT ABOVE..TILDE WITH DOT BELOW
            { Start = 0x2E20; Last = 0x2E20; Width = EastAsianWidth.OfText "N"  } // 2E20;N           # Pi         LEFT VERTICAL BAR WITH QUILL
            { Start = 0x2E21; Last = 0x2E21; Width = EastAsianWidth.OfText "N"  } // 2E21;N           # Pf         RIGHT VERTICAL BAR WITH QUILL
            { Start = 0x2E22; Last = 0x2E22; Width = EastAsianWidth.OfText "N"  } // 2E22;N           # Ps         TOP LEFT HALF BRACKET
            { Start = 0x2E23; Last = 0x2E23; Width = EastAsianWidth.OfText "N"  } // 2E23;N           # Pe         TOP RIGHT HALF BRACKET
            { Start = 0x2E24; Last = 0x2E24; Width = EastAsianWidth.OfText "N"  } // 2E24;N           # Ps         BOTTOM LEFT HALF BRACKET
            { Start = 0x2E25; Last = 0x2E25; Width = EastAsianWidth.OfText "N"  } // 2E25;N           # Pe         BOTTOM RIGHT HALF BRACKET
            { Start = 0x2E26; Last = 0x2E26; Width = EastAsianWidth.OfText "N"  } // 2E26;N           # Ps         LEFT SIDEWAYS U BRACKET
            { Start = 0x2E27; Last = 0x2E27; Width = EastAsianWidth.OfText "N"  } // 2E27;N           # Pe         RIGHT SIDEWAYS U BRACKET
            { Start = 0x2E28; Last = 0x2E28; Width = EastAsianWidth.OfText "N"  } // 2E28;N           # Ps         LEFT DOUBLE PARENTHESIS
            { Start = 0x2E29; Last = 0x2E29; Width = EastAsianWidth.OfText "N"  } // 2E29;N           # Pe         RIGHT DOUBLE PARENTHESIS
            { Start = 0x2E2A; Last = 0x2E2E; Width = EastAsianWidth.OfText "N"  } // 2E2A..2E2E;N     # Po     [5] TWO DOTS OVER ONE DOT PUNCTUATION..REVERSED QUESTION MARK
            { Start = 0x2E2F; Last = 0x2E2F; Width = EastAsianWidth.OfText "N"  } // 2E2F;N           # Lm         VERTICAL TILDE
            { Start = 0x2E30; Last = 0x2E39; Width = EastAsianWidth.OfText "N"  } // 2E30..2E39;N     # Po    [10] RING POINT..TOP HALF SECTION SIGN
            { Start = 0x2E3A; Last = 0x2E3B; Width = EastAsianWidth.OfText "N"  } // 2E3A..2E3B;N     # Pd     [2] TWO-EM DASH..THREE-EM DASH
            { Start = 0x2E3C; Last = 0x2E3F; Width = EastAsianWidth.OfText "N"  } // 2E3C..2E3F;N     # Po     [4] STENOGRAPHIC FULL STOP..CAPITULUM
            { Start = 0x2E40; Last = 0x2E40; Width = EastAsianWidth.OfText "N"  } // 2E40;N           # Pd         DOUBLE HYPHEN
            { Start = 0x2E41; Last = 0x2E41; Width = EastAsianWidth.OfText "N"  } // 2E41;N           # Po         REVERSED COMMA
            { Start = 0x2E42; Last = 0x2E42; Width = EastAsianWidth.OfText "N"  } // 2E42;N           # Ps         DOUBLE LOW-REVERSED-9 QUOTATION MARK
            { Start = 0x2E43; Last = 0x2E4E; Width = EastAsianWidth.OfText "N"  } // 2E43..2E4E;N     # Po    [12] DASH WITH LEFT UPTURN..PUNCTUS ELEVATUS MARK
            { Start = 0x2E80; Last = 0x2E99; Width = EastAsianWidth.OfText "W"  } // 2E80..2E99;W     # So    [26] CJK RADICAL REPEAT..CJK RADICAL RAP
            { Start = 0x2E9B; Last = 0x2EF3; Width = EastAsianWidth.OfText "W"  } // 2E9B..2EF3;W     # So    [89] CJK RADICAL CHOKE..CJK RADICAL C-SIMPLIFIED TURTLE
            { Start = 0x2F00; Last = 0x2FD5; Width = EastAsianWidth.OfText "W"  } // 2F00..2FD5;W     # So   [214] KANGXI RADICAL ONE..KANGXI RADICAL FLUTE
            { Start = 0x2FF0; Last = 0x2FFB; Width = EastAsianWidth.OfText "W"  } // 2FF0..2FFB;W     # So    [12] IDEOGRAPHIC DESCRIPTION CHARACTER LEFT TO RIGHT..IDEOGRAPHIC DESCRIPTION CHARACTER OVERLAID
            { Start = 0x3000; Last = 0x3000; Width = EastAsianWidth.OfText "F"  } // 3000;F           # Zs         IDEOGRAPHIC SPACE
            { Start = 0x3001; Last = 0x3003; Width = EastAsianWidth.OfText "W"  } // 3001..3003;W     # Po     [3] IDEOGRAPHIC COMMA..DITTO MARK
            { Start = 0x3004; Last = 0x3004; Width = EastAsianWidth.OfText "W"  } // 3004;W           # So         JAPANESE INDUSTRIAL STANDARD SYMBOL
            { Start = 0x3005; Last = 0x3005; Width = EastAsianWidth.OfText "W"  } // 3005;W           # Lm         IDEOGRAPHIC ITERATION MARK
            { Start = 0x3006; Last = 0x3006; Width = EastAsianWidth.OfText "W"  } // 3006;W           # Lo         IDEOGRAPHIC CLOSING MARK
            { Start = 0x3007; Last = 0x3007; Width = EastAsianWidth.OfText "W"  } // 3007;W           # Nl         IDEOGRAPHIC NUMBER ZERO
            { Start = 0x3008; Last = 0x3008; Width = EastAsianWidth.OfText "W"  } // 3008;W           # Ps         LEFT ANGLE BRACKET
            { Start = 0x3009; Last = 0x3009; Width = EastAsianWidth.OfText "W"  } // 3009;W           # Pe         RIGHT ANGLE BRACKET
            { Start = 0x300A; Last = 0x300A; Width = EastAsianWidth.OfText "W"  } // 300A;W           # Ps         LEFT DOUBLE ANGLE BRACKET
            { Start = 0x300B; Last = 0x300B; Width = EastAsianWidth.OfText "W"  } // 300B;W           # Pe         RIGHT DOUBLE ANGLE BRACKET
            { Start = 0x300C; Last = 0x300C; Width = EastAsianWidth.OfText "W"  } // 300C;W           # Ps         LEFT CORNER BRACKET
            { Start = 0x300D; Last = 0x300D; Width = EastAsianWidth.OfText "W"  } // 300D;W           # Pe         RIGHT CORNER BRACKET
            { Start = 0x300E; Last = 0x300E; Width = EastAsianWidth.OfText "W"  } // 300E;W           # Ps         LEFT WHITE CORNER BRACKET
            { Start = 0x300F; Last = 0x300F; Width = EastAsianWidth.OfText "W"  } // 300F;W           # Pe         RIGHT WHITE CORNER BRACKET
            { Start = 0x3010; Last = 0x3010; Width = EastAsianWidth.OfText "W"  } // 3010;W           # Ps         LEFT BLACK LENTICULAR BRACKET
            { Start = 0x3011; Last = 0x3011; Width = EastAsianWidth.OfText "W"  } // 3011;W           # Pe         RIGHT BLACK LENTICULAR BRACKET
            { Start = 0x3012; Last = 0x3013; Width = EastAsianWidth.OfText "W"  } // 3012..3013;W     # So     [2] POSTAL MARK..GETA MARK
            { Start = 0x3014; Last = 0x3014; Width = EastAsianWidth.OfText "W"  } // 3014;W           # Ps         LEFT TORTOISE SHELL BRACKET
            { Start = 0x3015; Last = 0x3015; Width = EastAsianWidth.OfText "W"  } // 3015;W           # Pe         RIGHT TORTOISE SHELL BRACKET
            { Start = 0x3016; Last = 0x3016; Width = EastAsianWidth.OfText "W"  } // 3016;W           # Ps         LEFT WHITE LENTICULAR BRACKET
            { Start = 0x3017; Last = 0x3017; Width = EastAsianWidth.OfText "W"  } // 3017;W           # Pe         RIGHT WHITE LENTICULAR BRACKET
            { Start = 0x3018; Last = 0x3018; Width = EastAsianWidth.OfText "W"  } // 3018;W           # Ps         LEFT WHITE TORTOISE SHELL BRACKET
            { Start = 0x3019; Last = 0x3019; Width = EastAsianWidth.OfText "W"  } // 3019;W           # Pe         RIGHT WHITE TORTOISE SHELL BRACKET
            { Start = 0x301A; Last = 0x301A; Width = EastAsianWidth.OfText "W"  } // 301A;W           # Ps         LEFT WHITE SQUARE BRACKET
            { Start = 0x301B; Last = 0x301B; Width = EastAsianWidth.OfText "W"  } // 301B;W           # Pe         RIGHT WHITE SQUARE BRACKET
            { Start = 0x301C; Last = 0x301C; Width = EastAsianWidth.OfText "W"  } // 301C;W           # Pd         WAVE DASH
            { Start = 0x301D; Last = 0x301D; Width = EastAsianWidth.OfText "W"  } // 301D;W           # Ps         REVERSED DOUBLE PRIME QUOTATION MARK
            { Start = 0x301E; Last = 0x301F; Width = EastAsianWidth.OfText "W"  } // 301E..301F;W     # Pe     [2] DOUBLE PRIME QUOTATION MARK..LOW DOUBLE PRIME QUOTATION MARK
            { Start = 0x3020; Last = 0x3020; Width = EastAsianWidth.OfText "W"  } // 3020;W           # So         POSTAL MARK FACE
            { Start = 0x3021; Last = 0x3029; Width = EastAsianWidth.OfText "W"  } // 3021..3029;W     # Nl     [9] HANGZHOU NUMERAL ONE..HANGZHOU NUMERAL NINE
            { Start = 0x302A; Last = 0x302D; Width = EastAsianWidth.OfText "W"  } // 302A..302D;W     # Mn     [4] IDEOGRAPHIC LEVEL TONE MARK..IDEOGRAPHIC ENTERING TONE MARK
            { Start = 0x302E; Last = 0x302F; Width = EastAsianWidth.OfText "W"  } // 302E..302F;W     # Mc     [2] HANGUL SINGLE DOT TONE MARK..HANGUL DOUBLE DOT TONE MARK
            { Start = 0x3030; Last = 0x3030; Width = EastAsianWidth.OfText "W"  } // 3030;W           # Pd         WAVY DASH
            { Start = 0x3031; Last = 0x3035; Width = EastAsianWidth.OfText "W"  } // 3031..3035;W     # Lm     [5] VERTICAL KANA REPEAT MARK..VERTICAL KANA REPEAT MARK LOWER HALF
            { Start = 0x3036; Last = 0x3037; Width = EastAsianWidth.OfText "W"  } // 3036..3037;W     # So     [2] CIRCLED POSTAL MARK..IDEOGRAPHIC TELEGRAPH LINE FEED SEPARATOR SYMBOL
            { Start = 0x3038; Last = 0x303A; Width = EastAsianWidth.OfText "W"  } // 3038..303A;W     # Nl     [3] HANGZHOU NUMERAL TEN..HANGZHOU NUMERAL THIRTY
            { Start = 0x303B; Last = 0x303B; Width = EastAsianWidth.OfText "W"  } // 303B;W           # Lm         VERTICAL IDEOGRAPHIC ITERATION MARK
            { Start = 0x303C; Last = 0x303C; Width = EastAsianWidth.OfText "W"  } // 303C;W           # Lo         MASU MARK
            { Start = 0x303D; Last = 0x303D; Width = EastAsianWidth.OfText "W"  } // 303D;W           # Po         PART ALTERNATION MARK
            { Start = 0x303E; Last = 0x303E; Width = EastAsianWidth.OfText "W"  } // 303E;W           # So         IDEOGRAPHIC VARIATION INDICATOR
            { Start = 0x303F; Last = 0x303F; Width = EastAsianWidth.OfText "N"  } // 303F;N           # So         IDEOGRAPHIC HALF FILL SPACE
            { Start = 0x3041; Last = 0x3096; Width = EastAsianWidth.OfText "W"  } // 3041..3096;W     # Lo    [86] HIRAGANA LETTER SMALL A..HIRAGANA LETTER SMALL KE
            { Start = 0x3099; Last = 0x309A; Width = EastAsianWidth.OfText "W"  } // 3099..309A;W     # Mn     [2] COMBINING KATAKANA-HIRAGANA VOICED SOUND MARK..COMBINING KATAKANA-HIRAGANA SEMI-VOICED SOUND MARK
            { Start = 0x309B; Last = 0x309C; Width = EastAsianWidth.OfText "W"  } // 309B..309C;W     # Sk     [2] KATAKANA-HIRAGANA VOICED SOUND MARK..KATAKANA-HIRAGANA SEMI-VOICED SOUND MARK
            { Start = 0x309D; Last = 0x309E; Width = EastAsianWidth.OfText "W"  } // 309D..309E;W     # Lm     [2] HIRAGANA ITERATION MARK..HIRAGANA VOICED ITERATION MARK
            { Start = 0x309F; Last = 0x309F; Width = EastAsianWidth.OfText "W"  } // 309F;W           # Lo         HIRAGANA DIGRAPH YORI
            { Start = 0x30A0; Last = 0x30A0; Width = EastAsianWidth.OfText "W"  } // 30A0;W           # Pd         KATAKANA-HIRAGANA DOUBLE HYPHEN
            { Start = 0x30A1; Last = 0x30FA; Width = EastAsianWidth.OfText "W"  } // 30A1..30FA;W     # Lo    [90] KATAKANA LETTER SMALL A..KATAKANA LETTER VO
            { Start = 0x30FB; Last = 0x30FB; Width = EastAsianWidth.OfText "W"  } // 30FB;W           # Po         KATAKANA MIDDLE DOT
            { Start = 0x30FC; Last = 0x30FE; Width = EastAsianWidth.OfText "W"  } // 30FC..30FE;W     # Lm     [3] KATAKANA-HIRAGANA PROLONGED SOUND MARK..KATAKANA VOICED ITERATION MARK
            { Start = 0x30FF; Last = 0x30FF; Width = EastAsianWidth.OfText "W"  } // 30FF;W           # Lo         KATAKANA DIGRAPH KOTO
            { Start = 0x3105; Last = 0x312F; Width = EastAsianWidth.OfText "W"  } // 3105..312F;W     # Lo    [43] BOPOMOFO LETTER B..BOPOMOFO LETTER NN
            { Start = 0x3131; Last = 0x318E; Width = EastAsianWidth.OfText "W"  } // 3131..318E;W     # Lo    [94] HANGUL LETTER KIYEOK..HANGUL LETTER ARAEAE
            { Start = 0x3190; Last = 0x3191; Width = EastAsianWidth.OfText "W"  } // 3190..3191;W     # So     [2] IDEOGRAPHIC ANNOTATION LINKING MARK..IDEOGRAPHIC ANNOTATION REVERSE MARK
            { Start = 0x3192; Last = 0x3195; Width = EastAsianWidth.OfText "W"  } // 3192..3195;W     # No     [4] IDEOGRAPHIC ANNOTATION ONE MARK..IDEOGRAPHIC ANNOTATION FOUR MARK
            { Start = 0x3196; Last = 0x319F; Width = EastAsianWidth.OfText "W"  } // 3196..319F;W     # So    [10] IDEOGRAPHIC ANNOTATION TOP MARK..IDEOGRAPHIC ANNOTATION MAN MARK
            { Start = 0x31A0; Last = 0x31BA; Width = EastAsianWidth.OfText "W"  } // 31A0..31BA;W     # Lo    [27] BOPOMOFO LETTER BU..BOPOMOFO LETTER ZY
            { Start = 0x31C0; Last = 0x31E3; Width = EastAsianWidth.OfText "W"  } // 31C0..31E3;W     # So    [36] CJK STROKE T..CJK STROKE Q
            { Start = 0x31F0; Last = 0x31FF; Width = EastAsianWidth.OfText "W"  } // 31F0..31FF;W     # Lo    [16] KATAKANA LETTER SMALL KU..KATAKANA LETTER SMALL RO
            { Start = 0x3200; Last = 0x321E; Width = EastAsianWidth.OfText "W"  } // 3200..321E;W     # So    [31] PARENTHESIZED HANGUL KIYEOK..PARENTHESIZED KOREAN CHARACTER O HU
            { Start = 0x3220; Last = 0x3229; Width = EastAsianWidth.OfText "W"  } // 3220..3229;W     # No    [10] PARENTHESIZED IDEOGRAPH ONE..PARENTHESIZED IDEOGRAPH TEN
            { Start = 0x322A; Last = 0x3247; Width = EastAsianWidth.OfText "W"  } // 322A..3247;W     # So    [30] PARENTHESIZED IDEOGRAPH MOON..CIRCLED IDEOGRAPH KOTO
            { Start = 0x3248; Last = 0x324F; Width = EastAsianWidth.OfText "A"  } // 3248..324F;A     # No     [8] CIRCLED NUMBER TEN ON BLACK SQUARE..CIRCLED NUMBER EIGHTY ON BLACK SQUARE
            { Start = 0x3250; Last = 0x3250; Width = EastAsianWidth.OfText "W"  } // 3250;W           # So         PARTNERSHIP SIGN
            { Start = 0x3251; Last = 0x325F; Width = EastAsianWidth.OfText "W"  } // 3251..325F;W     # No    [15] CIRCLED NUMBER TWENTY ONE..CIRCLED NUMBER THIRTY FIVE
            { Start = 0x3260; Last = 0x327F; Width = EastAsianWidth.OfText "W"  } // 3260..327F;W     # So    [32] CIRCLED HANGUL KIYEOK..KOREAN STANDARD SYMBOL
            { Start = 0x3280; Last = 0x3289; Width = EastAsianWidth.OfText "W"  } // 3280..3289;W     # No    [10] CIRCLED IDEOGRAPH ONE..CIRCLED IDEOGRAPH TEN
            { Start = 0x328A; Last = 0x32B0; Width = EastAsianWidth.OfText "W"  } // 328A..32B0;W     # So    [39] CIRCLED IDEOGRAPH MOON..CIRCLED IDEOGRAPH NIGHT
            { Start = 0x32B1; Last = 0x32BF; Width = EastAsianWidth.OfText "W"  } // 32B1..32BF;W     # No    [15] CIRCLED NUMBER THIRTY SIX..CIRCLED NUMBER FIFTY
            { Start = 0x32C0; Last = 0x32FE; Width = EastAsianWidth.OfText "W"  } // 32C0..32FE;W     # So    [63] IDEOGRAPHIC TELEGRAPH SYMBOL FOR JANUARY..CIRCLED KATAKANA WO
            { Start = 0x3300; Last = 0x33FF; Width = EastAsianWidth.OfText "W"  } // 3300..33FF;W     # So   [256] SQUARE APAATO..SQUARE GAL
            { Start = 0x3400; Last = 0x4DB5; Width = EastAsianWidth.OfText "W"  } // 3400..4DB5;W     # Lo  [6582] CJK UNIFIED IDEOGRAPH-3400..CJK UNIFIED IDEOGRAPH-4DB5
            { Start = 0x4DB6; Last = 0x4DBF; Width = EastAsianWidth.OfText "W"  } // 4DB6..4DBF;W     # Cn    [10] <reserved-4DB6>..<reserved-4DBF>
            { Start = 0x4DC0; Last = 0x4DFF; Width = EastAsianWidth.OfText "N"  } // 4DC0..4DFF;N     # So    [64] HEXAGRAM FOR THE CREATIVE HEAVEN..HEXAGRAM FOR BEFORE COMPLETION
            { Start = 0x4E00; Last = 0x9FEF; Width = EastAsianWidth.OfText "W"  } // 4E00..9FEF;W     # Lo [20976] CJK UNIFIED IDEOGRAPH-4E00..CJK UNIFIED IDEOGRAPH-9FEF
            { Start = 0x9FF0; Last = 0x9FFF; Width = EastAsianWidth.OfText "W"  } // 9FF0..9FFF;W     # Cn    [16] <reserved-9FF0>..<reserved-9FFF>
            { Start = 0xA000; Last = 0xA014; Width = EastAsianWidth.OfText "W"  } // A000..A014;W     # Lo    [21] YI SYLLABLE IT..YI SYLLABLE E
            { Start = 0xA015; Last = 0xA015; Width = EastAsianWidth.OfText "W"  } // A015;W           # Lm         YI SYLLABLE WU
            { Start = 0xA016; Last = 0xA48C; Width = EastAsianWidth.OfText "W"  } // A016..A48C;W     # Lo  [1143] YI SYLLABLE BIT..YI SYLLABLE YYR
            { Start = 0xA490; Last = 0xA4C6; Width = EastAsianWidth.OfText "W"  } // A490..A4C6;W     # So    [55] YI RADICAL QOT..YI RADICAL KE
            { Start = 0xA4D0; Last = 0xA4F7; Width = EastAsianWidth.OfText "N"  } // A4D0..A4F7;N     # Lo    [40] LISU LETTER BA..LISU LETTER OE
            { Start = 0xA4F8; Last = 0xA4FD; Width = EastAsianWidth.OfText "N"  } // A4F8..A4FD;N     # Lm     [6] LISU LETTER TONE MYA TI..LISU LETTER TONE MYA JEU
            { Start = 0xA4FE; Last = 0xA4FF; Width = EastAsianWidth.OfText "N"  } // A4FE..A4FF;N     # Po     [2] LISU PUNCTUATION COMMA..LISU PUNCTUATION FULL STOP
            { Start = 0xA500; Last = 0xA60B; Width = EastAsianWidth.OfText "N"  } // A500..A60B;N     # Lo   [268] VAI SYLLABLE EE..VAI SYLLABLE NG
            { Start = 0xA60C; Last = 0xA60C; Width = EastAsianWidth.OfText "N"  } // A60C;N           # Lm         VAI SYLLABLE LENGTHENER
            { Start = 0xA60D; Last = 0xA60F; Width = EastAsianWidth.OfText "N"  } // A60D..A60F;N     # Po     [3] VAI COMMA..VAI QUESTION MARK
            { Start = 0xA610; Last = 0xA61F; Width = EastAsianWidth.OfText "N"  } // A610..A61F;N     # Lo    [16] VAI SYLLABLE NDOLE FA..VAI SYMBOL JONG
            { Start = 0xA620; Last = 0xA629; Width = EastAsianWidth.OfText "N"  } // A620..A629;N     # Nd    [10] VAI DIGIT ZERO..VAI DIGIT NINE
            { Start = 0xA62A; Last = 0xA62B; Width = EastAsianWidth.OfText "N"  } // A62A..A62B;N     # Lo     [2] VAI SYLLABLE NDOLE MA..VAI SYLLABLE NDOLE DO
            { Start = 0xA640; Last = 0xA66D; Width = EastAsianWidth.OfText "N"  } // A640..A66D;N     # L&    [46] CYRILLIC CAPITAL LETTER ZEMLYA..CYRILLIC SMALL LETTER DOUBLE MONOCULAR O
            { Start = 0xA66E; Last = 0xA66E; Width = EastAsianWidth.OfText "N"  } // A66E;N           # Lo         CYRILLIC LETTER MULTIOCULAR O
            { Start = 0xA66F; Last = 0xA66F; Width = EastAsianWidth.OfText "N"  } // A66F;N           # Mn         COMBINING CYRILLIC VZMET
            { Start = 0xA670; Last = 0xA672; Width = EastAsianWidth.OfText "N"  } // A670..A672;N     # Me     [3] COMBINING CYRILLIC TEN MILLIONS SIGN..COMBINING CYRILLIC THOUSAND MILLIONS SIGN
            { Start = 0xA673; Last = 0xA673; Width = EastAsianWidth.OfText "N"  } // A673;N           # Po         SLAVONIC ASTERISK
            { Start = 0xA674; Last = 0xA67D; Width = EastAsianWidth.OfText "N"  } // A674..A67D;N     # Mn    [10] COMBINING CYRILLIC LETTER UKRAINIAN IE..COMBINING CYRILLIC PAYEROK
            { Start = 0xA67E; Last = 0xA67E; Width = EastAsianWidth.OfText "N"  } // A67E;N           # Po         CYRILLIC KAVYKA
            { Start = 0xA67F; Last = 0xA67F; Width = EastAsianWidth.OfText "N"  } // A67F;N           # Lm         CYRILLIC PAYEROK
            { Start = 0xA680; Last = 0xA69B; Width = EastAsianWidth.OfText "N"  } // A680..A69B;N     # L&    [28] CYRILLIC CAPITAL LETTER DWE..CYRILLIC SMALL LETTER CROSSED O
            { Start = 0xA69C; Last = 0xA69D; Width = EastAsianWidth.OfText "N"  } // A69C..A69D;N     # Lm     [2] MODIFIER LETTER CYRILLIC HARD SIGN..MODIFIER LETTER CYRILLIC SOFT SIGN
            { Start = 0xA69E; Last = 0xA69F; Width = EastAsianWidth.OfText "N"  } // A69E..A69F;N     # Mn     [2] COMBINING CYRILLIC LETTER EF..COMBINING CYRILLIC LETTER IOTIFIED E
            { Start = 0xA6A0; Last = 0xA6E5; Width = EastAsianWidth.OfText "N"  } // A6A0..A6E5;N     # Lo    [70] BAMUM LETTER A..BAMUM LETTER KI
            { Start = 0xA6E6; Last = 0xA6EF; Width = EastAsianWidth.OfText "N"  } // A6E6..A6EF;N     # Nl    [10] BAMUM LETTER MO..BAMUM LETTER KOGHOM
            { Start = 0xA6F0; Last = 0xA6F1; Width = EastAsianWidth.OfText "N"  } // A6F0..A6F1;N     # Mn     [2] BAMUM COMBINING MARK KOQNDON..BAMUM COMBINING MARK TUKWENTIS
            { Start = 0xA6F2; Last = 0xA6F7; Width = EastAsianWidth.OfText "N"  } // A6F2..A6F7;N     # Po     [6] BAMUM NJAEMLI..BAMUM QUESTION MARK
            { Start = 0xA700; Last = 0xA716; Width = EastAsianWidth.OfText "N"  } // A700..A716;N     # Sk    [23] MODIFIER LETTER CHINESE TONE YIN PING..MODIFIER LETTER EXTRA-LOW LEFT-STEM TONE BAR
            { Start = 0xA717; Last = 0xA71F; Width = EastAsianWidth.OfText "N"  } // A717..A71F;N     # Lm     [9] MODIFIER LETTER DOT VERTICAL BAR..MODIFIER LETTER LOW INVERTED EXCLAMATION MARK
            { Start = 0xA720; Last = 0xA721; Width = EastAsianWidth.OfText "N"  } // A720..A721;N     # Sk     [2] MODIFIER LETTER STRESS AND HIGH TONE..MODIFIER LETTER STRESS AND LOW TONE
            { Start = 0xA722; Last = 0xA76F; Width = EastAsianWidth.OfText "N"  } // A722..A76F;N     # L&    [78] LATIN CAPITAL LETTER EGYPTOLOGICAL ALEF..LATIN SMALL LETTER CON
            { Start = 0xA770; Last = 0xA770; Width = EastAsianWidth.OfText "N"  } // A770;N           # Lm         MODIFIER LETTER US
            { Start = 0xA771; Last = 0xA787; Width = EastAsianWidth.OfText "N"  } // A771..A787;N     # L&    [23] LATIN SMALL LETTER DUM..LATIN SMALL LETTER INSULAR T
            { Start = 0xA788; Last = 0xA788; Width = EastAsianWidth.OfText "N"  } // A788;N           # Lm         MODIFIER LETTER LOW CIRCUMFLEX ACCENT
            { Start = 0xA789; Last = 0xA78A; Width = EastAsianWidth.OfText "N"  } // A789..A78A;N     # Sk     [2] MODIFIER LETTER COLON..MODIFIER LETTER SHORT EQUALS SIGN
            { Start = 0xA78B; Last = 0xA78E; Width = EastAsianWidth.OfText "N"  } // A78B..A78E;N     # L&     [4] LATIN CAPITAL LETTER SALTILLO..LATIN SMALL LETTER L WITH RETROFLEX HOOK AND BELT
            { Start = 0xA78F; Last = 0xA78F; Width = EastAsianWidth.OfText "N"  } // A78F;N           # Lo         LATIN LETTER SINOLOGICAL DOT
            { Start = 0xA790; Last = 0xA7B9; Width = EastAsianWidth.OfText "N"  } // A790..A7B9;N     # L&    [42] LATIN CAPITAL LETTER N WITH DESCENDER..LATIN SMALL LETTER U WITH STROKE
            { Start = 0xA7F7; Last = 0xA7F7; Width = EastAsianWidth.OfText "N"  } // A7F7;N           # Lo         LATIN EPIGRAPHIC LETTER SIDEWAYS I
            { Start = 0xA7F8; Last = 0xA7F9; Width = EastAsianWidth.OfText "N"  } // A7F8..A7F9;N     # Lm     [2] MODIFIER LETTER CAPITAL H WITH STROKE..MODIFIER LETTER SMALL LIGATURE OE
            { Start = 0xA7FA; Last = 0xA7FA; Width = EastAsianWidth.OfText "N"  } // A7FA;N           # Ll         LATIN LETTER SMALL CAPITAL TURNED M
            { Start = 0xA7FB; Last = 0xA7FF; Width = EastAsianWidth.OfText "N"  } // A7FB..A7FF;N     # Lo     [5] LATIN EPIGRAPHIC LETTER REVERSED F..LATIN EPIGRAPHIC LETTER ARCHAIC M
            { Start = 0xA800; Last = 0xA801; Width = EastAsianWidth.OfText "N"  } // A800..A801;N     # Lo     [2] SYLOTI NAGRI LETTER A..SYLOTI NAGRI LETTER I
            { Start = 0xA802; Last = 0xA802; Width = EastAsianWidth.OfText "N"  } // A802;N           # Mn         SYLOTI NAGRI SIGN DVISVARA
            { Start = 0xA803; Last = 0xA805; Width = EastAsianWidth.OfText "N"  } // A803..A805;N     # Lo     [3] SYLOTI NAGRI LETTER U..SYLOTI NAGRI LETTER O
            { Start = 0xA806; Last = 0xA806; Width = EastAsianWidth.OfText "N"  } // A806;N           # Mn         SYLOTI NAGRI SIGN HASANTA
            { Start = 0xA807; Last = 0xA80A; Width = EastAsianWidth.OfText "N"  } // A807..A80A;N     # Lo     [4] SYLOTI NAGRI LETTER KO..SYLOTI NAGRI LETTER GHO
            { Start = 0xA80B; Last = 0xA80B; Width = EastAsianWidth.OfText "N"  } // A80B;N           # Mn         SYLOTI NAGRI SIGN ANUSVARA
            { Start = 0xA80C; Last = 0xA822; Width = EastAsianWidth.OfText "N"  } // A80C..A822;N     # Lo    [23] SYLOTI NAGRI LETTER CO..SYLOTI NAGRI LETTER HO
            { Start = 0xA823; Last = 0xA824; Width = EastAsianWidth.OfText "N"  } // A823..A824;N     # Mc     [2] SYLOTI NAGRI VOWEL SIGN A..SYLOTI NAGRI VOWEL SIGN I
            { Start = 0xA825; Last = 0xA826; Width = EastAsianWidth.OfText "N"  } // A825..A826;N     # Mn     [2] SYLOTI NAGRI VOWEL SIGN U..SYLOTI NAGRI VOWEL SIGN E
            { Start = 0xA827; Last = 0xA827; Width = EastAsianWidth.OfText "N"  } // A827;N           # Mc         SYLOTI NAGRI VOWEL SIGN OO
            { Start = 0xA828; Last = 0xA82B; Width = EastAsianWidth.OfText "N"  } // A828..A82B;N     # So     [4] SYLOTI NAGRI POETRY MARK-1..SYLOTI NAGRI POETRY MARK-4
            { Start = 0xA830; Last = 0xA835; Width = EastAsianWidth.OfText "N"  } // A830..A835;N     # No     [6] NORTH INDIC FRACTION ONE QUARTER..NORTH INDIC FRACTION THREE SIXTEENTHS
            { Start = 0xA836; Last = 0xA837; Width = EastAsianWidth.OfText "N"  } // A836..A837;N     # So     [2] NORTH INDIC QUARTER MARK..NORTH INDIC PLACEHOLDER MARK
            { Start = 0xA838; Last = 0xA838; Width = EastAsianWidth.OfText "N"  } // A838;N           # Sc         NORTH INDIC RUPEE MARK
            { Start = 0xA839; Last = 0xA839; Width = EastAsianWidth.OfText "N"  } // A839;N           # So         NORTH INDIC QUANTITY MARK
            { Start = 0xA840; Last = 0xA873; Width = EastAsianWidth.OfText "N"  } // A840..A873;N     # Lo    [52] PHAGS-PA LETTER KA..PHAGS-PA LETTER CANDRABINDU
            { Start = 0xA874; Last = 0xA877; Width = EastAsianWidth.OfText "N"  } // A874..A877;N     # Po     [4] PHAGS-PA SINGLE HEAD MARK..PHAGS-PA MARK DOUBLE SHAD
            { Start = 0xA880; Last = 0xA881; Width = EastAsianWidth.OfText "N"  } // A880..A881;N     # Mc     [2] SAURASHTRA SIGN ANUSVARA..SAURASHTRA SIGN VISARGA
            { Start = 0xA882; Last = 0xA8B3; Width = EastAsianWidth.OfText "N"  } // A882..A8B3;N     # Lo    [50] SAURASHTRA LETTER A..SAURASHTRA LETTER LLA
            { Start = 0xA8B4; Last = 0xA8C3; Width = EastAsianWidth.OfText "N"  } // A8B4..A8C3;N     # Mc    [16] SAURASHTRA CONSONANT SIGN HAARU..SAURASHTRA VOWEL SIGN AU
            { Start = 0xA8C4; Last = 0xA8C5; Width = EastAsianWidth.OfText "N"  } // A8C4..A8C5;N     # Mn     [2] SAURASHTRA SIGN VIRAMA..SAURASHTRA SIGN CANDRABINDU
            { Start = 0xA8CE; Last = 0xA8CF; Width = EastAsianWidth.OfText "N"  } // A8CE..A8CF;N     # Po     [2] SAURASHTRA DANDA..SAURASHTRA DOUBLE DANDA
            { Start = 0xA8D0; Last = 0xA8D9; Width = EastAsianWidth.OfText "N"  } // A8D0..A8D9;N     # Nd    [10] SAURASHTRA DIGIT ZERO..SAURASHTRA DIGIT NINE
            { Start = 0xA8E0; Last = 0xA8F1; Width = EastAsianWidth.OfText "N"  } // A8E0..A8F1;N     # Mn    [18] COMBINING DEVANAGARI DIGIT ZERO..COMBINING DEVANAGARI SIGN AVAGRAHA
            { Start = 0xA8F2; Last = 0xA8F7; Width = EastAsianWidth.OfText "N"  } // A8F2..A8F7;N     # Lo     [6] DEVANAGARI SIGN SPACING CANDRABINDU..DEVANAGARI SIGN CANDRABINDU AVAGRAHA
            { Start = 0xA8F8; Last = 0xA8FA; Width = EastAsianWidth.OfText "N"  } // A8F8..A8FA;N     # Po     [3] DEVANAGARI SIGN PUSHPIKA..DEVANAGARI CARET
            { Start = 0xA8FB; Last = 0xA8FB; Width = EastAsianWidth.OfText "N"  } // A8FB;N           # Lo         DEVANAGARI HEADSTROKE
            { Start = 0xA8FC; Last = 0xA8FC; Width = EastAsianWidth.OfText "N"  } // A8FC;N           # Po         DEVANAGARI SIGN SIDDHAM
            { Start = 0xA8FD; Last = 0xA8FE; Width = EastAsianWidth.OfText "N"  } // A8FD..A8FE;N     # Lo     [2] DEVANAGARI JAIN OM..DEVANAGARI LETTER AY
            { Start = 0xA8FF; Last = 0xA8FF; Width = EastAsianWidth.OfText "N"  } // A8FF;N           # Mn         DEVANAGARI VOWEL SIGN AY
            { Start = 0xA900; Last = 0xA909; Width = EastAsianWidth.OfText "N"  } // A900..A909;N     # Nd    [10] KAYAH LI DIGIT ZERO..KAYAH LI DIGIT NINE
            { Start = 0xA90A; Last = 0xA925; Width = EastAsianWidth.OfText "N"  } // A90A..A925;N     # Lo    [28] KAYAH LI LETTER KA..KAYAH LI LETTER OO
            { Start = 0xA926; Last = 0xA92D; Width = EastAsianWidth.OfText "N"  } // A926..A92D;N     # Mn     [8] KAYAH LI VOWEL UE..KAYAH LI TONE CALYA PLOPHU
            { Start = 0xA92E; Last = 0xA92F; Width = EastAsianWidth.OfText "N"  } // A92E..A92F;N     # Po     [2] KAYAH LI SIGN CWI..KAYAH LI SIGN SHYA
            { Start = 0xA930; Last = 0xA946; Width = EastAsianWidth.OfText "N"  } // A930..A946;N     # Lo    [23] REJANG LETTER KA..REJANG LETTER A
            { Start = 0xA947; Last = 0xA951; Width = EastAsianWidth.OfText "N"  } // A947..A951;N     # Mn    [11] REJANG VOWEL SIGN I..REJANG CONSONANT SIGN R
            { Start = 0xA952; Last = 0xA953; Width = EastAsianWidth.OfText "N"  } // A952..A953;N     # Mc     [2] REJANG CONSONANT SIGN H..REJANG VIRAMA
            { Start = 0xA95F; Last = 0xA95F; Width = EastAsianWidth.OfText "N"  } // A95F;N           # Po         REJANG SECTION MARK
            { Start = 0xA960; Last = 0xA97C; Width = EastAsianWidth.OfText "W"  } // A960..A97C;W     # Lo    [29] HANGUL CHOSEONG TIKEUT-MIEUM..HANGUL CHOSEONG SSANGYEORINHIEUH
            { Start = 0xA980; Last = 0xA982; Width = EastAsianWidth.OfText "N"  } // A980..A982;N     # Mn     [3] JAVANESE SIGN PANYANGGA..JAVANESE SIGN LAYAR
            { Start = 0xA983; Last = 0xA983; Width = EastAsianWidth.OfText "N"  } // A983;N           # Mc         JAVANESE SIGN WIGNYAN
            { Start = 0xA984; Last = 0xA9B2; Width = EastAsianWidth.OfText "N"  } // A984..A9B2;N     # Lo    [47] JAVANESE LETTER A..JAVANESE LETTER HA
            { Start = 0xA9B3; Last = 0xA9B3; Width = EastAsianWidth.OfText "N"  } // A9B3;N           # Mn         JAVANESE SIGN CECAK TELU
            { Start = 0xA9B4; Last = 0xA9B5; Width = EastAsianWidth.OfText "N"  } // A9B4..A9B5;N     # Mc     [2] JAVANESE VOWEL SIGN TARUNG..JAVANESE VOWEL SIGN TOLONG
            { Start = 0xA9B6; Last = 0xA9B9; Width = EastAsianWidth.OfText "N"  } // A9B6..A9B9;N     # Mn     [4] JAVANESE VOWEL SIGN WULU..JAVANESE VOWEL SIGN SUKU MENDUT
            { Start = 0xA9BA; Last = 0xA9BB; Width = EastAsianWidth.OfText "N"  } // A9BA..A9BB;N     # Mc     [2] JAVANESE VOWEL SIGN TALING..JAVANESE VOWEL SIGN DIRGA MURE
            { Start = 0xA9BC; Last = 0xA9BC; Width = EastAsianWidth.OfText "N"  } // A9BC;N           # Mn         JAVANESE VOWEL SIGN PEPET
            { Start = 0xA9BD; Last = 0xA9C0; Width = EastAsianWidth.OfText "N"  } // A9BD..A9C0;N     # Mc     [4] JAVANESE CONSONANT SIGN KERET..JAVANESE PANGKON
            { Start = 0xA9C1; Last = 0xA9CD; Width = EastAsianWidth.OfText "N"  } // A9C1..A9CD;N     # Po    [13] JAVANESE LEFT RERENGGAN..JAVANESE TURNED PADA PISELEH
            { Start = 0xA9CF; Last = 0xA9CF; Width = EastAsianWidth.OfText "N"  } // A9CF;N           # Lm         JAVANESE PANGRANGKEP
            { Start = 0xA9D0; Last = 0xA9D9; Width = EastAsianWidth.OfText "N"  } // A9D0..A9D9;N     # Nd    [10] JAVANESE DIGIT ZERO..JAVANESE DIGIT NINE
            { Start = 0xA9DE; Last = 0xA9DF; Width = EastAsianWidth.OfText "N"  } // A9DE..A9DF;N     # Po     [2] JAVANESE PADA TIRTA TUMETES..JAVANESE PADA ISEN-ISEN
            { Start = 0xA9E0; Last = 0xA9E4; Width = EastAsianWidth.OfText "N"  } // A9E0..A9E4;N     # Lo     [5] MYANMAR LETTER SHAN GHA..MYANMAR LETTER SHAN BHA
            { Start = 0xA9E5; Last = 0xA9E5; Width = EastAsianWidth.OfText "N"  } // A9E5;N           # Mn         MYANMAR SIGN SHAN SAW
            { Start = 0xA9E6; Last = 0xA9E6; Width = EastAsianWidth.OfText "N"  } // A9E6;N           # Lm         MYANMAR MODIFIER LETTER SHAN REDUPLICATION
            { Start = 0xA9E7; Last = 0xA9EF; Width = EastAsianWidth.OfText "N"  } // A9E7..A9EF;N     # Lo     [9] MYANMAR LETTER TAI LAING NYA..MYANMAR LETTER TAI LAING NNA
            { Start = 0xA9F0; Last = 0xA9F9; Width = EastAsianWidth.OfText "N"  } // A9F0..A9F9;N     # Nd    [10] MYANMAR TAI LAING DIGIT ZERO..MYANMAR TAI LAING DIGIT NINE
            { Start = 0xA9FA; Last = 0xA9FE; Width = EastAsianWidth.OfText "N"  } // A9FA..A9FE;N     # Lo     [5] MYANMAR LETTER TAI LAING LLA..MYANMAR LETTER TAI LAING BHA
            { Start = 0xAA00; Last = 0xAA28; Width = EastAsianWidth.OfText "N"  } // AA00..AA28;N     # Lo    [41] CHAM LETTER A..CHAM LETTER HA
            { Start = 0xAA29; Last = 0xAA2E; Width = EastAsianWidth.OfText "N"  } // AA29..AA2E;N     # Mn     [6] CHAM VOWEL SIGN AA..CHAM VOWEL SIGN OE
            { Start = 0xAA2F; Last = 0xAA30; Width = EastAsianWidth.OfText "N"  } // AA2F..AA30;N     # Mc     [2] CHAM VOWEL SIGN O..CHAM VOWEL SIGN AI
            { Start = 0xAA31; Last = 0xAA32; Width = EastAsianWidth.OfText "N"  } // AA31..AA32;N     # Mn     [2] CHAM VOWEL SIGN AU..CHAM VOWEL SIGN UE
            { Start = 0xAA33; Last = 0xAA34; Width = EastAsianWidth.OfText "N"  } // AA33..AA34;N     # Mc     [2] CHAM CONSONANT SIGN YA..CHAM CONSONANT SIGN RA
            { Start = 0xAA35; Last = 0xAA36; Width = EastAsianWidth.OfText "N"  } // AA35..AA36;N     # Mn     [2] CHAM CONSONANT SIGN LA..CHAM CONSONANT SIGN WA
            { Start = 0xAA40; Last = 0xAA42; Width = EastAsianWidth.OfText "N"  } // AA40..AA42;N     # Lo     [3] CHAM LETTER FINAL K..CHAM LETTER FINAL NG
            { Start = 0xAA43; Last = 0xAA43; Width = EastAsianWidth.OfText "N"  } // AA43;N           # Mn         CHAM CONSONANT SIGN FINAL NG
            { Start = 0xAA44; Last = 0xAA4B; Width = EastAsianWidth.OfText "N"  } // AA44..AA4B;N     # Lo     [8] CHAM LETTER FINAL CH..CHAM LETTER FINAL SS
            { Start = 0xAA4C; Last = 0xAA4C; Width = EastAsianWidth.OfText "N"  } // AA4C;N           # Mn         CHAM CONSONANT SIGN FINAL M
            { Start = 0xAA4D; Last = 0xAA4D; Width = EastAsianWidth.OfText "N"  } // AA4D;N           # Mc         CHAM CONSONANT SIGN FINAL H
            { Start = 0xAA50; Last = 0xAA59; Width = EastAsianWidth.OfText "N"  } // AA50..AA59;N     # Nd    [10] CHAM DIGIT ZERO..CHAM DIGIT NINE
            { Start = 0xAA5C; Last = 0xAA5F; Width = EastAsianWidth.OfText "N"  } // AA5C..AA5F;N     # Po     [4] CHAM PUNCTUATION SPIRAL..CHAM PUNCTUATION TRIPLE DANDA
            { Start = 0xAA60; Last = 0xAA6F; Width = EastAsianWidth.OfText "N"  } // AA60..AA6F;N     # Lo    [16] MYANMAR LETTER KHAMTI GA..MYANMAR LETTER KHAMTI FA
            { Start = 0xAA70; Last = 0xAA70; Width = EastAsianWidth.OfText "N"  } // AA70;N           # Lm         MYANMAR MODIFIER LETTER KHAMTI REDUPLICATION
            { Start = 0xAA71; Last = 0xAA76; Width = EastAsianWidth.OfText "N"  } // AA71..AA76;N     # Lo     [6] MYANMAR LETTER KHAMTI XA..MYANMAR LOGOGRAM KHAMTI HM
            { Start = 0xAA77; Last = 0xAA79; Width = EastAsianWidth.OfText "N"  } // AA77..AA79;N     # So     [3] MYANMAR SYMBOL AITON EXCLAMATION..MYANMAR SYMBOL AITON TWO
            { Start = 0xAA7A; Last = 0xAA7A; Width = EastAsianWidth.OfText "N"  } // AA7A;N           # Lo         MYANMAR LETTER AITON RA
            { Start = 0xAA7B; Last = 0xAA7B; Width = EastAsianWidth.OfText "N"  } // AA7B;N           # Mc         MYANMAR SIGN PAO KAREN TONE
            { Start = 0xAA7C; Last = 0xAA7C; Width = EastAsianWidth.OfText "N"  } // AA7C;N           # Mn         MYANMAR SIGN TAI LAING TONE-2
            { Start = 0xAA7D; Last = 0xAA7D; Width = EastAsianWidth.OfText "N"  } // AA7D;N           # Mc         MYANMAR SIGN TAI LAING TONE-5
            { Start = 0xAA7E; Last = 0xAA7F; Width = EastAsianWidth.OfText "N"  } // AA7E..AA7F;N     # Lo     [2] MYANMAR LETTER SHWE PALAUNG CHA..MYANMAR LETTER SHWE PALAUNG SHA
            { Start = 0xAA80; Last = 0xAAAF; Width = EastAsianWidth.OfText "N"  } // AA80..AAAF;N     # Lo    [48] TAI VIET LETTER LOW KO..TAI VIET LETTER HIGH O
            { Start = 0xAAB0; Last = 0xAAB0; Width = EastAsianWidth.OfText "N"  } // AAB0;N           # Mn         TAI VIET MAI KANG
            { Start = 0xAAB1; Last = 0xAAB1; Width = EastAsianWidth.OfText "N"  } // AAB1;N           # Lo         TAI VIET VOWEL AA
            { Start = 0xAAB2; Last = 0xAAB4; Width = EastAsianWidth.OfText "N"  } // AAB2..AAB4;N     # Mn     [3] TAI VIET VOWEL I..TAI VIET VOWEL U
            { Start = 0xAAB5; Last = 0xAAB6; Width = EastAsianWidth.OfText "N"  } // AAB5..AAB6;N     # Lo     [2] TAI VIET VOWEL E..TAI VIET VOWEL O
            { Start = 0xAAB7; Last = 0xAAB8; Width = EastAsianWidth.OfText "N"  } // AAB7..AAB8;N     # Mn     [2] TAI VIET MAI KHIT..TAI VIET VOWEL IA
            { Start = 0xAAB9; Last = 0xAABD; Width = EastAsianWidth.OfText "N"  } // AAB9..AABD;N     # Lo     [5] TAI VIET VOWEL UEA..TAI VIET VOWEL AN
            { Start = 0xAABE; Last = 0xAABF; Width = EastAsianWidth.OfText "N"  } // AABE..AABF;N     # Mn     [2] TAI VIET VOWEL AM..TAI VIET TONE MAI EK
            { Start = 0xAAC0; Last = 0xAAC0; Width = EastAsianWidth.OfText "N"  } // AAC0;N           # Lo         TAI VIET TONE MAI NUENG
            { Start = 0xAAC1; Last = 0xAAC1; Width = EastAsianWidth.OfText "N"  } // AAC1;N           # Mn         TAI VIET TONE MAI THO
            { Start = 0xAAC2; Last = 0xAAC2; Width = EastAsianWidth.OfText "N"  } // AAC2;N           # Lo         TAI VIET TONE MAI SONG
            { Start = 0xAADB; Last = 0xAADC; Width = EastAsianWidth.OfText "N"  } // AADB..AADC;N     # Lo     [2] TAI VIET SYMBOL KON..TAI VIET SYMBOL NUENG
            { Start = 0xAADD; Last = 0xAADD; Width = EastAsianWidth.OfText "N"  } // AADD;N           # Lm         TAI VIET SYMBOL SAM
            { Start = 0xAADE; Last = 0xAADF; Width = EastAsianWidth.OfText "N"  } // AADE..AADF;N     # Po     [2] TAI VIET SYMBOL HO HOI..TAI VIET SYMBOL KOI KOI
            { Start = 0xAAE0; Last = 0xAAEA; Width = EastAsianWidth.OfText "N"  } // AAE0..AAEA;N     # Lo    [11] MEETEI MAYEK LETTER E..MEETEI MAYEK LETTER SSA
            { Start = 0xAAEB; Last = 0xAAEB; Width = EastAsianWidth.OfText "N"  } // AAEB;N           # Mc         MEETEI MAYEK VOWEL SIGN II
            { Start = 0xAAEC; Last = 0xAAED; Width = EastAsianWidth.OfText "N"  } // AAEC..AAED;N     # Mn     [2] MEETEI MAYEK VOWEL SIGN UU..MEETEI MAYEK VOWEL SIGN AAI
            { Start = 0xAAEE; Last = 0xAAEF; Width = EastAsianWidth.OfText "N"  } // AAEE..AAEF;N     # Mc     [2] MEETEI MAYEK VOWEL SIGN AU..MEETEI MAYEK VOWEL SIGN AAU
            { Start = 0xAAF0; Last = 0xAAF1; Width = EastAsianWidth.OfText "N"  } // AAF0..AAF1;N     # Po     [2] MEETEI MAYEK CHEIKHAN..MEETEI MAYEK AHANG KHUDAM
            { Start = 0xAAF2; Last = 0xAAF2; Width = EastAsianWidth.OfText "N"  } // AAF2;N           # Lo         MEETEI MAYEK ANJI
            { Start = 0xAAF3; Last = 0xAAF4; Width = EastAsianWidth.OfText "N"  } // AAF3..AAF4;N     # Lm     [2] MEETEI MAYEK SYLLABLE REPETITION MARK..MEETEI MAYEK WORD REPETITION MARK
            { Start = 0xAAF5; Last = 0xAAF5; Width = EastAsianWidth.OfText "N"  } // AAF5;N           # Mc         MEETEI MAYEK VOWEL SIGN VISARGA
            { Start = 0xAAF6; Last = 0xAAF6; Width = EastAsianWidth.OfText "N"  } // AAF6;N           # Mn         MEETEI MAYEK VIRAMA
            { Start = 0xAB01; Last = 0xAB06; Width = EastAsianWidth.OfText "N"  } // AB01..AB06;N     # Lo     [6] ETHIOPIC SYLLABLE TTHU..ETHIOPIC SYLLABLE TTHO
            { Start = 0xAB09; Last = 0xAB0E; Width = EastAsianWidth.OfText "N"  } // AB09..AB0E;N     # Lo     [6] ETHIOPIC SYLLABLE DDHU..ETHIOPIC SYLLABLE DDHO
            { Start = 0xAB11; Last = 0xAB16; Width = EastAsianWidth.OfText "N"  } // AB11..AB16;N     # Lo     [6] ETHIOPIC SYLLABLE DZU..ETHIOPIC SYLLABLE DZO
            { Start = 0xAB20; Last = 0xAB26; Width = EastAsianWidth.OfText "N"  } // AB20..AB26;N     # Lo     [7] ETHIOPIC SYLLABLE CCHHA..ETHIOPIC SYLLABLE CCHHO
            { Start = 0xAB28; Last = 0xAB2E; Width = EastAsianWidth.OfText "N"  } // AB28..AB2E;N     # Lo     [7] ETHIOPIC SYLLABLE BBA..ETHIOPIC SYLLABLE BBO
            { Start = 0xAB30; Last = 0xAB5A; Width = EastAsianWidth.OfText "N"  } // AB30..AB5A;N     # Ll    [43] LATIN SMALL LETTER BARRED ALPHA..LATIN SMALL LETTER Y WITH SHORT RIGHT LEG
            { Start = 0xAB5B; Last = 0xAB5B; Width = EastAsianWidth.OfText "N"  } // AB5B;N           # Sk         MODIFIER BREVE WITH INVERTED BREVE
            { Start = 0xAB5C; Last = 0xAB5F; Width = EastAsianWidth.OfText "N"  } // AB5C..AB5F;N     # Lm     [4] MODIFIER LETTER SMALL HENG..MODIFIER LETTER SMALL U WITH LEFT HOOK
            { Start = 0xAB60; Last = 0xAB65; Width = EastAsianWidth.OfText "N"  } // AB60..AB65;N     # Ll     [6] LATIN SMALL LETTER SAKHA YAT..GREEK LETTER SMALL CAPITAL OMEGA
            { Start = 0xAB70; Last = 0xABBF; Width = EastAsianWidth.OfText "N"  } // AB70..ABBF;N     # Ll    [80] CHEROKEE SMALL LETTER A..CHEROKEE SMALL LETTER YA
            { Start = 0xABC0; Last = 0xABE2; Width = EastAsianWidth.OfText "N"  } // ABC0..ABE2;N     # Lo    [35] MEETEI MAYEK LETTER KOK..MEETEI MAYEK LETTER I LONSUM
            { Start = 0xABE3; Last = 0xABE4; Width = EastAsianWidth.OfText "N"  } // ABE3..ABE4;N     # Mc     [2] MEETEI MAYEK VOWEL SIGN ONAP..MEETEI MAYEK VOWEL SIGN INAP
            { Start = 0xABE5; Last = 0xABE5; Width = EastAsianWidth.OfText "N"  } // ABE5;N           # Mn         MEETEI MAYEK VOWEL SIGN ANAP
            { Start = 0xABE6; Last = 0xABE7; Width = EastAsianWidth.OfText "N"  } // ABE6..ABE7;N     # Mc     [2] MEETEI MAYEK VOWEL SIGN YENAP..MEETEI MAYEK VOWEL SIGN SOUNAP
            { Start = 0xABE8; Last = 0xABE8; Width = EastAsianWidth.OfText "N"  } // ABE8;N           # Mn         MEETEI MAYEK VOWEL SIGN UNAP
            { Start = 0xABE9; Last = 0xABEA; Width = EastAsianWidth.OfText "N"  } // ABE9..ABEA;N     # Mc     [2] MEETEI MAYEK VOWEL SIGN CHEINAP..MEETEI MAYEK VOWEL SIGN NUNG
            { Start = 0xABEB; Last = 0xABEB; Width = EastAsianWidth.OfText "N"  } // ABEB;N           # Po         MEETEI MAYEK CHEIKHEI
            { Start = 0xABEC; Last = 0xABEC; Width = EastAsianWidth.OfText "N"  } // ABEC;N           # Mc         MEETEI MAYEK LUM IYEK
            { Start = 0xABED; Last = 0xABED; Width = EastAsianWidth.OfText "N"  } // ABED;N           # Mn         MEETEI MAYEK APUN IYEK
            { Start = 0xABF0; Last = 0xABF9; Width = EastAsianWidth.OfText "N"  } // ABF0..ABF9;N     # Nd    [10] MEETEI MAYEK DIGIT ZERO..MEETEI MAYEK DIGIT NINE
            { Start = 0xAC00; Last = 0xD7A3; Width = EastAsianWidth.OfText "W"  } // AC00..D7A3;W     # Lo [11172] HANGUL SYLLABLE GA..HANGUL SYLLABLE HIH
            { Start = 0xD7B0; Last = 0xD7C6; Width = EastAsianWidth.OfText "N"  } // D7B0..D7C6;N     # Lo    [23] HANGUL JUNGSEONG O-YEO..HANGUL JUNGSEONG ARAEA-E
            { Start = 0xD7CB; Last = 0xD7FB; Width = EastAsianWidth.OfText "N"  } // D7CB..D7FB;N     # Lo    [49] HANGUL JONGSEONG NIEUN-RIEUL..HANGUL JONGSEONG PHIEUPH-THIEUTH
            { Start = 0xD800; Last = 0xDB7F; Width = EastAsianWidth.OfText "N"  } // D800..DB7F;N     # Cs   [896] <surrogate-D800>..<surrogate-DB7F>
            { Start = 0xDB80; Last = 0xDBFF; Width = EastAsianWidth.OfText "N"  } // DB80..DBFF;N     # Cs   [128] <surrogate-DB80>..<surrogate-DBFF>
            { Start = 0xDC00; Last = 0xDFFF; Width = EastAsianWidth.OfText "N"  } // DC00..DFFF;N     # Cs  [1024] <surrogate-DC00>..<surrogate-DFFF>
            { Start = 0xE000; Last = 0xF8FF; Width = EastAsianWidth.OfText "A"  } // E000..F8FF;A     # Co  [6400] <private-use-E000>..<private-use-F8FF>
            { Start = 0xF900; Last = 0xFA6D; Width = EastAsianWidth.OfText "W"  } // F900..FA6D;W     # Lo   [366] CJK COMPATIBILITY IDEOGRAPH-F900..CJK COMPATIBILITY IDEOGRAPH-FA6D
            { Start = 0xFA6E; Last = 0xFA6F; Width = EastAsianWidth.OfText "W"  } // FA6E..FA6F;W     # Cn     [2] <reserved-FA6E>..<reserved-FA6F>
            { Start = 0xFA70; Last = 0xFAD9; Width = EastAsianWidth.OfText "W"  } // FA70..FAD9;W     # Lo   [106] CJK COMPATIBILITY IDEOGRAPH-FA70..CJK COMPATIBILITY IDEOGRAPH-FAD9
            { Start = 0xFADA; Last = 0xFAFF; Width = EastAsianWidth.OfText "W"  } // FADA..FAFF;W     # Cn    [38] <reserved-FADA>..<reserved-FAFF>
            { Start = 0xFB00; Last = 0xFB06; Width = EastAsianWidth.OfText "N"  } // FB00..FB06;N     # Ll     [7] LATIN SMALL LIGATURE FF..LATIN SMALL LIGATURE ST
            { Start = 0xFB13; Last = 0xFB17; Width = EastAsianWidth.OfText "N"  } // FB13..FB17;N     # Ll     [5] ARMENIAN SMALL LIGATURE MEN NOW..ARMENIAN SMALL LIGATURE MEN XEH
            { Start = 0xFB1D; Last = 0xFB1D; Width = EastAsianWidth.OfText "N"  } // FB1D;N           # Lo         HEBREW LETTER YOD WITH HIRIQ
            { Start = 0xFB1E; Last = 0xFB1E; Width = EastAsianWidth.OfText "N"  } // FB1E;N           # Mn         HEBREW POINT JUDEO-SPANISH VARIKA
            { Start = 0xFB1F; Last = 0xFB28; Width = EastAsianWidth.OfText "N"  } // FB1F..FB28;N     # Lo    [10] HEBREW LIGATURE YIDDISH YOD YOD PATAH..HEBREW LETTER WIDE TAV
            { Start = 0xFB29; Last = 0xFB29; Width = EastAsianWidth.OfText "N"  } // FB29;N           # Sm         HEBREW LETTER ALTERNATIVE PLUS SIGN
            { Start = 0xFB2A; Last = 0xFB36; Width = EastAsianWidth.OfText "N"  } // FB2A..FB36;N     # Lo    [13] HEBREW LETTER SHIN WITH SHIN DOT..HEBREW LETTER ZAYIN WITH DAGESH
            { Start = 0xFB38; Last = 0xFB3C; Width = EastAsianWidth.OfText "N"  } // FB38..FB3C;N     # Lo     [5] HEBREW LETTER TET WITH DAGESH..HEBREW LETTER LAMED WITH DAGESH
            { Start = 0xFB3E; Last = 0xFB3E; Width = EastAsianWidth.OfText "N"  } // FB3E;N           # Lo         HEBREW LETTER MEM WITH DAGESH
            { Start = 0xFB40; Last = 0xFB41; Width = EastAsianWidth.OfText "N"  } // FB40..FB41;N     # Lo     [2] HEBREW LETTER NUN WITH DAGESH..HEBREW LETTER SAMEKH WITH DAGESH
            { Start = 0xFB43; Last = 0xFB44; Width = EastAsianWidth.OfText "N"  } // FB43..FB44;N     # Lo     [2] HEBREW LETTER FINAL PE WITH DAGESH..HEBREW LETTER PE WITH DAGESH
            { Start = 0xFB46; Last = 0xFB4F; Width = EastAsianWidth.OfText "N"  } // FB46..FB4F;N     # Lo    [10] HEBREW LETTER TSADI WITH DAGESH..HEBREW LIGATURE ALEF LAMED
            { Start = 0xFB50; Last = 0xFBB1; Width = EastAsianWidth.OfText "N"  } // FB50..FBB1;N     # Lo    [98] ARABIC LETTER ALEF WASLA ISOLATED FORM..ARABIC LETTER YEH BARREE WITH HAMZA ABOVE FINAL FORM
            { Start = 0xFBB2; Last = 0xFBC1; Width = EastAsianWidth.OfText "N"  } // FBB2..FBC1;N     # Sk    [16] ARABIC SYMBOL DOT ABOVE..ARABIC SYMBOL SMALL TAH BELOW
            { Start = 0xFBD3; Last = 0xFD3D; Width = EastAsianWidth.OfText "N"  } // FBD3..FD3D;N     # Lo   [363] ARABIC LETTER NG ISOLATED FORM..ARABIC LIGATURE ALEF WITH FATHATAN ISOLATED FORM
            { Start = 0xFD3E; Last = 0xFD3E; Width = EastAsianWidth.OfText "N"  } // FD3E;N           # Pe         ORNATE LEFT PARENTHESIS
            { Start = 0xFD3F; Last = 0xFD3F; Width = EastAsianWidth.OfText "N"  } // FD3F;N           # Ps         ORNATE RIGHT PARENTHESIS
            { Start = 0xFD50; Last = 0xFD8F; Width = EastAsianWidth.OfText "N"  } // FD50..FD8F;N     # Lo    [64] ARABIC LIGATURE TEH WITH JEEM WITH MEEM INITIAL FORM..ARABIC LIGATURE MEEM WITH KHAH WITH MEEM INITIAL FORM
            { Start = 0xFD92; Last = 0xFDC7; Width = EastAsianWidth.OfText "N"  } // FD92..FDC7;N     # Lo    [54] ARABIC LIGATURE MEEM WITH JEEM WITH KHAH INITIAL FORM..ARABIC LIGATURE NOON WITH JEEM WITH YEH FINAL FORM
            { Start = 0xFDF0; Last = 0xFDFB; Width = EastAsianWidth.OfText "N"  } // FDF0..FDFB;N     # Lo    [12] ARABIC LIGATURE SALLA USED AS KORANIC STOP SIGN ISOLATED FORM..ARABIC LIGATURE JALLAJALALOUHOU
            { Start = 0xFDFC; Last = 0xFDFC; Width = EastAsianWidth.OfText "N"  } // FDFC;N           # Sc         RIAL SIGN
            { Start = 0xFDFD; Last = 0xFDFD; Width = EastAsianWidth.OfText "N"  } // FDFD;N           # So         ARABIC LIGATURE BISMILLAH AR-RAHMAN AR-RAHEEM
            { Start = 0xFE00; Last = 0xFE0F; Width = EastAsianWidth.OfText "A"  } // FE00..FE0F;A     # Mn    [16] VARIATION SELECTOR-1..VARIATION SELECTOR-16
            { Start = 0xFE10; Last = 0xFE16; Width = EastAsianWidth.OfText "W"  } // FE10..FE16;W     # Po     [7] PRESENTATION FORM FOR VERTICAL COMMA..PRESENTATION FORM FOR VERTICAL QUESTION MARK
            { Start = 0xFE17; Last = 0xFE17; Width = EastAsianWidth.OfText "W"  } // FE17;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT WHITE LENTICULAR BRACKET
            { Start = 0xFE18; Last = 0xFE18; Width = EastAsianWidth.OfText "W"  } // FE18;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT WHITE LENTICULAR BRAKCET
            { Start = 0xFE19; Last = 0xFE19; Width = EastAsianWidth.OfText "W"  } // FE19;W           # Po         PRESENTATION FORM FOR VERTICAL HORIZONTAL ELLIPSIS
            { Start = 0xFE20; Last = 0xFE2F; Width = EastAsianWidth.OfText "N"  } // FE20..FE2F;N     # Mn    [16] COMBINING LIGATURE LEFT HALF..COMBINING CYRILLIC TITLO RIGHT HALF
            { Start = 0xFE30; Last = 0xFE30; Width = EastAsianWidth.OfText "W"  } // FE30;W           # Po         PRESENTATION FORM FOR VERTICAL TWO DOT LEADER
            { Start = 0xFE31; Last = 0xFE32; Width = EastAsianWidth.OfText "W"  } // FE31..FE32;W     # Pd     [2] PRESENTATION FORM FOR VERTICAL EM DASH..PRESENTATION FORM FOR VERTICAL EN DASH
            { Start = 0xFE33; Last = 0xFE34; Width = EastAsianWidth.OfText "W"  } // FE33..FE34;W     # Pc     [2] PRESENTATION FORM FOR VERTICAL LOW LINE..PRESENTATION FORM FOR VERTICAL WAVY LOW LINE
            { Start = 0xFE35; Last = 0xFE35; Width = EastAsianWidth.OfText "W"  } // FE35;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT PARENTHESIS
            { Start = 0xFE36; Last = 0xFE36; Width = EastAsianWidth.OfText "W"  } // FE36;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT PARENTHESIS
            { Start = 0xFE37; Last = 0xFE37; Width = EastAsianWidth.OfText "W"  } // FE37;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT CURLY BRACKET
            { Start = 0xFE38; Last = 0xFE38; Width = EastAsianWidth.OfText "W"  } // FE38;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT CURLY BRACKET
            { Start = 0xFE39; Last = 0xFE39; Width = EastAsianWidth.OfText "W"  } // FE39;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT TORTOISE SHELL BRACKET
            { Start = 0xFE3A; Last = 0xFE3A; Width = EastAsianWidth.OfText "W"  } // FE3A;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT TORTOISE SHELL BRACKET
            { Start = 0xFE3B; Last = 0xFE3B; Width = EastAsianWidth.OfText "W"  } // FE3B;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT BLACK LENTICULAR BRACKET
            { Start = 0xFE3C; Last = 0xFE3C; Width = EastAsianWidth.OfText "W"  } // FE3C;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT BLACK LENTICULAR BRACKET
            { Start = 0xFE3D; Last = 0xFE3D; Width = EastAsianWidth.OfText "W"  } // FE3D;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT DOUBLE ANGLE BRACKET
            { Start = 0xFE3E; Last = 0xFE3E; Width = EastAsianWidth.OfText "W"  } // FE3E;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT DOUBLE ANGLE BRACKET
            { Start = 0xFE3F; Last = 0xFE3F; Width = EastAsianWidth.OfText "W"  } // FE3F;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT ANGLE BRACKET
            { Start = 0xFE40; Last = 0xFE40; Width = EastAsianWidth.OfText "W"  } // FE40;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT ANGLE BRACKET
            { Start = 0xFE41; Last = 0xFE41; Width = EastAsianWidth.OfText "W"  } // FE41;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT CORNER BRACKET
            { Start = 0xFE42; Last = 0xFE42; Width = EastAsianWidth.OfText "W"  } // FE42;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT CORNER BRACKET
            { Start = 0xFE43; Last = 0xFE43; Width = EastAsianWidth.OfText "W"  } // FE43;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT WHITE CORNER BRACKET
            { Start = 0xFE44; Last = 0xFE44; Width = EastAsianWidth.OfText "W"  } // FE44;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT WHITE CORNER BRACKET
            { Start = 0xFE45; Last = 0xFE46; Width = EastAsianWidth.OfText "W"  } // FE45..FE46;W     # Po     [2] SESAME DOT..WHITE SESAME DOT
            { Start = 0xFE47; Last = 0xFE47; Width = EastAsianWidth.OfText "W"  } // FE47;W           # Ps         PRESENTATION FORM FOR VERTICAL LEFT SQUARE BRACKET
            { Start = 0xFE48; Last = 0xFE48; Width = EastAsianWidth.OfText "W"  } // FE48;W           # Pe         PRESENTATION FORM FOR VERTICAL RIGHT SQUARE BRACKET
            { Start = 0xFE49; Last = 0xFE4C; Width = EastAsianWidth.OfText "W"  } // FE49..FE4C;W     # Po     [4] DASHED OVERLINE..DOUBLE WAVY OVERLINE
            { Start = 0xFE4D; Last = 0xFE4F; Width = EastAsianWidth.OfText "W"  } // FE4D..FE4F;W     # Pc     [3] DASHED LOW LINE..WAVY LOW LINE
            { Start = 0xFE50; Last = 0xFE52; Width = EastAsianWidth.OfText "W"  } // FE50..FE52;W     # Po     [3] SMALL COMMA..SMALL FULL STOP
            { Start = 0xFE54; Last = 0xFE57; Width = EastAsianWidth.OfText "W"  } // FE54..FE57;W     # Po     [4] SMALL SEMICOLON..SMALL EXCLAMATION MARK
            { Start = 0xFE58; Last = 0xFE58; Width = EastAsianWidth.OfText "W"  } // FE58;W           # Pd         SMALL EM DASH
            { Start = 0xFE59; Last = 0xFE59; Width = EastAsianWidth.OfText "W"  } // FE59;W           # Ps         SMALL LEFT PARENTHESIS
            { Start = 0xFE5A; Last = 0xFE5A; Width = EastAsianWidth.OfText "W"  } // FE5A;W           # Pe         SMALL RIGHT PARENTHESIS
            { Start = 0xFE5B; Last = 0xFE5B; Width = EastAsianWidth.OfText "W"  } // FE5B;W           # Ps         SMALL LEFT CURLY BRACKET
            { Start = 0xFE5C; Last = 0xFE5C; Width = EastAsianWidth.OfText "W"  } // FE5C;W           # Pe         SMALL RIGHT CURLY BRACKET
            { Start = 0xFE5D; Last = 0xFE5D; Width = EastAsianWidth.OfText "W"  } // FE5D;W           # Ps         SMALL LEFT TORTOISE SHELL BRACKET
            { Start = 0xFE5E; Last = 0xFE5E; Width = EastAsianWidth.OfText "W"  } // FE5E;W           # Pe         SMALL RIGHT TORTOISE SHELL BRACKET
            { Start = 0xFE5F; Last = 0xFE61; Width = EastAsianWidth.OfText "W"  } // FE5F..FE61;W     # Po     [3] SMALL NUMBER SIGN..SMALL ASTERISK
            { Start = 0xFE62; Last = 0xFE62; Width = EastAsianWidth.OfText "W"  } // FE62;W           # Sm         SMALL PLUS SIGN
            { Start = 0xFE63; Last = 0xFE63; Width = EastAsianWidth.OfText "W"  } // FE63;W           # Pd         SMALL HYPHEN-MINUS
            { Start = 0xFE64; Last = 0xFE66; Width = EastAsianWidth.OfText "W"  } // FE64..FE66;W     # Sm     [3] SMALL LESS-THAN SIGN..SMALL EQUALS SIGN
            { Start = 0xFE68; Last = 0xFE68; Width = EastAsianWidth.OfText "W"  } // FE68;W           # Po         SMALL REVERSE SOLIDUS
            { Start = 0xFE69; Last = 0xFE69; Width = EastAsianWidth.OfText "W"  } // FE69;W           # Sc         SMALL DOLLAR SIGN
            { Start = 0xFE6A; Last = 0xFE6B; Width = EastAsianWidth.OfText "W"  } // FE6A..FE6B;W     # Po     [2] SMALL PERCENT SIGN..SMALL COMMERCIAL AT
            { Start = 0xFE70; Last = 0xFE74; Width = EastAsianWidth.OfText "N"  } // FE70..FE74;N     # Lo     [5] ARABIC FATHATAN ISOLATED FORM..ARABIC KASRATAN ISOLATED FORM
            { Start = 0xFE76; Last = 0xFEFC; Width = EastAsianWidth.OfText "N"  } // FE76..FEFC;N     # Lo   [135] ARABIC FATHA ISOLATED FORM..ARABIC LIGATURE LAM WITH ALEF FINAL FORM
            { Start = 0xFEFF; Last = 0xFEFF; Width = EastAsianWidth.OfText "N"  } // FEFF;N           # Cf         ZERO WIDTH NO-BREAK SPACE
            { Start = 0xFF01; Last = 0xFF03; Width = EastAsianWidth.OfText "F"  } // FF01..FF03;F     # Po     [3] FULLWIDTH EXCLAMATION MARK..FULLWIDTH NUMBER SIGN
            { Start = 0xFF04; Last = 0xFF04; Width = EastAsianWidth.OfText "F"  } // FF04;F           # Sc         FULLWIDTH DOLLAR SIGN
            { Start = 0xFF05; Last = 0xFF07; Width = EastAsianWidth.OfText "F"  } // FF05..FF07;F     # Po     [3] FULLWIDTH PERCENT SIGN..FULLWIDTH APOSTROPHE
            { Start = 0xFF08; Last = 0xFF08; Width = EastAsianWidth.OfText "F"  } // FF08;F           # Ps         FULLWIDTH LEFT PARENTHESIS
            { Start = 0xFF09; Last = 0xFF09; Width = EastAsianWidth.OfText "F"  } // FF09;F           # Pe         FULLWIDTH RIGHT PARENTHESIS
            { Start = 0xFF0A; Last = 0xFF0A; Width = EastAsianWidth.OfText "F"  } // FF0A;F           # Po         FULLWIDTH ASTERISK
            { Start = 0xFF0B; Last = 0xFF0B; Width = EastAsianWidth.OfText "F"  } // FF0B;F           # Sm         FULLWIDTH PLUS SIGN
            { Start = 0xFF0C; Last = 0xFF0C; Width = EastAsianWidth.OfText "F"  } // FF0C;F           # Po         FULLWIDTH COMMA
            { Start = 0xFF0D; Last = 0xFF0D; Width = EastAsianWidth.OfText "F"  } // FF0D;F           # Pd         FULLWIDTH HYPHEN-MINUS
            { Start = 0xFF0E; Last = 0xFF0F; Width = EastAsianWidth.OfText "F"  } // FF0E..FF0F;F     # Po     [2] FULLWIDTH FULL STOP..FULLWIDTH SOLIDUS
            { Start = 0xFF10; Last = 0xFF19; Width = EastAsianWidth.OfText "F"  } // FF10..FF19;F     # Nd    [10] FULLWIDTH DIGIT ZERO..FULLWIDTH DIGIT NINE
            { Start = 0xFF1A; Last = 0xFF1B; Width = EastAsianWidth.OfText "F"  } // FF1A..FF1B;F     # Po     [2] FULLWIDTH COLON..FULLWIDTH SEMICOLON
            { Start = 0xFF1C; Last = 0xFF1E; Width = EastAsianWidth.OfText "F"  } // FF1C..FF1E;F     # Sm     [3] FULLWIDTH LESS-THAN SIGN..FULLWIDTH GREATER-THAN SIGN
            { Start = 0xFF1F; Last = 0xFF20; Width = EastAsianWidth.OfText "F"  } // FF1F..FF20;F     # Po     [2] FULLWIDTH QUESTION MARK..FULLWIDTH COMMERCIAL AT
            { Start = 0xFF21; Last = 0xFF3A; Width = EastAsianWidth.OfText "F"  } // FF21..FF3A;F     # Lu    [26] FULLWIDTH LATIN CAPITAL LETTER A..FULLWIDTH LATIN CAPITAL LETTER Z
            { Start = 0xFF3B; Last = 0xFF3B; Width = EastAsianWidth.OfText "F"  } // FF3B;F           # Ps         FULLWIDTH LEFT SQUARE BRACKET
            { Start = 0xFF3C; Last = 0xFF3C; Width = EastAsianWidth.OfText "F"  } // FF3C;F           # Po         FULLWIDTH REVERSE SOLIDUS
            { Start = 0xFF3D; Last = 0xFF3D; Width = EastAsianWidth.OfText "F"  } // FF3D;F           # Pe         FULLWIDTH RIGHT SQUARE BRACKET
            { Start = 0xFF3E; Last = 0xFF3E; Width = EastAsianWidth.OfText "F"  } // FF3E;F           # Sk         FULLWIDTH CIRCUMFLEX ACCENT
            { Start = 0xFF3F; Last = 0xFF3F; Width = EastAsianWidth.OfText "F"  } // FF3F;F           # Pc         FULLWIDTH LOW LINE
            { Start = 0xFF40; Last = 0xFF40; Width = EastAsianWidth.OfText "F"  } // FF40;F           # Sk         FULLWIDTH GRAVE ACCENT
            { Start = 0xFF41; Last = 0xFF5A; Width = EastAsianWidth.OfText "F"  } // FF41..FF5A;F     # Ll    [26] FULLWIDTH LATIN SMALL LETTER A..FULLWIDTH LATIN SMALL LETTER Z
            { Start = 0xFF5B; Last = 0xFF5B; Width = EastAsianWidth.OfText "F"  } // FF5B;F           # Ps         FULLWIDTH LEFT CURLY BRACKET
            { Start = 0xFF5C; Last = 0xFF5C; Width = EastAsianWidth.OfText "F"  } // FF5C;F           # Sm         FULLWIDTH VERTICAL LINE
            { Start = 0xFF5D; Last = 0xFF5D; Width = EastAsianWidth.OfText "F"  } // FF5D;F           # Pe         FULLWIDTH RIGHT CURLY BRACKET
            { Start = 0xFF5E; Last = 0xFF5E; Width = EastAsianWidth.OfText "F"  } // FF5E;F           # Sm         FULLWIDTH TILDE
            { Start = 0xFF5F; Last = 0xFF5F; Width = EastAsianWidth.OfText "F"  } // FF5F;F           # Ps         FULLWIDTH LEFT WHITE PARENTHESIS
            { Start = 0xFF60; Last = 0xFF60; Width = EastAsianWidth.OfText "F"  } // FF60;F           # Pe         FULLWIDTH RIGHT WHITE PARENTHESIS
            { Start = 0xFF61; Last = 0xFF61; Width = EastAsianWidth.OfText "H"  } // FF61;H           # Po         HALFWIDTH IDEOGRAPHIC FULL STOP
            { Start = 0xFF62; Last = 0xFF62; Width = EastAsianWidth.OfText "H"  } // FF62;H           # Ps         HALFWIDTH LEFT CORNER BRACKET
            { Start = 0xFF63; Last = 0xFF63; Width = EastAsianWidth.OfText "H"  } // FF63;H           # Pe         HALFWIDTH RIGHT CORNER BRACKET
            { Start = 0xFF64; Last = 0xFF65; Width = EastAsianWidth.OfText "H"  } // FF64..FF65;H     # Po     [2] HALFWIDTH IDEOGRAPHIC COMMA..HALFWIDTH KATAKANA MIDDLE DOT
            { Start = 0xFF66; Last = 0xFF6F; Width = EastAsianWidth.OfText "H"  } // FF66..FF6F;H     # Lo    [10] HALFWIDTH KATAKANA LETTER WO..HALFWIDTH KATAKANA LETTER SMALL TU
            { Start = 0xFF70; Last = 0xFF70; Width = EastAsianWidth.OfText "H"  } // FF70;H           # Lm         HALFWIDTH KATAKANA-HIRAGANA PROLONGED SOUND MARK
            { Start = 0xFF71; Last = 0xFF9D; Width = EastAsianWidth.OfText "H"  } // FF71..FF9D;H     # Lo    [45] HALFWIDTH KATAKANA LETTER A..HALFWIDTH KATAKANA LETTER N
            { Start = 0xFF9E; Last = 0xFF9F; Width = EastAsianWidth.OfText "H"  } // FF9E..FF9F;H     # Lm     [2] HALFWIDTH KATAKANA VOICED SOUND MARK..HALFWIDTH KATAKANA SEMI-VOICED SOUND MARK
            { Start = 0xFFA0; Last = 0xFFBE; Width = EastAsianWidth.OfText "H"  } // FFA0..FFBE;H     # Lo    [31] HALFWIDTH HANGUL FILLER..HALFWIDTH HANGUL LETTER HIEUH
            { Start = 0xFFC2; Last = 0xFFC7; Width = EastAsianWidth.OfText "H"  } // FFC2..FFC7;H     # Lo     [6] HALFWIDTH HANGUL LETTER A..HALFWIDTH HANGUL LETTER E
            { Start = 0xFFCA; Last = 0xFFCF; Width = EastAsianWidth.OfText "H"  } // FFCA..FFCF;H     # Lo     [6] HALFWIDTH HANGUL LETTER YEO..HALFWIDTH HANGUL LETTER OE
            { Start = 0xFFD2; Last = 0xFFD7; Width = EastAsianWidth.OfText "H"  } // FFD2..FFD7;H     # Lo     [6] HALFWIDTH HANGUL LETTER YO..HALFWIDTH HANGUL LETTER YU
            { Start = 0xFFDA; Last = 0xFFDC; Width = EastAsianWidth.OfText "H"  } // FFDA..FFDC;H     # Lo     [3] HALFWIDTH HANGUL LETTER EU..HALFWIDTH HANGUL LETTER I
            { Start = 0xFFE0; Last = 0xFFE1; Width = EastAsianWidth.OfText "F"  } // FFE0..FFE1;F     # Sc     [2] FULLWIDTH CENT SIGN..FULLWIDTH POUND SIGN
            { Start = 0xFFE2; Last = 0xFFE2; Width = EastAsianWidth.OfText "F"  } // FFE2;F           # Sm         FULLWIDTH NOT SIGN
            { Start = 0xFFE3; Last = 0xFFE3; Width = EastAsianWidth.OfText "F"  } // FFE3;F           # Sk         FULLWIDTH MACRON
            { Start = 0xFFE4; Last = 0xFFE4; Width = EastAsianWidth.OfText "F"  } // FFE4;F           # So         FULLWIDTH BROKEN BAR
            { Start = 0xFFE5; Last = 0xFFE6; Width = EastAsianWidth.OfText "F"  } // FFE5..FFE6;F     # Sc     [2] FULLWIDTH YEN SIGN..FULLWIDTH WON SIGN
            { Start = 0xFFE8; Last = 0xFFE8; Width = EastAsianWidth.OfText "H"  } // FFE8;H           # So         HALFWIDTH FORMS LIGHT VERTICAL
            { Start = 0xFFE9; Last = 0xFFEC; Width = EastAsianWidth.OfText "H"  } // FFE9..FFEC;H     # Sm     [4] HALFWIDTH LEFTWARDS ARROW..HALFWIDTH DOWNWARDS ARROW
            { Start = 0xFFED; Last = 0xFFEE; Width = EastAsianWidth.OfText "H"  } // FFED..FFEE;H     # So     [2] HALFWIDTH BLACK SQUARE..HALFWIDTH WHITE CIRCLE
            { Start = 0xFFF9; Last = 0xFFFB; Width = EastAsianWidth.OfText "N"  } // FFF9..FFFB;N     # Cf     [3] INTERLINEAR ANNOTATION ANCHOR..INTERLINEAR ANNOTATION TERMINATOR
            { Start = 0xFFFC; Last = 0xFFFC; Width = EastAsianWidth.OfText "N"  } // FFFC;N           # So         OBJECT REPLACEMENT CHARACTER
            { Start = 0xFFFD; Last = 0xFFFD; Width = EastAsianWidth.OfText "A"  } // FFFD;A           # So         REPLACEMENT CHARACTER
            { Start = 0x10000; Last = 0x1000B; Width = EastAsianWidth.OfText "N"  } // 10000..1000B;N   # Lo    [12] LINEAR B SYLLABLE B008 A..LINEAR B SYLLABLE B046 JE
            { Start = 0x1000D; Last = 0x10026; Width = EastAsianWidth.OfText "N"  } // 1000D..10026;N   # Lo    [26] LINEAR B SYLLABLE B036 JO..LINEAR B SYLLABLE B032 QO
            { Start = 0x10028; Last = 0x1003A; Width = EastAsianWidth.OfText "N"  } // 10028..1003A;N   # Lo    [19] LINEAR B SYLLABLE B060 RA..LINEAR B SYLLABLE B042 WO
            { Start = 0x1003C; Last = 0x1003D; Width = EastAsianWidth.OfText "N"  } // 1003C..1003D;N   # Lo     [2] LINEAR B SYLLABLE B017 ZA..LINEAR B SYLLABLE B074 ZE
            { Start = 0x1003F; Last = 0x1004D; Width = EastAsianWidth.OfText "N"  } // 1003F..1004D;N   # Lo    [15] LINEAR B SYLLABLE B020 ZO..LINEAR B SYLLABLE B091 TWO
            { Start = 0x10050; Last = 0x1005D; Width = EastAsianWidth.OfText "N"  } // 10050..1005D;N   # Lo    [14] LINEAR B SYMBOL B018..LINEAR B SYMBOL B089
            { Start = 0x10080; Last = 0x100FA; Width = EastAsianWidth.OfText "N"  } // 10080..100FA;N   # Lo   [123] LINEAR B IDEOGRAM B100 MAN..LINEAR B IDEOGRAM VESSEL B305
            { Start = 0x10100; Last = 0x10102; Width = EastAsianWidth.OfText "N"  } // 10100..10102;N   # Po     [3] AEGEAN WORD SEPARATOR LINE..AEGEAN CHECK MARK
            { Start = 0x10107; Last = 0x10133; Width = EastAsianWidth.OfText "N"  } // 10107..10133;N   # No    [45] AEGEAN NUMBER ONE..AEGEAN NUMBER NINETY THOUSAND
            { Start = 0x10137; Last = 0x1013F; Width = EastAsianWidth.OfText "N"  } // 10137..1013F;N   # So     [9] AEGEAN WEIGHT BASE UNIT..AEGEAN MEASURE THIRD SUBUNIT
            { Start = 0x10140; Last = 0x10174; Width = EastAsianWidth.OfText "N"  } // 10140..10174;N   # Nl    [53] GREEK ACROPHONIC ATTIC ONE QUARTER..GREEK ACROPHONIC STRATIAN FIFTY MNAS
            { Start = 0x10175; Last = 0x10178; Width = EastAsianWidth.OfText "N"  } // 10175..10178;N   # No     [4] GREEK ONE HALF SIGN..GREEK THREE QUARTERS SIGN
            { Start = 0x10179; Last = 0x10189; Width = EastAsianWidth.OfText "N"  } // 10179..10189;N   # So    [17] GREEK YEAR SIGN..GREEK TRYBLION BASE SIGN
            { Start = 0x1018A; Last = 0x1018B; Width = EastAsianWidth.OfText "N"  } // 1018A..1018B;N   # No     [2] GREEK ZERO SIGN..GREEK ONE QUARTER SIGN
            { Start = 0x1018C; Last = 0x1018E; Width = EastAsianWidth.OfText "N"  } // 1018C..1018E;N   # So     [3] GREEK SINUSOID SIGN..NOMISMA SIGN
            { Start = 0x10190; Last = 0x1019B; Width = EastAsianWidth.OfText "N"  } // 10190..1019B;N   # So    [12] ROMAN SEXTANS SIGN..ROMAN CENTURIAL SIGN
            { Start = 0x101A0; Last = 0x101A0; Width = EastAsianWidth.OfText "N"  } // 101A0;N          # So         GREEK SYMBOL TAU RHO
            { Start = 0x101D0; Last = 0x101FC; Width = EastAsianWidth.OfText "N"  } // 101D0..101FC;N   # So    [45] PHAISTOS DISC SIGN PEDESTRIAN..PHAISTOS DISC SIGN WAVY BAND
            { Start = 0x101FD; Last = 0x101FD; Width = EastAsianWidth.OfText "N"  } // 101FD;N          # Mn         PHAISTOS DISC SIGN COMBINING OBLIQUE STROKE
            { Start = 0x10280; Last = 0x1029C; Width = EastAsianWidth.OfText "N"  } // 10280..1029C;N   # Lo    [29] LYCIAN LETTER A..LYCIAN LETTER X
            { Start = 0x102A0; Last = 0x102D0; Width = EastAsianWidth.OfText "N"  } // 102A0..102D0;N   # Lo    [49] CARIAN LETTER A..CARIAN LETTER UUU3
            { Start = 0x102E0; Last = 0x102E0; Width = EastAsianWidth.OfText "N"  } // 102E0;N          # Mn         COPTIC EPACT THOUSANDS MARK
            { Start = 0x102E1; Last = 0x102FB; Width = EastAsianWidth.OfText "N"  } // 102E1..102FB;N   # No    [27] COPTIC EPACT DIGIT ONE..COPTIC EPACT NUMBER NINE HUNDRED
            { Start = 0x10300; Last = 0x1031F; Width = EastAsianWidth.OfText "N"  } // 10300..1031F;N   # Lo    [32] OLD ITALIC LETTER A..OLD ITALIC LETTER ESS
            { Start = 0x10320; Last = 0x10323; Width = EastAsianWidth.OfText "N"  } // 10320..10323;N   # No     [4] OLD ITALIC NUMERAL ONE..OLD ITALIC NUMERAL FIFTY
            { Start = 0x1032D; Last = 0x1032F; Width = EastAsianWidth.OfText "N"  } // 1032D..1032F;N   # Lo     [3] OLD ITALIC LETTER YE..OLD ITALIC LETTER SOUTHERN TSE
            { Start = 0x10330; Last = 0x10340; Width = EastAsianWidth.OfText "N"  } // 10330..10340;N   # Lo    [17] GOTHIC LETTER AHSA..GOTHIC LETTER PAIRTHRA
            { Start = 0x10341; Last = 0x10341; Width = EastAsianWidth.OfText "N"  } // 10341;N          # Nl         GOTHIC LETTER NINETY
            { Start = 0x10342; Last = 0x10349; Width = EastAsianWidth.OfText "N"  } // 10342..10349;N   # Lo     [8] GOTHIC LETTER RAIDA..GOTHIC LETTER OTHAL
            { Start = 0x1034A; Last = 0x1034A; Width = EastAsianWidth.OfText "N"  } // 1034A;N          # Nl         GOTHIC LETTER NINE HUNDRED
            { Start = 0x10350; Last = 0x10375; Width = EastAsianWidth.OfText "N"  } // 10350..10375;N   # Lo    [38] OLD PERMIC LETTER AN..OLD PERMIC LETTER IA
            { Start = 0x10376; Last = 0x1037A; Width = EastAsianWidth.OfText "N"  } // 10376..1037A;N   # Mn     [5] COMBINING OLD PERMIC LETTER AN..COMBINING OLD PERMIC LETTER SII
            { Start = 0x10380; Last = 0x1039D; Width = EastAsianWidth.OfText "N"  } // 10380..1039D;N   # Lo    [30] UGARITIC LETTER ALPA..UGARITIC LETTER SSU
            { Start = 0x1039F; Last = 0x1039F; Width = EastAsianWidth.OfText "N"  } // 1039F;N          # Po         UGARITIC WORD DIVIDER
            { Start = 0x103A0; Last = 0x103C3; Width = EastAsianWidth.OfText "N"  } // 103A0..103C3;N   # Lo    [36] OLD PERSIAN SIGN A..OLD PERSIAN SIGN HA
            { Start = 0x103C8; Last = 0x103CF; Width = EastAsianWidth.OfText "N"  } // 103C8..103CF;N   # Lo     [8] OLD PERSIAN SIGN AURAMAZDAA..OLD PERSIAN SIGN BUUMISH
            { Start = 0x103D0; Last = 0x103D0; Width = EastAsianWidth.OfText "N"  } // 103D0;N          # Po         OLD PERSIAN WORD DIVIDER
            { Start = 0x103D1; Last = 0x103D5; Width = EastAsianWidth.OfText "N"  } // 103D1..103D5;N   # Nl     [5] OLD PERSIAN NUMBER ONE..OLD PERSIAN NUMBER HUNDRED
            { Start = 0x10400; Last = 0x1044F; Width = EastAsianWidth.OfText "N"  } // 10400..1044F;N   # L&    [80] DESERET CAPITAL LETTER LONG I..DESERET SMALL LETTER EW
            { Start = 0x10450; Last = 0x1047F; Width = EastAsianWidth.OfText "N"  } // 10450..1047F;N   # Lo    [48] SHAVIAN LETTER PEEP..SHAVIAN LETTER YEW
            { Start = 0x10480; Last = 0x1049D; Width = EastAsianWidth.OfText "N"  } // 10480..1049D;N   # Lo    [30] OSMANYA LETTER ALEF..OSMANYA LETTER OO
            { Start = 0x104A0; Last = 0x104A9; Width = EastAsianWidth.OfText "N"  } // 104A0..104A9;N   # Nd    [10] OSMANYA DIGIT ZERO..OSMANYA DIGIT NINE
            { Start = 0x104B0; Last = 0x104D3; Width = EastAsianWidth.OfText "N"  } // 104B0..104D3;N   # Lu    [36] OSAGE CAPITAL LETTER A..OSAGE CAPITAL LETTER ZHA
            { Start = 0x104D8; Last = 0x104FB; Width = EastAsianWidth.OfText "N"  } // 104D8..104FB;N   # Ll    [36] OSAGE SMALL LETTER A..OSAGE SMALL LETTER ZHA
            { Start = 0x10500; Last = 0x10527; Width = EastAsianWidth.OfText "N"  } // 10500..10527;N   # Lo    [40] ELBASAN LETTER A..ELBASAN LETTER KHE
            { Start = 0x10530; Last = 0x10563; Width = EastAsianWidth.OfText "N"  } // 10530..10563;N   # Lo    [52] CAUCASIAN ALBANIAN LETTER ALT..CAUCASIAN ALBANIAN LETTER KIW
            { Start = 0x1056F; Last = 0x1056F; Width = EastAsianWidth.OfText "N"  } // 1056F;N          # Po         CAUCASIAN ALBANIAN CITATION MARK
            { Start = 0x10600; Last = 0x10736; Width = EastAsianWidth.OfText "N"  } // 10600..10736;N   # Lo   [311] LINEAR A SIGN AB001..LINEAR A SIGN A664
            { Start = 0x10740; Last = 0x10755; Width = EastAsianWidth.OfText "N"  } // 10740..10755;N   # Lo    [22] LINEAR A SIGN A701 A..LINEAR A SIGN A732 JE
            { Start = 0x10760; Last = 0x10767; Width = EastAsianWidth.OfText "N"  } // 10760..10767;N   # Lo     [8] LINEAR A SIGN A800..LINEAR A SIGN A807
            { Start = 0x10800; Last = 0x10805; Width = EastAsianWidth.OfText "N"  } // 10800..10805;N   # Lo     [6] CYPRIOT SYLLABLE A..CYPRIOT SYLLABLE JA
            { Start = 0x10808; Last = 0x10808; Width = EastAsianWidth.OfText "N"  } // 10808;N          # Lo         CYPRIOT SYLLABLE JO
            { Start = 0x1080A; Last = 0x10835; Width = EastAsianWidth.OfText "N"  } // 1080A..10835;N   # Lo    [44] CYPRIOT SYLLABLE KA..CYPRIOT SYLLABLE WO
            { Start = 0x10837; Last = 0x10838; Width = EastAsianWidth.OfText "N"  } // 10837..10838;N   # Lo     [2] CYPRIOT SYLLABLE XA..CYPRIOT SYLLABLE XE
            { Start = 0x1083C; Last = 0x1083C; Width = EastAsianWidth.OfText "N"  } // 1083C;N          # Lo         CYPRIOT SYLLABLE ZA
            { Start = 0x1083F; Last = 0x1083F; Width = EastAsianWidth.OfText "N"  } // 1083F;N          # Lo         CYPRIOT SYLLABLE ZO
            { Start = 0x10840; Last = 0x10855; Width = EastAsianWidth.OfText "N"  } // 10840..10855;N   # Lo    [22] IMPERIAL ARAMAIC LETTER ALEPH..IMPERIAL ARAMAIC LETTER TAW
            { Start = 0x10857; Last = 0x10857; Width = EastAsianWidth.OfText "N"  } // 10857;N          # Po         IMPERIAL ARAMAIC SECTION SIGN
            { Start = 0x10858; Last = 0x1085F; Width = EastAsianWidth.OfText "N"  } // 10858..1085F;N   # No     [8] IMPERIAL ARAMAIC NUMBER ONE..IMPERIAL ARAMAIC NUMBER TEN THOUSAND
            { Start = 0x10860; Last = 0x10876; Width = EastAsianWidth.OfText "N"  } // 10860..10876;N   # Lo    [23] PALMYRENE LETTER ALEPH..PALMYRENE LETTER TAW
            { Start = 0x10877; Last = 0x10878; Width = EastAsianWidth.OfText "N"  } // 10877..10878;N   # So     [2] PALMYRENE LEFT-POINTING FLEURON..PALMYRENE RIGHT-POINTING FLEURON
            { Start = 0x10879; Last = 0x1087F; Width = EastAsianWidth.OfText "N"  } // 10879..1087F;N   # No     [7] PALMYRENE NUMBER ONE..PALMYRENE NUMBER TWENTY
            { Start = 0x10880; Last = 0x1089E; Width = EastAsianWidth.OfText "N"  } // 10880..1089E;N   # Lo    [31] NABATAEAN LETTER FINAL ALEPH..NABATAEAN LETTER TAW
            { Start = 0x108A7; Last = 0x108AF; Width = EastAsianWidth.OfText "N"  } // 108A7..108AF;N   # No     [9] NABATAEAN NUMBER ONE..NABATAEAN NUMBER ONE HUNDRED
            { Start = 0x108E0; Last = 0x108F2; Width = EastAsianWidth.OfText "N"  } // 108E0..108F2;N   # Lo    [19] HATRAN LETTER ALEPH..HATRAN LETTER QOPH
            { Start = 0x108F4; Last = 0x108F5; Width = EastAsianWidth.OfText "N"  } // 108F4..108F5;N   # Lo     [2] HATRAN LETTER SHIN..HATRAN LETTER TAW
            { Start = 0x108FB; Last = 0x108FF; Width = EastAsianWidth.OfText "N"  } // 108FB..108FF;N   # No     [5] HATRAN NUMBER ONE..HATRAN NUMBER ONE HUNDRED
            { Start = 0x10900; Last = 0x10915; Width = EastAsianWidth.OfText "N"  } // 10900..10915;N   # Lo    [22] PHOENICIAN LETTER ALF..PHOENICIAN LETTER TAU
            { Start = 0x10916; Last = 0x1091B; Width = EastAsianWidth.OfText "N"  } // 10916..1091B;N   # No     [6] PHOENICIAN NUMBER ONE..PHOENICIAN NUMBER THREE
            { Start = 0x1091F; Last = 0x1091F; Width = EastAsianWidth.OfText "N"  } // 1091F;N          # Po         PHOENICIAN WORD SEPARATOR
            { Start = 0x10920; Last = 0x10939; Width = EastAsianWidth.OfText "N"  } // 10920..10939;N   # Lo    [26] LYDIAN LETTER A..LYDIAN LETTER C
            { Start = 0x1093F; Last = 0x1093F; Width = EastAsianWidth.OfText "N"  } // 1093F;N          # Po         LYDIAN TRIANGULAR MARK
            { Start = 0x10980; Last = 0x1099F; Width = EastAsianWidth.OfText "N"  } // 10980..1099F;N   # Lo    [32] MEROITIC HIEROGLYPHIC LETTER A..MEROITIC HIEROGLYPHIC SYMBOL VIDJ-2
            { Start = 0x109A0; Last = 0x109B7; Width = EastAsianWidth.OfText "N"  } // 109A0..109B7;N   # Lo    [24] MEROITIC CURSIVE LETTER A..MEROITIC CURSIVE LETTER DA
            { Start = 0x109BC; Last = 0x109BD; Width = EastAsianWidth.OfText "N"  } // 109BC..109BD;N   # No     [2] MEROITIC CURSIVE FRACTION ELEVEN TWELFTHS..MEROITIC CURSIVE FRACTION ONE HALF
            { Start = 0x109BE; Last = 0x109BF; Width = EastAsianWidth.OfText "N"  } // 109BE..109BF;N   # Lo     [2] MEROITIC CURSIVE LOGOGRAM RMT..MEROITIC CURSIVE LOGOGRAM IMN
            { Start = 0x109C0; Last = 0x109CF; Width = EastAsianWidth.OfText "N"  } // 109C0..109CF;N   # No    [16] MEROITIC CURSIVE NUMBER ONE..MEROITIC CURSIVE NUMBER SEVENTY
            { Start = 0x109D2; Last = 0x109FF; Width = EastAsianWidth.OfText "N"  } // 109D2..109FF;N   # No    [46] MEROITIC CURSIVE NUMBER ONE HUNDRED..MEROITIC CURSIVE FRACTION TEN TWELFTHS
            { Start = 0x10A00; Last = 0x10A00; Width = EastAsianWidth.OfText "N"  } // 10A00;N          # Lo         KHAROSHTHI LETTER A
            { Start = 0x10A01; Last = 0x10A03; Width = EastAsianWidth.OfText "N"  } // 10A01..10A03;N   # Mn     [3] KHAROSHTHI VOWEL SIGN I..KHAROSHTHI VOWEL SIGN VOCALIC R
            { Start = 0x10A05; Last = 0x10A06; Width = EastAsianWidth.OfText "N"  } // 10A05..10A06;N   # Mn     [2] KHAROSHTHI VOWEL SIGN E..KHAROSHTHI VOWEL SIGN O
            { Start = 0x10A0C; Last = 0x10A0F; Width = EastAsianWidth.OfText "N"  } // 10A0C..10A0F;N   # Mn     [4] KHAROSHTHI VOWEL LENGTH MARK..KHAROSHTHI SIGN VISARGA
            { Start = 0x10A10; Last = 0x10A13; Width = EastAsianWidth.OfText "N"  } // 10A10..10A13;N   # Lo     [4] KHAROSHTHI LETTER KA..KHAROSHTHI LETTER GHA
            { Start = 0x10A15; Last = 0x10A17; Width = EastAsianWidth.OfText "N"  } // 10A15..10A17;N   # Lo     [3] KHAROSHTHI LETTER CA..KHAROSHTHI LETTER JA
            { Start = 0x10A19; Last = 0x10A35; Width = EastAsianWidth.OfText "N"  } // 10A19..10A35;N   # Lo    [29] KHAROSHTHI LETTER NYA..KHAROSHTHI LETTER VHA
            { Start = 0x10A38; Last = 0x10A3A; Width = EastAsianWidth.OfText "N"  } // 10A38..10A3A;N   # Mn     [3] KHAROSHTHI SIGN BAR ABOVE..KHAROSHTHI SIGN DOT BELOW
            { Start = 0x10A3F; Last = 0x10A3F; Width = EastAsianWidth.OfText "N"  } // 10A3F;N          # Mn         KHAROSHTHI VIRAMA
            { Start = 0x10A40; Last = 0x10A48; Width = EastAsianWidth.OfText "N"  } // 10A40..10A48;N   # No     [9] KHAROSHTHI DIGIT ONE..KHAROSHTHI FRACTION ONE HALF
            { Start = 0x10A50; Last = 0x10A58; Width = EastAsianWidth.OfText "N"  } // 10A50..10A58;N   # Po     [9] KHAROSHTHI PUNCTUATION DOT..KHAROSHTHI PUNCTUATION LINES
            { Start = 0x10A60; Last = 0x10A7C; Width = EastAsianWidth.OfText "N"  } // 10A60..10A7C;N   # Lo    [29] OLD SOUTH ARABIAN LETTER HE..OLD SOUTH ARABIAN LETTER THETH
            { Start = 0x10A7D; Last = 0x10A7E; Width = EastAsianWidth.OfText "N"  } // 10A7D..10A7E;N   # No     [2] OLD SOUTH ARABIAN NUMBER ONE..OLD SOUTH ARABIAN NUMBER FIFTY
            { Start = 0x10A7F; Last = 0x10A7F; Width = EastAsianWidth.OfText "N"  } // 10A7F;N          # Po         OLD SOUTH ARABIAN NUMERIC INDICATOR
            { Start = 0x10A80; Last = 0x10A9C; Width = EastAsianWidth.OfText "N"  } // 10A80..10A9C;N   # Lo    [29] OLD NORTH ARABIAN LETTER HEH..OLD NORTH ARABIAN LETTER ZAH
            { Start = 0x10A9D; Last = 0x10A9F; Width = EastAsianWidth.OfText "N"  } // 10A9D..10A9F;N   # No     [3] OLD NORTH ARABIAN NUMBER ONE..OLD NORTH ARABIAN NUMBER TWENTY
            { Start = 0x10AC0; Last = 0x10AC7; Width = EastAsianWidth.OfText "N"  } // 10AC0..10AC7;N   # Lo     [8] MANICHAEAN LETTER ALEPH..MANICHAEAN LETTER WAW
            { Start = 0x10AC8; Last = 0x10AC8; Width = EastAsianWidth.OfText "N"  } // 10AC8;N          # So         MANICHAEAN SIGN UD
            { Start = 0x10AC9; Last = 0x10AE4; Width = EastAsianWidth.OfText "N"  } // 10AC9..10AE4;N   # Lo    [28] MANICHAEAN LETTER ZAYIN..MANICHAEAN LETTER TAW
            { Start = 0x10AE5; Last = 0x10AE6; Width = EastAsianWidth.OfText "N"  } // 10AE5..10AE6;N   # Mn     [2] MANICHAEAN ABBREVIATION MARK ABOVE..MANICHAEAN ABBREVIATION MARK BELOW
            { Start = 0x10AEB; Last = 0x10AEF; Width = EastAsianWidth.OfText "N"  } // 10AEB..10AEF;N   # No     [5] MANICHAEAN NUMBER ONE..MANICHAEAN NUMBER ONE HUNDRED
            { Start = 0x10AF0; Last = 0x10AF6; Width = EastAsianWidth.OfText "N"  } // 10AF0..10AF6;N   # Po     [7] MANICHAEAN PUNCTUATION STAR..MANICHAEAN PUNCTUATION LINE FILLER
            { Start = 0x10B00; Last = 0x10B35; Width = EastAsianWidth.OfText "N"  } // 10B00..10B35;N   # Lo    [54] AVESTAN LETTER A..AVESTAN LETTER HE
            { Start = 0x10B39; Last = 0x10B3F; Width = EastAsianWidth.OfText "N"  } // 10B39..10B3F;N   # Po     [7] AVESTAN ABBREVIATION MARK..LARGE ONE RING OVER TWO RINGS PUNCTUATION
            { Start = 0x10B40; Last = 0x10B55; Width = EastAsianWidth.OfText "N"  } // 10B40..10B55;N   # Lo    [22] INSCRIPTIONAL PARTHIAN LETTER ALEPH..INSCRIPTIONAL PARTHIAN LETTER TAW
            { Start = 0x10B58; Last = 0x10B5F; Width = EastAsianWidth.OfText "N"  } // 10B58..10B5F;N   # No     [8] INSCRIPTIONAL PARTHIAN NUMBER ONE..INSCRIPTIONAL PARTHIAN NUMBER ONE THOUSAND
            { Start = 0x10B60; Last = 0x10B72; Width = EastAsianWidth.OfText "N"  } // 10B60..10B72;N   # Lo    [19] INSCRIPTIONAL PAHLAVI LETTER ALEPH..INSCRIPTIONAL PAHLAVI LETTER TAW
            { Start = 0x10B78; Last = 0x10B7F; Width = EastAsianWidth.OfText "N"  } // 10B78..10B7F;N   # No     [8] INSCRIPTIONAL PAHLAVI NUMBER ONE..INSCRIPTIONAL PAHLAVI NUMBER ONE THOUSAND
            { Start = 0x10B80; Last = 0x10B91; Width = EastAsianWidth.OfText "N"  } // 10B80..10B91;N   # Lo    [18] PSALTER PAHLAVI LETTER ALEPH..PSALTER PAHLAVI LETTER TAW
            { Start = 0x10B99; Last = 0x10B9C; Width = EastAsianWidth.OfText "N"  } // 10B99..10B9C;N   # Po     [4] PSALTER PAHLAVI SECTION MARK..PSALTER PAHLAVI FOUR DOTS WITH DOT
            { Start = 0x10BA9; Last = 0x10BAF; Width = EastAsianWidth.OfText "N"  } // 10BA9..10BAF;N   # No     [7] PSALTER PAHLAVI NUMBER ONE..PSALTER PAHLAVI NUMBER ONE HUNDRED
            { Start = 0x10C00; Last = 0x10C48; Width = EastAsianWidth.OfText "N"  } // 10C00..10C48;N   # Lo    [73] OLD TURKIC LETTER ORKHON A..OLD TURKIC LETTER ORKHON BASH
            { Start = 0x10C80; Last = 0x10CB2; Width = EastAsianWidth.OfText "N"  } // 10C80..10CB2;N   # Lu    [51] OLD HUNGARIAN CAPITAL LETTER A..OLD HUNGARIAN CAPITAL LETTER US
            { Start = 0x10CC0; Last = 0x10CF2; Width = EastAsianWidth.OfText "N"  } // 10CC0..10CF2;N   # Ll    [51] OLD HUNGARIAN SMALL LETTER A..OLD HUNGARIAN SMALL LETTER US
            { Start = 0x10CFA; Last = 0x10CFF; Width = EastAsianWidth.OfText "N"  } // 10CFA..10CFF;N   # No     [6] OLD HUNGARIAN NUMBER ONE..OLD HUNGARIAN NUMBER ONE THOUSAND
            { Start = 0x10D00; Last = 0x10D23; Width = EastAsianWidth.OfText "N"  } // 10D00..10D23;N   # Lo    [36] HANIFI ROHINGYA LETTER A..HANIFI ROHINGYA MARK NA KHONNA
            { Start = 0x10D24; Last = 0x10D27; Width = EastAsianWidth.OfText "N"  } // 10D24..10D27;N   # Mn     [4] HANIFI ROHINGYA SIGN HARBAHAY..HANIFI ROHINGYA SIGN TASSI
            { Start = 0x10D30; Last = 0x10D39; Width = EastAsianWidth.OfText "N"  } // 10D30..10D39;N   # Nd    [10] HANIFI ROHINGYA DIGIT ZERO..HANIFI ROHINGYA DIGIT NINE
            { Start = 0x10E60; Last = 0x10E7E; Width = EastAsianWidth.OfText "N"  } // 10E60..10E7E;N   # No    [31] RUMI DIGIT ONE..RUMI FRACTION TWO THIRDS
            { Start = 0x10F00; Last = 0x10F1C; Width = EastAsianWidth.OfText "N"  } // 10F00..10F1C;N   # Lo    [29] OLD SOGDIAN LETTER ALEPH..OLD SOGDIAN LETTER FINAL TAW WITH VERTICAL TAIL
            { Start = 0x10F1D; Last = 0x10F26; Width = EastAsianWidth.OfText "N"  } // 10F1D..10F26;N   # No    [10] OLD SOGDIAN NUMBER ONE..OLD SOGDIAN FRACTION ONE HALF
            { Start = 0x10F27; Last = 0x10F27; Width = EastAsianWidth.OfText "N"  } // 10F27;N          # Lo         OLD SOGDIAN LIGATURE AYIN-DALETH
            { Start = 0x10F30; Last = 0x10F45; Width = EastAsianWidth.OfText "N"  } // 10F30..10F45;N   # Lo    [22] SOGDIAN LETTER ALEPH..SOGDIAN INDEPENDENT SHIN
            { Start = 0x10F46; Last = 0x10F50; Width = EastAsianWidth.OfText "N"  } // 10F46..10F50;N   # Mn    [11] SOGDIAN COMBINING DOT BELOW..SOGDIAN COMBINING STROKE BELOW
            { Start = 0x10F51; Last = 0x10F54; Width = EastAsianWidth.OfText "N"  } // 10F51..10F54;N   # No     [4] SOGDIAN NUMBER ONE..SOGDIAN NUMBER ONE HUNDRED
            { Start = 0x10F55; Last = 0x10F59; Width = EastAsianWidth.OfText "N"  } // 10F55..10F59;N   # Po     [5] SOGDIAN PUNCTUATION TWO VERTICAL BARS..SOGDIAN PUNCTUATION HALF CIRCLE WITH DOT
            { Start = 0x11000; Last = 0x11000; Width = EastAsianWidth.OfText "N"  } // 11000;N          # Mc         BRAHMI SIGN CANDRABINDU
            { Start = 0x11001; Last = 0x11001; Width = EastAsianWidth.OfText "N"  } // 11001;N          # Mn         BRAHMI SIGN ANUSVARA
            { Start = 0x11002; Last = 0x11002; Width = EastAsianWidth.OfText "N"  } // 11002;N          # Mc         BRAHMI SIGN VISARGA
            { Start = 0x11003; Last = 0x11037; Width = EastAsianWidth.OfText "N"  } // 11003..11037;N   # Lo    [53] BRAHMI SIGN JIHVAMULIYA..BRAHMI LETTER OLD TAMIL NNNA
            { Start = 0x11038; Last = 0x11046; Width = EastAsianWidth.OfText "N"  } // 11038..11046;N   # Mn    [15] BRAHMI VOWEL SIGN AA..BRAHMI VIRAMA
            { Start = 0x11047; Last = 0x1104D; Width = EastAsianWidth.OfText "N"  } // 11047..1104D;N   # Po     [7] BRAHMI DANDA..BRAHMI PUNCTUATION LOTUS
            { Start = 0x11052; Last = 0x11065; Width = EastAsianWidth.OfText "N"  } // 11052..11065;N   # No    [20] BRAHMI NUMBER ONE..BRAHMI NUMBER ONE THOUSAND
            { Start = 0x11066; Last = 0x1106F; Width = EastAsianWidth.OfText "N"  } // 11066..1106F;N   # Nd    [10] BRAHMI DIGIT ZERO..BRAHMI DIGIT NINE
            { Start = 0x1107F; Last = 0x1107F; Width = EastAsianWidth.OfText "N"  } // 1107F;N          # Mn         BRAHMI NUMBER JOINER
            { Start = 0x11080; Last = 0x11081; Width = EastAsianWidth.OfText "N"  } // 11080..11081;N   # Mn     [2] KAITHI SIGN CANDRABINDU..KAITHI SIGN ANUSVARA
            { Start = 0x11082; Last = 0x11082; Width = EastAsianWidth.OfText "N"  } // 11082;N          # Mc         KAITHI SIGN VISARGA
            { Start = 0x11083; Last = 0x110AF; Width = EastAsianWidth.OfText "N"  } // 11083..110AF;N   # Lo    [45] KAITHI LETTER A..KAITHI LETTER HA
            { Start = 0x110B0; Last = 0x110B2; Width = EastAsianWidth.OfText "N"  } // 110B0..110B2;N   # Mc     [3] KAITHI VOWEL SIGN AA..KAITHI VOWEL SIGN II
            { Start = 0x110B3; Last = 0x110B6; Width = EastAsianWidth.OfText "N"  } // 110B3..110B6;N   # Mn     [4] KAITHI VOWEL SIGN U..KAITHI VOWEL SIGN AI
            { Start = 0x110B7; Last = 0x110B8; Width = EastAsianWidth.OfText "N"  } // 110B7..110B8;N   # Mc     [2] KAITHI VOWEL SIGN O..KAITHI VOWEL SIGN AU
            { Start = 0x110B9; Last = 0x110BA; Width = EastAsianWidth.OfText "N"  } // 110B9..110BA;N   # Mn     [2] KAITHI SIGN VIRAMA..KAITHI SIGN NUKTA
            { Start = 0x110BB; Last = 0x110BC; Width = EastAsianWidth.OfText "N"  } // 110BB..110BC;N   # Po     [2] KAITHI ABBREVIATION SIGN..KAITHI ENUMERATION SIGN
            { Start = 0x110BD; Last = 0x110BD; Width = EastAsianWidth.OfText "N"  } // 110BD;N          # Cf         KAITHI NUMBER SIGN
            { Start = 0x110BE; Last = 0x110C1; Width = EastAsianWidth.OfText "N"  } // 110BE..110C1;N   # Po     [4] KAITHI SECTION MARK..KAITHI DOUBLE DANDA
            { Start = 0x110CD; Last = 0x110CD; Width = EastAsianWidth.OfText "N"  } // 110CD;N          # Cf         KAITHI NUMBER SIGN ABOVE
            { Start = 0x110D0; Last = 0x110E8; Width = EastAsianWidth.OfText "N"  } // 110D0..110E8;N   # Lo    [25] SORA SOMPENG LETTER SAH..SORA SOMPENG LETTER MAE
            { Start = 0x110F0; Last = 0x110F9; Width = EastAsianWidth.OfText "N"  } // 110F0..110F9;N   # Nd    [10] SORA SOMPENG DIGIT ZERO..SORA SOMPENG DIGIT NINE
            { Start = 0x11100; Last = 0x11102; Width = EastAsianWidth.OfText "N"  } // 11100..11102;N   # Mn     [3] CHAKMA SIGN CANDRABINDU..CHAKMA SIGN VISARGA
            { Start = 0x11103; Last = 0x11126; Width = EastAsianWidth.OfText "N"  } // 11103..11126;N   # Lo    [36] CHAKMA LETTER AA..CHAKMA LETTER HAA
            { Start = 0x11127; Last = 0x1112B; Width = EastAsianWidth.OfText "N"  } // 11127..1112B;N   # Mn     [5] CHAKMA VOWEL SIGN A..CHAKMA VOWEL SIGN UU
            { Start = 0x1112C; Last = 0x1112C; Width = EastAsianWidth.OfText "N"  } // 1112C;N          # Mc         CHAKMA VOWEL SIGN E
            { Start = 0x1112D; Last = 0x11134; Width = EastAsianWidth.OfText "N"  } // 1112D..11134;N   # Mn     [8] CHAKMA VOWEL SIGN AI..CHAKMA MAAYYAA
            { Start = 0x11136; Last = 0x1113F; Width = EastAsianWidth.OfText "N"  } // 11136..1113F;N   # Nd    [10] CHAKMA DIGIT ZERO..CHAKMA DIGIT NINE
            { Start = 0x11140; Last = 0x11143; Width = EastAsianWidth.OfText "N"  } // 11140..11143;N   # Po     [4] CHAKMA SECTION MARK..CHAKMA QUESTION MARK
            { Start = 0x11144; Last = 0x11144; Width = EastAsianWidth.OfText "N"  } // 11144;N          # Lo         CHAKMA LETTER LHAA
            { Start = 0x11145; Last = 0x11146; Width = EastAsianWidth.OfText "N"  } // 11145..11146;N   # Mc     [2] CHAKMA VOWEL SIGN AA..CHAKMA VOWEL SIGN EI
            { Start = 0x11150; Last = 0x11172; Width = EastAsianWidth.OfText "N"  } // 11150..11172;N   # Lo    [35] MAHAJANI LETTER A..MAHAJANI LETTER RRA
            { Start = 0x11173; Last = 0x11173; Width = EastAsianWidth.OfText "N"  } // 11173;N          # Mn         MAHAJANI SIGN NUKTA
            { Start = 0x11174; Last = 0x11175; Width = EastAsianWidth.OfText "N"  } // 11174..11175;N   # Po     [2] MAHAJANI ABBREVIATION SIGN..MAHAJANI SECTION MARK
            { Start = 0x11176; Last = 0x11176; Width = EastAsianWidth.OfText "N"  } // 11176;N          # Lo         MAHAJANI LIGATURE SHRI
            { Start = 0x11180; Last = 0x11181; Width = EastAsianWidth.OfText "N"  } // 11180..11181;N   # Mn     [2] SHARADA SIGN CANDRABINDU..SHARADA SIGN ANUSVARA
            { Start = 0x11182; Last = 0x11182; Width = EastAsianWidth.OfText "N"  } // 11182;N          # Mc         SHARADA SIGN VISARGA
            { Start = 0x11183; Last = 0x111B2; Width = EastAsianWidth.OfText "N"  } // 11183..111B2;N   # Lo    [48] SHARADA LETTER A..SHARADA LETTER HA
            { Start = 0x111B3; Last = 0x111B5; Width = EastAsianWidth.OfText "N"  } // 111B3..111B5;N   # Mc     [3] SHARADA VOWEL SIGN AA..SHARADA VOWEL SIGN II
            { Start = 0x111B6; Last = 0x111BE; Width = EastAsianWidth.OfText "N"  } // 111B6..111BE;N   # Mn     [9] SHARADA VOWEL SIGN U..SHARADA VOWEL SIGN O
            { Start = 0x111BF; Last = 0x111C0; Width = EastAsianWidth.OfText "N"  } // 111BF..111C0;N   # Mc     [2] SHARADA VOWEL SIGN AU..SHARADA SIGN VIRAMA
            { Start = 0x111C1; Last = 0x111C4; Width = EastAsianWidth.OfText "N"  } // 111C1..111C4;N   # Lo     [4] SHARADA SIGN AVAGRAHA..SHARADA OM
            { Start = 0x111C5; Last = 0x111C8; Width = EastAsianWidth.OfText "N"  } // 111C5..111C8;N   # Po     [4] SHARADA DANDA..SHARADA SEPARATOR
            { Start = 0x111C9; Last = 0x111CC; Width = EastAsianWidth.OfText "N"  } // 111C9..111CC;N   # Mn     [4] SHARADA SANDHI MARK..SHARADA EXTRA SHORT VOWEL MARK
            { Start = 0x111CD; Last = 0x111CD; Width = EastAsianWidth.OfText "N"  } // 111CD;N          # Po         SHARADA SUTRA MARK
            { Start = 0x111D0; Last = 0x111D9; Width = EastAsianWidth.OfText "N"  } // 111D0..111D9;N   # Nd    [10] SHARADA DIGIT ZERO..SHARADA DIGIT NINE
            { Start = 0x111DA; Last = 0x111DA; Width = EastAsianWidth.OfText "N"  } // 111DA;N          # Lo         SHARADA EKAM
            { Start = 0x111DB; Last = 0x111DB; Width = EastAsianWidth.OfText "N"  } // 111DB;N          # Po         SHARADA SIGN SIDDHAM
            { Start = 0x111DC; Last = 0x111DC; Width = EastAsianWidth.OfText "N"  } // 111DC;N          # Lo         SHARADA HEADSTROKE
            { Start = 0x111DD; Last = 0x111DF; Width = EastAsianWidth.OfText "N"  } // 111DD..111DF;N   # Po     [3] SHARADA CONTINUATION SIGN..SHARADA SECTION MARK-2
            { Start = 0x111E1; Last = 0x111F4; Width = EastAsianWidth.OfText "N"  } // 111E1..111F4;N   # No    [20] SINHALA ARCHAIC DIGIT ONE..SINHALA ARCHAIC NUMBER ONE THOUSAND
            { Start = 0x11200; Last = 0x11211; Width = EastAsianWidth.OfText "N"  } // 11200..11211;N   # Lo    [18] KHOJKI LETTER A..KHOJKI LETTER JJA
            { Start = 0x11213; Last = 0x1122B; Width = EastAsianWidth.OfText "N"  } // 11213..1122B;N   # Lo    [25] KHOJKI LETTER NYA..KHOJKI LETTER LLA
            { Start = 0x1122C; Last = 0x1122E; Width = EastAsianWidth.OfText "N"  } // 1122C..1122E;N   # Mc     [3] KHOJKI VOWEL SIGN AA..KHOJKI VOWEL SIGN II
            { Start = 0x1122F; Last = 0x11231; Width = EastAsianWidth.OfText "N"  } // 1122F..11231;N   # Mn     [3] KHOJKI VOWEL SIGN U..KHOJKI VOWEL SIGN AI
            { Start = 0x11232; Last = 0x11233; Width = EastAsianWidth.OfText "N"  } // 11232..11233;N   # Mc     [2] KHOJKI VOWEL SIGN O..KHOJKI VOWEL SIGN AU
            { Start = 0x11234; Last = 0x11234; Width = EastAsianWidth.OfText "N"  } // 11234;N          # Mn         KHOJKI SIGN ANUSVARA
            { Start = 0x11235; Last = 0x11235; Width = EastAsianWidth.OfText "N"  } // 11235;N          # Mc         KHOJKI SIGN VIRAMA
            { Start = 0x11236; Last = 0x11237; Width = EastAsianWidth.OfText "N"  } // 11236..11237;N   # Mn     [2] KHOJKI SIGN NUKTA..KHOJKI SIGN SHADDA
            { Start = 0x11238; Last = 0x1123D; Width = EastAsianWidth.OfText "N"  } // 11238..1123D;N   # Po     [6] KHOJKI DANDA..KHOJKI ABBREVIATION SIGN
            { Start = 0x1123E; Last = 0x1123E; Width = EastAsianWidth.OfText "N"  } // 1123E;N          # Mn         KHOJKI SIGN SUKUN
            { Start = 0x11280; Last = 0x11286; Width = EastAsianWidth.OfText "N"  } // 11280..11286;N   # Lo     [7] MULTANI LETTER A..MULTANI LETTER GA
            { Start = 0x11288; Last = 0x11288; Width = EastAsianWidth.OfText "N"  } // 11288;N          # Lo         MULTANI LETTER GHA
            { Start = 0x1128A; Last = 0x1128D; Width = EastAsianWidth.OfText "N"  } // 1128A..1128D;N   # Lo     [4] MULTANI LETTER CA..MULTANI LETTER JJA
            { Start = 0x1128F; Last = 0x1129D; Width = EastAsianWidth.OfText "N"  } // 1128F..1129D;N   # Lo    [15] MULTANI LETTER NYA..MULTANI LETTER BA
            { Start = 0x1129F; Last = 0x112A8; Width = EastAsianWidth.OfText "N"  } // 1129F..112A8;N   # Lo    [10] MULTANI LETTER BHA..MULTANI LETTER RHA
            { Start = 0x112A9; Last = 0x112A9; Width = EastAsianWidth.OfText "N"  } // 112A9;N          # Po         MULTANI SECTION MARK
            { Start = 0x112B0; Last = 0x112DE; Width = EastAsianWidth.OfText "N"  } // 112B0..112DE;N   # Lo    [47] KHUDAWADI LETTER A..KHUDAWADI LETTER HA
            { Start = 0x112DF; Last = 0x112DF; Width = EastAsianWidth.OfText "N"  } // 112DF;N          # Mn         KHUDAWADI SIGN ANUSVARA
            { Start = 0x112E0; Last = 0x112E2; Width = EastAsianWidth.OfText "N"  } // 112E0..112E2;N   # Mc     [3] KHUDAWADI VOWEL SIGN AA..KHUDAWADI VOWEL SIGN II
            { Start = 0x112E3; Last = 0x112EA; Width = EastAsianWidth.OfText "N"  } // 112E3..112EA;N   # Mn     [8] KHUDAWADI VOWEL SIGN U..KHUDAWADI SIGN VIRAMA
            { Start = 0x112F0; Last = 0x112F9; Width = EastAsianWidth.OfText "N"  } // 112F0..112F9;N   # Nd    [10] KHUDAWADI DIGIT ZERO..KHUDAWADI DIGIT NINE
            { Start = 0x11300; Last = 0x11301; Width = EastAsianWidth.OfText "N"  } // 11300..11301;N   # Mn     [2] GRANTHA SIGN COMBINING ANUSVARA ABOVE..GRANTHA SIGN CANDRABINDU
            { Start = 0x11302; Last = 0x11303; Width = EastAsianWidth.OfText "N"  } // 11302..11303;N   # Mc     [2] GRANTHA SIGN ANUSVARA..GRANTHA SIGN VISARGA
            { Start = 0x11305; Last = 0x1130C; Width = EastAsianWidth.OfText "N"  } // 11305..1130C;N   # Lo     [8] GRANTHA LETTER A..GRANTHA LETTER VOCALIC L
            { Start = 0x1130F; Last = 0x11310; Width = EastAsianWidth.OfText "N"  } // 1130F..11310;N   # Lo     [2] GRANTHA LETTER EE..GRANTHA LETTER AI
            { Start = 0x11313; Last = 0x11328; Width = EastAsianWidth.OfText "N"  } // 11313..11328;N   # Lo    [22] GRANTHA LETTER OO..GRANTHA LETTER NA
            { Start = 0x1132A; Last = 0x11330; Width = EastAsianWidth.OfText "N"  } // 1132A..11330;N   # Lo     [7] GRANTHA LETTER PA..GRANTHA LETTER RA
            { Start = 0x11332; Last = 0x11333; Width = EastAsianWidth.OfText "N"  } // 11332..11333;N   # Lo     [2] GRANTHA LETTER LA..GRANTHA LETTER LLA
            { Start = 0x11335; Last = 0x11339; Width = EastAsianWidth.OfText "N"  } // 11335..11339;N   # Lo     [5] GRANTHA LETTER VA..GRANTHA LETTER HA
            { Start = 0x1133B; Last = 0x1133C; Width = EastAsianWidth.OfText "N"  } // 1133B..1133C;N   # Mn     [2] COMBINING BINDU BELOW..GRANTHA SIGN NUKTA
            { Start = 0x1133D; Last = 0x1133D; Width = EastAsianWidth.OfText "N"  } // 1133D;N          # Lo         GRANTHA SIGN AVAGRAHA
            { Start = 0x1133E; Last = 0x1133F; Width = EastAsianWidth.OfText "N"  } // 1133E..1133F;N   # Mc     [2] GRANTHA VOWEL SIGN AA..GRANTHA VOWEL SIGN I
            { Start = 0x11340; Last = 0x11340; Width = EastAsianWidth.OfText "N"  } // 11340;N          # Mn         GRANTHA VOWEL SIGN II
            { Start = 0x11341; Last = 0x11344; Width = EastAsianWidth.OfText "N"  } // 11341..11344;N   # Mc     [4] GRANTHA VOWEL SIGN U..GRANTHA VOWEL SIGN VOCALIC RR
            { Start = 0x11347; Last = 0x11348; Width = EastAsianWidth.OfText "N"  } // 11347..11348;N   # Mc     [2] GRANTHA VOWEL SIGN EE..GRANTHA VOWEL SIGN AI
            { Start = 0x1134B; Last = 0x1134D; Width = EastAsianWidth.OfText "N"  } // 1134B..1134D;N   # Mc     [3] GRANTHA VOWEL SIGN OO..GRANTHA SIGN VIRAMA
            { Start = 0x11350; Last = 0x11350; Width = EastAsianWidth.OfText "N"  } // 11350;N          # Lo         GRANTHA OM
            { Start = 0x11357; Last = 0x11357; Width = EastAsianWidth.OfText "N"  } // 11357;N          # Mc         GRANTHA AU LENGTH MARK
            { Start = 0x1135D; Last = 0x11361; Width = EastAsianWidth.OfText "N"  } // 1135D..11361;N   # Lo     [5] GRANTHA SIGN PLUTA..GRANTHA LETTER VOCALIC LL
            { Start = 0x11362; Last = 0x11363; Width = EastAsianWidth.OfText "N"  } // 11362..11363;N   # Mc     [2] GRANTHA VOWEL SIGN VOCALIC L..GRANTHA VOWEL SIGN VOCALIC LL
            { Start = 0x11366; Last = 0x1136C; Width = EastAsianWidth.OfText "N"  } // 11366..1136C;N   # Mn     [7] COMBINING GRANTHA DIGIT ZERO..COMBINING GRANTHA DIGIT SIX
            { Start = 0x11370; Last = 0x11374; Width = EastAsianWidth.OfText "N"  } // 11370..11374;N   # Mn     [5] COMBINING GRANTHA LETTER A..COMBINING GRANTHA LETTER PA
            { Start = 0x11400; Last = 0x11434; Width = EastAsianWidth.OfText "N"  } // 11400..11434;N   # Lo    [53] NEWA LETTER A..NEWA LETTER HA
            { Start = 0x11435; Last = 0x11437; Width = EastAsianWidth.OfText "N"  } // 11435..11437;N   # Mc     [3] NEWA VOWEL SIGN AA..NEWA VOWEL SIGN II
            { Start = 0x11438; Last = 0x1143F; Width = EastAsianWidth.OfText "N"  } // 11438..1143F;N   # Mn     [8] NEWA VOWEL SIGN U..NEWA VOWEL SIGN AI
            { Start = 0x11440; Last = 0x11441; Width = EastAsianWidth.OfText "N"  } // 11440..11441;N   # Mc     [2] NEWA VOWEL SIGN O..NEWA VOWEL SIGN AU
            { Start = 0x11442; Last = 0x11444; Width = EastAsianWidth.OfText "N"  } // 11442..11444;N   # Mn     [3] NEWA SIGN VIRAMA..NEWA SIGN ANUSVARA
            { Start = 0x11445; Last = 0x11445; Width = EastAsianWidth.OfText "N"  } // 11445;N          # Mc         NEWA SIGN VISARGA
            { Start = 0x11446; Last = 0x11446; Width = EastAsianWidth.OfText "N"  } // 11446;N          # Mn         NEWA SIGN NUKTA
            { Start = 0x11447; Last = 0x1144A; Width = EastAsianWidth.OfText "N"  } // 11447..1144A;N   # Lo     [4] NEWA SIGN AVAGRAHA..NEWA SIDDHI
            { Start = 0x1144B; Last = 0x1144F; Width = EastAsianWidth.OfText "N"  } // 1144B..1144F;N   # Po     [5] NEWA DANDA..NEWA ABBREVIATION SIGN
            { Start = 0x11450; Last = 0x11459; Width = EastAsianWidth.OfText "N"  } // 11450..11459;N   # Nd    [10] NEWA DIGIT ZERO..NEWA DIGIT NINE
            { Start = 0x1145B; Last = 0x1145B; Width = EastAsianWidth.OfText "N"  } // 1145B;N          # Po         NEWA PLACEHOLDER MARK
            { Start = 0x1145D; Last = 0x1145D; Width = EastAsianWidth.OfText "N"  } // 1145D;N          # Po         NEWA INSERTION SIGN
            { Start = 0x1145E; Last = 0x1145E; Width = EastAsianWidth.OfText "N"  } // 1145E;N          # Mn         NEWA SANDHI MARK
            { Start = 0x11480; Last = 0x114AF; Width = EastAsianWidth.OfText "N"  } // 11480..114AF;N   # Lo    [48] TIRHUTA ANJI..TIRHUTA LETTER HA
            { Start = 0x114B0; Last = 0x114B2; Width = EastAsianWidth.OfText "N"  } // 114B0..114B2;N   # Mc     [3] TIRHUTA VOWEL SIGN AA..TIRHUTA VOWEL SIGN II
            { Start = 0x114B3; Last = 0x114B8; Width = EastAsianWidth.OfText "N"  } // 114B3..114B8;N   # Mn     [6] TIRHUTA VOWEL SIGN U..TIRHUTA VOWEL SIGN VOCALIC LL
            { Start = 0x114B9; Last = 0x114B9; Width = EastAsianWidth.OfText "N"  } // 114B9;N          # Mc         TIRHUTA VOWEL SIGN E
            { Start = 0x114BA; Last = 0x114BA; Width = EastAsianWidth.OfText "N"  } // 114BA;N          # Mn         TIRHUTA VOWEL SIGN SHORT E
            { Start = 0x114BB; Last = 0x114BE; Width = EastAsianWidth.OfText "N"  } // 114BB..114BE;N   # Mc     [4] TIRHUTA VOWEL SIGN AI..TIRHUTA VOWEL SIGN AU
            { Start = 0x114BF; Last = 0x114C0; Width = EastAsianWidth.OfText "N"  } // 114BF..114C0;N   # Mn     [2] TIRHUTA SIGN CANDRABINDU..TIRHUTA SIGN ANUSVARA
            { Start = 0x114C1; Last = 0x114C1; Width = EastAsianWidth.OfText "N"  } // 114C1;N          # Mc         TIRHUTA SIGN VISARGA
            { Start = 0x114C2; Last = 0x114C3; Width = EastAsianWidth.OfText "N"  } // 114C2..114C3;N   # Mn     [2] TIRHUTA SIGN VIRAMA..TIRHUTA SIGN NUKTA
            { Start = 0x114C4; Last = 0x114C5; Width = EastAsianWidth.OfText "N"  } // 114C4..114C5;N   # Lo     [2] TIRHUTA SIGN AVAGRAHA..TIRHUTA GVANG
            { Start = 0x114C6; Last = 0x114C6; Width = EastAsianWidth.OfText "N"  } // 114C6;N          # Po         TIRHUTA ABBREVIATION SIGN
            { Start = 0x114C7; Last = 0x114C7; Width = EastAsianWidth.OfText "N"  } // 114C7;N          # Lo         TIRHUTA OM
            { Start = 0x114D0; Last = 0x114D9; Width = EastAsianWidth.OfText "N"  } // 114D0..114D9;N   # Nd    [10] TIRHUTA DIGIT ZERO..TIRHUTA DIGIT NINE
            { Start = 0x11580; Last = 0x115AE; Width = EastAsianWidth.OfText "N"  } // 11580..115AE;N   # Lo    [47] SIDDHAM LETTER A..SIDDHAM LETTER HA
            { Start = 0x115AF; Last = 0x115B1; Width = EastAsianWidth.OfText "N"  } // 115AF..115B1;N   # Mc     [3] SIDDHAM VOWEL SIGN AA..SIDDHAM VOWEL SIGN II
            { Start = 0x115B2; Last = 0x115B5; Width = EastAsianWidth.OfText "N"  } // 115B2..115B5;N   # Mn     [4] SIDDHAM VOWEL SIGN U..SIDDHAM VOWEL SIGN VOCALIC RR
            { Start = 0x115B8; Last = 0x115BB; Width = EastAsianWidth.OfText "N"  } // 115B8..115BB;N   # Mc     [4] SIDDHAM VOWEL SIGN E..SIDDHAM VOWEL SIGN AU
            { Start = 0x115BC; Last = 0x115BD; Width = EastAsianWidth.OfText "N"  } // 115BC..115BD;N   # Mn     [2] SIDDHAM SIGN CANDRABINDU..SIDDHAM SIGN ANUSVARA
            { Start = 0x115BE; Last = 0x115BE; Width = EastAsianWidth.OfText "N"  } // 115BE;N          # Mc         SIDDHAM SIGN VISARGA
            { Start = 0x115BF; Last = 0x115C0; Width = EastAsianWidth.OfText "N"  } // 115BF..115C0;N   # Mn     [2] SIDDHAM SIGN VIRAMA..SIDDHAM SIGN NUKTA
            { Start = 0x115C1; Last = 0x115D7; Width = EastAsianWidth.OfText "N"  } // 115C1..115D7;N   # Po    [23] SIDDHAM SIGN SIDDHAM..SIDDHAM SECTION MARK WITH CIRCLES AND FOUR ENCLOSURES
            { Start = 0x115D8; Last = 0x115DB; Width = EastAsianWidth.OfText "N"  } // 115D8..115DB;N   # Lo     [4] SIDDHAM LETTER THREE-CIRCLE ALTERNATE I..SIDDHAM LETTER ALTERNATE U
            { Start = 0x115DC; Last = 0x115DD; Width = EastAsianWidth.OfText "N"  } // 115DC..115DD;N   # Mn     [2] SIDDHAM VOWEL SIGN ALTERNATE U..SIDDHAM VOWEL SIGN ALTERNATE UU
            { Start = 0x11600; Last = 0x1162F; Width = EastAsianWidth.OfText "N"  } // 11600..1162F;N   # Lo    [48] MODI LETTER A..MODI LETTER LLA
            { Start = 0x11630; Last = 0x11632; Width = EastAsianWidth.OfText "N"  } // 11630..11632;N   # Mc     [3] MODI VOWEL SIGN AA..MODI VOWEL SIGN II
            { Start = 0x11633; Last = 0x1163A; Width = EastAsianWidth.OfText "N"  } // 11633..1163A;N   # Mn     [8] MODI VOWEL SIGN U..MODI VOWEL SIGN AI
            { Start = 0x1163B; Last = 0x1163C; Width = EastAsianWidth.OfText "N"  } // 1163B..1163C;N   # Mc     [2] MODI VOWEL SIGN O..MODI VOWEL SIGN AU
            { Start = 0x1163D; Last = 0x1163D; Width = EastAsianWidth.OfText "N"  } // 1163D;N          # Mn         MODI SIGN ANUSVARA
            { Start = 0x1163E; Last = 0x1163E; Width = EastAsianWidth.OfText "N"  } // 1163E;N          # Mc         MODI SIGN VISARGA
            { Start = 0x1163F; Last = 0x11640; Width = EastAsianWidth.OfText "N"  } // 1163F..11640;N   # Mn     [2] MODI SIGN VIRAMA..MODI SIGN ARDHACANDRA
            { Start = 0x11641; Last = 0x11643; Width = EastAsianWidth.OfText "N"  } // 11641..11643;N   # Po     [3] MODI DANDA..MODI ABBREVIATION SIGN
            { Start = 0x11644; Last = 0x11644; Width = EastAsianWidth.OfText "N"  } // 11644;N          # Lo         MODI SIGN HUVA
            { Start = 0x11650; Last = 0x11659; Width = EastAsianWidth.OfText "N"  } // 11650..11659;N   # Nd    [10] MODI DIGIT ZERO..MODI DIGIT NINE
            { Start = 0x11660; Last = 0x1166C; Width = EastAsianWidth.OfText "N"  } // 11660..1166C;N   # Po    [13] MONGOLIAN BIRGA WITH ORNAMENT..MONGOLIAN TURNED SWIRL BIRGA WITH DOUBLE ORNAMENT
            { Start = 0x11680; Last = 0x116AA; Width = EastAsianWidth.OfText "N"  } // 11680..116AA;N   # Lo    [43] TAKRI LETTER A..TAKRI LETTER RRA
            { Start = 0x116AB; Last = 0x116AB; Width = EastAsianWidth.OfText "N"  } // 116AB;N          # Mn         TAKRI SIGN ANUSVARA
            { Start = 0x116AC; Last = 0x116AC; Width = EastAsianWidth.OfText "N"  } // 116AC;N          # Mc         TAKRI SIGN VISARGA
            { Start = 0x116AD; Last = 0x116AD; Width = EastAsianWidth.OfText "N"  } // 116AD;N          # Mn         TAKRI VOWEL SIGN AA
            { Start = 0x116AE; Last = 0x116AF; Width = EastAsianWidth.OfText "N"  } // 116AE..116AF;N   # Mc     [2] TAKRI VOWEL SIGN I..TAKRI VOWEL SIGN II
            { Start = 0x116B0; Last = 0x116B5; Width = EastAsianWidth.OfText "N"  } // 116B0..116B5;N   # Mn     [6] TAKRI VOWEL SIGN U..TAKRI VOWEL SIGN AU
            { Start = 0x116B6; Last = 0x116B6; Width = EastAsianWidth.OfText "N"  } // 116B6;N          # Mc         TAKRI SIGN VIRAMA
            { Start = 0x116B7; Last = 0x116B7; Width = EastAsianWidth.OfText "N"  } // 116B7;N          # Mn         TAKRI SIGN NUKTA
            { Start = 0x116C0; Last = 0x116C9; Width = EastAsianWidth.OfText "N"  } // 116C0..116C9;N   # Nd    [10] TAKRI DIGIT ZERO..TAKRI DIGIT NINE
            { Start = 0x11700; Last = 0x1171A; Width = EastAsianWidth.OfText "N"  } // 11700..1171A;N   # Lo    [27] AHOM LETTER KA..AHOM LETTER ALTERNATE BA
            { Start = 0x1171D; Last = 0x1171F; Width = EastAsianWidth.OfText "N"  } // 1171D..1171F;N   # Mn     [3] AHOM CONSONANT SIGN MEDIAL LA..AHOM CONSONANT SIGN MEDIAL LIGATING RA
            { Start = 0x11720; Last = 0x11721; Width = EastAsianWidth.OfText "N"  } // 11720..11721;N   # Mc     [2] AHOM VOWEL SIGN A..AHOM VOWEL SIGN AA
            { Start = 0x11722; Last = 0x11725; Width = EastAsianWidth.OfText "N"  } // 11722..11725;N   # Mn     [4] AHOM VOWEL SIGN I..AHOM VOWEL SIGN UU
            { Start = 0x11726; Last = 0x11726; Width = EastAsianWidth.OfText "N"  } // 11726;N          # Mc         AHOM VOWEL SIGN E
            { Start = 0x11727; Last = 0x1172B; Width = EastAsianWidth.OfText "N"  } // 11727..1172B;N   # Mn     [5] AHOM VOWEL SIGN AW..AHOM SIGN KILLER
            { Start = 0x11730; Last = 0x11739; Width = EastAsianWidth.OfText "N"  } // 11730..11739;N   # Nd    [10] AHOM DIGIT ZERO..AHOM DIGIT NINE
            { Start = 0x1173A; Last = 0x1173B; Width = EastAsianWidth.OfText "N"  } // 1173A..1173B;N   # No     [2] AHOM NUMBER TEN..AHOM NUMBER TWENTY
            { Start = 0x1173C; Last = 0x1173E; Width = EastAsianWidth.OfText "N"  } // 1173C..1173E;N   # Po     [3] AHOM SIGN SMALL SECTION..AHOM SIGN RULAI
            { Start = 0x1173F; Last = 0x1173F; Width = EastAsianWidth.OfText "N"  } // 1173F;N          # So         AHOM SYMBOL VI
            { Start = 0x11800; Last = 0x1182B; Width = EastAsianWidth.OfText "N"  } // 11800..1182B;N   # Lo    [44] DOGRA LETTER A..DOGRA LETTER RRA
            { Start = 0x1182C; Last = 0x1182E; Width = EastAsianWidth.OfText "N"  } // 1182C..1182E;N   # Mc     [3] DOGRA VOWEL SIGN AA..DOGRA VOWEL SIGN II
            { Start = 0x1182F; Last = 0x11837; Width = EastAsianWidth.OfText "N"  } // 1182F..11837;N   # Mn     [9] DOGRA VOWEL SIGN U..DOGRA SIGN ANUSVARA
            { Start = 0x11838; Last = 0x11838; Width = EastAsianWidth.OfText "N"  } // 11838;N          # Mc         DOGRA SIGN VISARGA
            { Start = 0x11839; Last = 0x1183A; Width = EastAsianWidth.OfText "N"  } // 11839..1183A;N   # Mn     [2] DOGRA SIGN VIRAMA..DOGRA SIGN NUKTA
            { Start = 0x1183B; Last = 0x1183B; Width = EastAsianWidth.OfText "N"  } // 1183B;N          # Po         DOGRA ABBREVIATION SIGN
            { Start = 0x118A0; Last = 0x118DF; Width = EastAsianWidth.OfText "N"  } // 118A0..118DF;N   # L&    [64] WARANG CITI CAPITAL LETTER NGAA..WARANG CITI SMALL LETTER VIYO
            { Start = 0x118E0; Last = 0x118E9; Width = EastAsianWidth.OfText "N"  } // 118E0..118E9;N   # Nd    [10] WARANG CITI DIGIT ZERO..WARANG CITI DIGIT NINE
            { Start = 0x118EA; Last = 0x118F2; Width = EastAsianWidth.OfText "N"  } // 118EA..118F2;N   # No     [9] WARANG CITI NUMBER TEN..WARANG CITI NUMBER NINETY
            { Start = 0x118FF; Last = 0x118FF; Width = EastAsianWidth.OfText "N"  } // 118FF;N          # Lo         WARANG CITI OM
            { Start = 0x11A00; Last = 0x11A00; Width = EastAsianWidth.OfText "N"  } // 11A00;N          # Lo         ZANABAZAR SQUARE LETTER A
            { Start = 0x11A01; Last = 0x11A0A; Width = EastAsianWidth.OfText "N"  } // 11A01..11A0A;N   # Mn    [10] ZANABAZAR SQUARE VOWEL SIGN I..ZANABAZAR SQUARE VOWEL LENGTH MARK
            { Start = 0x11A0B; Last = 0x11A32; Width = EastAsianWidth.OfText "N"  } // 11A0B..11A32;N   # Lo    [40] ZANABAZAR SQUARE LETTER KA..ZANABAZAR SQUARE LETTER KSSA
            { Start = 0x11A33; Last = 0x11A38; Width = EastAsianWidth.OfText "N"  } // 11A33..11A38;N   # Mn     [6] ZANABAZAR SQUARE FINAL CONSONANT MARK..ZANABAZAR SQUARE SIGN ANUSVARA
            { Start = 0x11A39; Last = 0x11A39; Width = EastAsianWidth.OfText "N"  } // 11A39;N          # Mc         ZANABAZAR SQUARE SIGN VISARGA
            { Start = 0x11A3A; Last = 0x11A3A; Width = EastAsianWidth.OfText "N"  } // 11A3A;N          # Lo         ZANABAZAR SQUARE CLUSTER-INITIAL LETTER RA
            { Start = 0x11A3B; Last = 0x11A3E; Width = EastAsianWidth.OfText "N"  } // 11A3B..11A3E;N   # Mn     [4] ZANABAZAR SQUARE CLUSTER-FINAL LETTER YA..ZANABAZAR SQUARE CLUSTER-FINAL LETTER VA
            { Start = 0x11A3F; Last = 0x11A46; Width = EastAsianWidth.OfText "N"  } // 11A3F..11A46;N   # Po     [8] ZANABAZAR SQUARE INITIAL HEAD MARK..ZANABAZAR SQUARE CLOSING DOUBLE-LINED HEAD MARK
            { Start = 0x11A47; Last = 0x11A47; Width = EastAsianWidth.OfText "N"  } // 11A47;N          # Mn         ZANABAZAR SQUARE SUBJOINER
            { Start = 0x11A50; Last = 0x11A50; Width = EastAsianWidth.OfText "N"  } // 11A50;N          # Lo         SOYOMBO LETTER A
            { Start = 0x11A51; Last = 0x11A56; Width = EastAsianWidth.OfText "N"  } // 11A51..11A56;N   # Mn     [6] SOYOMBO VOWEL SIGN I..SOYOMBO VOWEL SIGN OE
            { Start = 0x11A57; Last = 0x11A58; Width = EastAsianWidth.OfText "N"  } // 11A57..11A58;N   # Mc     [2] SOYOMBO VOWEL SIGN AI..SOYOMBO VOWEL SIGN AU
            { Start = 0x11A59; Last = 0x11A5B; Width = EastAsianWidth.OfText "N"  } // 11A59..11A5B;N   # Mn     [3] SOYOMBO VOWEL SIGN VOCALIC R..SOYOMBO VOWEL LENGTH MARK
            { Start = 0x11A5C; Last = 0x11A83; Width = EastAsianWidth.OfText "N"  } // 11A5C..11A83;N   # Lo    [40] SOYOMBO LETTER KA..SOYOMBO LETTER KSSA
            { Start = 0x11A86; Last = 0x11A89; Width = EastAsianWidth.OfText "N"  } // 11A86..11A89;N   # Lo     [4] SOYOMBO CLUSTER-INITIAL LETTER RA..SOYOMBO CLUSTER-INITIAL LETTER SA
            { Start = 0x11A8A; Last = 0x11A96; Width = EastAsianWidth.OfText "N"  } // 11A8A..11A96;N   # Mn    [13] SOYOMBO FINAL CONSONANT SIGN G..SOYOMBO SIGN ANUSVARA
            { Start = 0x11A97; Last = 0x11A97; Width = EastAsianWidth.OfText "N"  } // 11A97;N          # Mc         SOYOMBO SIGN VISARGA
            { Start = 0x11A98; Last = 0x11A99; Width = EastAsianWidth.OfText "N"  } // 11A98..11A99;N   # Mn     [2] SOYOMBO GEMINATION MARK..SOYOMBO SUBJOINER
            { Start = 0x11A9A; Last = 0x11A9C; Width = EastAsianWidth.OfText "N"  } // 11A9A..11A9C;N   # Po     [3] SOYOMBO MARK TSHEG..SOYOMBO MARK DOUBLE SHAD
            { Start = 0x11A9D; Last = 0x11A9D; Width = EastAsianWidth.OfText "N"  } // 11A9D;N          # Lo         SOYOMBO MARK PLUTA
            { Start = 0x11A9E; Last = 0x11AA2; Width = EastAsianWidth.OfText "N"  } // 11A9E..11AA2;N   # Po     [5] SOYOMBO HEAD MARK WITH MOON AND SUN AND TRIPLE FLAME..SOYOMBO TERMINAL MARK-2
            { Start = 0x11AC0; Last = 0x11AF8; Width = EastAsianWidth.OfText "N"  } // 11AC0..11AF8;N   # Lo    [57] PAU CIN HAU LETTER PA..PAU CIN HAU GLOTTAL STOP FINAL
            { Start = 0x11C00; Last = 0x11C08; Width = EastAsianWidth.OfText "N"  } // 11C00..11C08;N   # Lo     [9] BHAIKSUKI LETTER A..BHAIKSUKI LETTER VOCALIC L
            { Start = 0x11C0A; Last = 0x11C2E; Width = EastAsianWidth.OfText "N"  } // 11C0A..11C2E;N   # Lo    [37] BHAIKSUKI LETTER E..BHAIKSUKI LETTER HA
            { Start = 0x11C2F; Last = 0x11C2F; Width = EastAsianWidth.OfText "N"  } // 11C2F;N          # Mc         BHAIKSUKI VOWEL SIGN AA
            { Start = 0x11C30; Last = 0x11C36; Width = EastAsianWidth.OfText "N"  } // 11C30..11C36;N   # Mn     [7] BHAIKSUKI VOWEL SIGN I..BHAIKSUKI VOWEL SIGN VOCALIC L
            { Start = 0x11C38; Last = 0x11C3D; Width = EastAsianWidth.OfText "N"  } // 11C38..11C3D;N   # Mn     [6] BHAIKSUKI VOWEL SIGN E..BHAIKSUKI SIGN ANUSVARA
            { Start = 0x11C3E; Last = 0x11C3E; Width = EastAsianWidth.OfText "N"  } // 11C3E;N          # Mc         BHAIKSUKI SIGN VISARGA
            { Start = 0x11C3F; Last = 0x11C3F; Width = EastAsianWidth.OfText "N"  } // 11C3F;N          # Mn         BHAIKSUKI SIGN VIRAMA
            { Start = 0x11C40; Last = 0x11C40; Width = EastAsianWidth.OfText "N"  } // 11C40;N          # Lo         BHAIKSUKI SIGN AVAGRAHA
            { Start = 0x11C41; Last = 0x11C45; Width = EastAsianWidth.OfText "N"  } // 11C41..11C45;N   # Po     [5] BHAIKSUKI DANDA..BHAIKSUKI GAP FILLER-2
            { Start = 0x11C50; Last = 0x11C59; Width = EastAsianWidth.OfText "N"  } // 11C50..11C59;N   # Nd    [10] BHAIKSUKI DIGIT ZERO..BHAIKSUKI DIGIT NINE
            { Start = 0x11C5A; Last = 0x11C6C; Width = EastAsianWidth.OfText "N"  } // 11C5A..11C6C;N   # No    [19] BHAIKSUKI NUMBER ONE..BHAIKSUKI HUNDREDS UNIT MARK
            { Start = 0x11C70; Last = 0x11C71; Width = EastAsianWidth.OfText "N"  } // 11C70..11C71;N   # Po     [2] MARCHEN HEAD MARK..MARCHEN MARK SHAD
            { Start = 0x11C72; Last = 0x11C8F; Width = EastAsianWidth.OfText "N"  } // 11C72..11C8F;N   # Lo    [30] MARCHEN LETTER KA..MARCHEN LETTER A
            { Start = 0x11C92; Last = 0x11CA7; Width = EastAsianWidth.OfText "N"  } // 11C92..11CA7;N   # Mn    [22] MARCHEN SUBJOINED LETTER KA..MARCHEN SUBJOINED LETTER ZA
            { Start = 0x11CA9; Last = 0x11CA9; Width = EastAsianWidth.OfText "N"  } // 11CA9;N          # Mc         MARCHEN SUBJOINED LETTER YA
            { Start = 0x11CAA; Last = 0x11CB0; Width = EastAsianWidth.OfText "N"  } // 11CAA..11CB0;N   # Mn     [7] MARCHEN SUBJOINED LETTER RA..MARCHEN VOWEL SIGN AA
            { Start = 0x11CB1; Last = 0x11CB1; Width = EastAsianWidth.OfText "N"  } // 11CB1;N          # Mc         MARCHEN VOWEL SIGN I
            { Start = 0x11CB2; Last = 0x11CB3; Width = EastAsianWidth.OfText "N"  } // 11CB2..11CB3;N   # Mn     [2] MARCHEN VOWEL SIGN U..MARCHEN VOWEL SIGN E
            { Start = 0x11CB4; Last = 0x11CB4; Width = EastAsianWidth.OfText "N"  } // 11CB4;N          # Mc         MARCHEN VOWEL SIGN O
            { Start = 0x11CB5; Last = 0x11CB6; Width = EastAsianWidth.OfText "N"  } // 11CB5..11CB6;N   # Mn     [2] MARCHEN SIGN ANUSVARA..MARCHEN SIGN CANDRABINDU
            { Start = 0x11D00; Last = 0x11D06; Width = EastAsianWidth.OfText "N"  } // 11D00..11D06;N   # Lo     [7] MASARAM GONDI LETTER A..MASARAM GONDI LETTER E
            { Start = 0x11D08; Last = 0x11D09; Width = EastAsianWidth.OfText "N"  } // 11D08..11D09;N   # Lo     [2] MASARAM GONDI LETTER AI..MASARAM GONDI LETTER O
            { Start = 0x11D0B; Last = 0x11D30; Width = EastAsianWidth.OfText "N"  } // 11D0B..11D30;N   # Lo    [38] MASARAM GONDI LETTER AU..MASARAM GONDI LETTER TRA
            { Start = 0x11D31; Last = 0x11D36; Width = EastAsianWidth.OfText "N"  } // 11D31..11D36;N   # Mn     [6] MASARAM GONDI VOWEL SIGN AA..MASARAM GONDI VOWEL SIGN VOCALIC R
            { Start = 0x11D3A; Last = 0x11D3A; Width = EastAsianWidth.OfText "N"  } // 11D3A;N          # Mn         MASARAM GONDI VOWEL SIGN E
            { Start = 0x11D3C; Last = 0x11D3D; Width = EastAsianWidth.OfText "N"  } // 11D3C..11D3D;N   # Mn     [2] MASARAM GONDI VOWEL SIGN AI..MASARAM GONDI VOWEL SIGN O
            { Start = 0x11D3F; Last = 0x11D45; Width = EastAsianWidth.OfText "N"  } // 11D3F..11D45;N   # Mn     [7] MASARAM GONDI VOWEL SIGN AU..MASARAM GONDI VIRAMA
            { Start = 0x11D46; Last = 0x11D46; Width = EastAsianWidth.OfText "N"  } // 11D46;N          # Lo         MASARAM GONDI REPHA
            { Start = 0x11D47; Last = 0x11D47; Width = EastAsianWidth.OfText "N"  } // 11D47;N          # Mn         MASARAM GONDI RA-KARA
            { Start = 0x11D50; Last = 0x11D59; Width = EastAsianWidth.OfText "N"  } // 11D50..11D59;N   # Nd    [10] MASARAM GONDI DIGIT ZERO..MASARAM GONDI DIGIT NINE
            { Start = 0x11D60; Last = 0x11D65; Width = EastAsianWidth.OfText "N"  } // 11D60..11D65;N   # Lo     [6] GUNJALA GONDI LETTER A..GUNJALA GONDI LETTER UU
            { Start = 0x11D67; Last = 0x11D68; Width = EastAsianWidth.OfText "N"  } // 11D67..11D68;N   # Lo     [2] GUNJALA GONDI LETTER EE..GUNJALA GONDI LETTER AI
            { Start = 0x11D6A; Last = 0x11D89; Width = EastAsianWidth.OfText "N"  } // 11D6A..11D89;N   # Lo    [32] GUNJALA GONDI LETTER OO..GUNJALA GONDI LETTER SA
            { Start = 0x11D8A; Last = 0x11D8E; Width = EastAsianWidth.OfText "N"  } // 11D8A..11D8E;N   # Mc     [5] GUNJALA GONDI VOWEL SIGN AA..GUNJALA GONDI VOWEL SIGN UU
            { Start = 0x11D90; Last = 0x11D91; Width = EastAsianWidth.OfText "N"  } // 11D90..11D91;N   # Mn     [2] GUNJALA GONDI VOWEL SIGN EE..GUNJALA GONDI VOWEL SIGN AI
            { Start = 0x11D93; Last = 0x11D94; Width = EastAsianWidth.OfText "N"  } // 11D93..11D94;N   # Mc     [2] GUNJALA GONDI VOWEL SIGN OO..GUNJALA GONDI VOWEL SIGN AU
            { Start = 0x11D95; Last = 0x11D95; Width = EastAsianWidth.OfText "N"  } // 11D95;N          # Mn         GUNJALA GONDI SIGN ANUSVARA
            { Start = 0x11D96; Last = 0x11D96; Width = EastAsianWidth.OfText "N"  } // 11D96;N          # Mc         GUNJALA GONDI SIGN VISARGA
            { Start = 0x11D97; Last = 0x11D97; Width = EastAsianWidth.OfText "N"  } // 11D97;N          # Mn         GUNJALA GONDI VIRAMA
            { Start = 0x11D98; Last = 0x11D98; Width = EastAsianWidth.OfText "N"  } // 11D98;N          # Lo         GUNJALA GONDI OM
            { Start = 0x11DA0; Last = 0x11DA9; Width = EastAsianWidth.OfText "N"  } // 11DA0..11DA9;N   # Nd    [10] GUNJALA GONDI DIGIT ZERO..GUNJALA GONDI DIGIT NINE
            { Start = 0x11EE0; Last = 0x11EF2; Width = EastAsianWidth.OfText "N"  } // 11EE0..11EF2;N   # Lo    [19] MAKASAR LETTER KA..MAKASAR ANGKA
            { Start = 0x11EF3; Last = 0x11EF4; Width = EastAsianWidth.OfText "N"  } // 11EF3..11EF4;N   # Mn     [2] MAKASAR VOWEL SIGN I..MAKASAR VOWEL SIGN U
            { Start = 0x11EF5; Last = 0x11EF6; Width = EastAsianWidth.OfText "N"  } // 11EF5..11EF6;N   # Mc     [2] MAKASAR VOWEL SIGN E..MAKASAR VOWEL SIGN O
            { Start = 0x11EF7; Last = 0x11EF8; Width = EastAsianWidth.OfText "N"  } // 11EF7..11EF8;N   # Po     [2] MAKASAR PASSIMBANG..MAKASAR END OF SECTION
            { Start = 0x12000; Last = 0x12399; Width = EastAsianWidth.OfText "N"  } // 12000..12399;N   # Lo   [922] CUNEIFORM SIGN A..CUNEIFORM SIGN U U
            { Start = 0x12400; Last = 0x1246E; Width = EastAsianWidth.OfText "N"  } // 12400..1246E;N   # Nl   [111] CUNEIFORM NUMERIC SIGN TWO ASH..CUNEIFORM NUMERIC SIGN NINE U VARIANT FORM
            { Start = 0x12470; Last = 0x12474; Width = EastAsianWidth.OfText "N"  } // 12470..12474;N   # Po     [5] CUNEIFORM PUNCTUATION SIGN OLD ASSYRIAN WORD DIVIDER..CUNEIFORM PUNCTUATION SIGN DIAGONAL QUADCOLON
            { Start = 0x12480; Last = 0x12543; Width = EastAsianWidth.OfText "N"  } // 12480..12543;N   # Lo   [196] CUNEIFORM SIGN AB TIMES NUN TENU..CUNEIFORM SIGN ZU5 TIMES THREE DISH TENU
            { Start = 0x13000; Last = 0x1342E; Width = EastAsianWidth.OfText "N"  } // 13000..1342E;N   # Lo  [1071] EGYPTIAN HIEROGLYPH A001..EGYPTIAN HIEROGLYPH AA032
            { Start = 0x14400; Last = 0x14646; Width = EastAsianWidth.OfText "N"  } // 14400..14646;N   # Lo   [583] ANATOLIAN HIEROGLYPH A001..ANATOLIAN HIEROGLYPH A530
            { Start = 0x16800; Last = 0x16A38; Width = EastAsianWidth.OfText "N"  } // 16800..16A38;N   # Lo   [569] BAMUM LETTER PHASE-A NGKUE MFON..BAMUM LETTER PHASE-F VUEQ
            { Start = 0x16A40; Last = 0x16A5E; Width = EastAsianWidth.OfText "N"  } // 16A40..16A5E;N   # Lo    [31] MRO LETTER TA..MRO LETTER TEK
            { Start = 0x16A60; Last = 0x16A69; Width = EastAsianWidth.OfText "N"  } // 16A60..16A69;N   # Nd    [10] MRO DIGIT ZERO..MRO DIGIT NINE
            { Start = 0x16A6E; Last = 0x16A6F; Width = EastAsianWidth.OfText "N"  } // 16A6E..16A6F;N   # Po     [2] MRO DANDA..MRO DOUBLE DANDA
            { Start = 0x16AD0; Last = 0x16AED; Width = EastAsianWidth.OfText "N"  } // 16AD0..16AED;N   # Lo    [30] BASSA VAH LETTER ENNI..BASSA VAH LETTER I
            { Start = 0x16AF0; Last = 0x16AF4; Width = EastAsianWidth.OfText "N"  } // 16AF0..16AF4;N   # Mn     [5] BASSA VAH COMBINING HIGH TONE..BASSA VAH COMBINING HIGH-LOW TONE
            { Start = 0x16AF5; Last = 0x16AF5; Width = EastAsianWidth.OfText "N"  } // 16AF5;N          # Po         BASSA VAH FULL STOP
            { Start = 0x16B00; Last = 0x16B2F; Width = EastAsianWidth.OfText "N"  } // 16B00..16B2F;N   # Lo    [48] PAHAWH HMONG VOWEL KEEB..PAHAWH HMONG CONSONANT CAU
            { Start = 0x16B30; Last = 0x16B36; Width = EastAsianWidth.OfText "N"  } // 16B30..16B36;N   # Mn     [7] PAHAWH HMONG MARK CIM TUB..PAHAWH HMONG MARK CIM TAUM
            { Start = 0x16B37; Last = 0x16B3B; Width = EastAsianWidth.OfText "N"  } // 16B37..16B3B;N   # Po     [5] PAHAWH HMONG SIGN VOS THOM..PAHAWH HMONG SIGN VOS FEEM
            { Start = 0x16B3C; Last = 0x16B3F; Width = EastAsianWidth.OfText "N"  } // 16B3C..16B3F;N   # So     [4] PAHAWH HMONG SIGN XYEEM NTXIV..PAHAWH HMONG SIGN XYEEM FAIB
            { Start = 0x16B40; Last = 0x16B43; Width = EastAsianWidth.OfText "N"  } // 16B40..16B43;N   # Lm     [4] PAHAWH HMONG SIGN VOS SEEV..PAHAWH HMONG SIGN IB YAM
            { Start = 0x16B44; Last = 0x16B44; Width = EastAsianWidth.OfText "N"  } // 16B44;N          # Po         PAHAWH HMONG SIGN XAUS
            { Start = 0x16B45; Last = 0x16B45; Width = EastAsianWidth.OfText "N"  } // 16B45;N          # So         PAHAWH HMONG SIGN CIM TSOV ROG
            { Start = 0x16B50; Last = 0x16B59; Width = EastAsianWidth.OfText "N"  } // 16B50..16B59;N   # Nd    [10] PAHAWH HMONG DIGIT ZERO..PAHAWH HMONG DIGIT NINE
            { Start = 0x16B5B; Last = 0x16B61; Width = EastAsianWidth.OfText "N"  } // 16B5B..16B61;N   # No     [7] PAHAWH HMONG NUMBER TENS..PAHAWH HMONG NUMBER TRILLIONS
            { Start = 0x16B63; Last = 0x16B77; Width = EastAsianWidth.OfText "N"  } // 16B63..16B77;N   # Lo    [21] PAHAWH HMONG SIGN VOS LUB..PAHAWH HMONG SIGN CIM NRES TOS
            { Start = 0x16B7D; Last = 0x16B8F; Width = EastAsianWidth.OfText "N"  } // 16B7D..16B8F;N   # Lo    [19] PAHAWH HMONG CLAN SIGN TSHEEJ..PAHAWH HMONG CLAN SIGN VWJ
            { Start = 0x16E40; Last = 0x16E7F; Width = EastAsianWidth.OfText "N"  } // 16E40..16E7F;N   # L&    [64] MEDEFAIDRIN CAPITAL LETTER M..MEDEFAIDRIN SMALL LETTER Y
            { Start = 0x16E80; Last = 0x16E96; Width = EastAsianWidth.OfText "N"  } // 16E80..16E96;N   # No    [23] MEDEFAIDRIN DIGIT ZERO..MEDEFAIDRIN DIGIT THREE ALTERNATE FORM
            { Start = 0x16E97; Last = 0x16E9A; Width = EastAsianWidth.OfText "N"  } // 16E97..16E9A;N   # Po     [4] MEDEFAIDRIN COMMA..MEDEFAIDRIN EXCLAMATION OH
            { Start = 0x16F00; Last = 0x16F44; Width = EastAsianWidth.OfText "N"  } // 16F00..16F44;N   # Lo    [69] MIAO LETTER PA..MIAO LETTER HHA
            { Start = 0x16F50; Last = 0x16F50; Width = EastAsianWidth.OfText "N"  } // 16F50;N          # Lo         MIAO LETTER NASALIZATION
            { Start = 0x16F51; Last = 0x16F7E; Width = EastAsianWidth.OfText "N"  } // 16F51..16F7E;N   # Mc    [46] MIAO SIGN ASPIRATION..MIAO VOWEL SIGN NG
            { Start = 0x16F8F; Last = 0x16F92; Width = EastAsianWidth.OfText "N"  } // 16F8F..16F92;N   # Mn     [4] MIAO TONE RIGHT..MIAO TONE BELOW
            { Start = 0x16F93; Last = 0x16F9F; Width = EastAsianWidth.OfText "N"  } // 16F93..16F9F;N   # Lm    [13] MIAO LETTER TONE-2..MIAO LETTER REFORMED TONE-8
            { Start = 0x16FE0; Last = 0x16FE1; Width = EastAsianWidth.OfText "W"  } // 16FE0..16FE1;W   # Lm     [2] TANGUT ITERATION MARK..NUSHU ITERATION MARK
            { Start = 0x17000; Last = 0x187F1; Width = EastAsianWidth.OfText "W"  } // 17000..187F1;W   # Lo  [6130] TANGUT IDEOGRAPH-17000..TANGUT IDEOGRAPH-187F1
            { Start = 0x18800; Last = 0x18AF2; Width = EastAsianWidth.OfText "W"  } // 18800..18AF2;W   # Lo   [755] TANGUT COMPONENT-001..TANGUT COMPONENT-755
            { Start = 0x1B000; Last = 0x1B0FF; Width = EastAsianWidth.OfText "W"  } // 1B000..1B0FF;W   # Lo   [256] KATAKANA LETTER ARCHAIC E..HENTAIGANA LETTER RE-2
            { Start = 0x1B100; Last = 0x1B11E; Width = EastAsianWidth.OfText "W"  } // 1B100..1B11E;W   # Lo    [31] HENTAIGANA LETTER RE-3..HENTAIGANA LETTER N-MU-MO-2
            { Start = 0x1B170; Last = 0x1B2FB; Width = EastAsianWidth.OfText "W"  } // 1B170..1B2FB;W   # Lo   [396] NUSHU CHARACTER-1B170..NUSHU CHARACTER-1B2FB
            { Start = 0x1BC00; Last = 0x1BC6A; Width = EastAsianWidth.OfText "N"  } // 1BC00..1BC6A;N   # Lo   [107] DUPLOYAN LETTER H..DUPLOYAN LETTER VOCALIC M
            { Start = 0x1BC70; Last = 0x1BC7C; Width = EastAsianWidth.OfText "N"  } // 1BC70..1BC7C;N   # Lo    [13] DUPLOYAN AFFIX LEFT HORIZONTAL SECANT..DUPLOYAN AFFIX ATTACHED TANGENT HOOK
            { Start = 0x1BC80; Last = 0x1BC88; Width = EastAsianWidth.OfText "N"  } // 1BC80..1BC88;N   # Lo     [9] DUPLOYAN AFFIX HIGH ACUTE..DUPLOYAN AFFIX HIGH VERTICAL
            { Start = 0x1BC90; Last = 0x1BC99; Width = EastAsianWidth.OfText "N"  } // 1BC90..1BC99;N   # Lo    [10] DUPLOYAN AFFIX LOW ACUTE..DUPLOYAN AFFIX LOW ARROW
            { Start = 0x1BC9C; Last = 0x1BC9C; Width = EastAsianWidth.OfText "N"  } // 1BC9C;N          # So         DUPLOYAN SIGN O WITH CROSS
            { Start = 0x1BC9D; Last = 0x1BC9E; Width = EastAsianWidth.OfText "N"  } // 1BC9D..1BC9E;N   # Mn     [2] DUPLOYAN THICK LETTER SELECTOR..DUPLOYAN DOUBLE MARK
            { Start = 0x1BC9F; Last = 0x1BC9F; Width = EastAsianWidth.OfText "N"  } // 1BC9F;N          # Po         DUPLOYAN PUNCTUATION CHINOOK FULL STOP
            { Start = 0x1BCA0; Last = 0x1BCA3; Width = EastAsianWidth.OfText "N"  } // 1BCA0..1BCA3;N   # Cf     [4] SHORTHAND FORMAT LETTER OVERLAP..SHORTHAND FORMAT UP STEP
            { Start = 0x1D000; Last = 0x1D0F5; Width = EastAsianWidth.OfText "N"  } // 1D000..1D0F5;N   # So   [246] BYZANTINE MUSICAL SYMBOL PSILI..BYZANTINE MUSICAL SYMBOL GORGON NEO KATO
            { Start = 0x1D100; Last = 0x1D126; Width = EastAsianWidth.OfText "N"  } // 1D100..1D126;N   # So    [39] MUSICAL SYMBOL SINGLE BARLINE..MUSICAL SYMBOL DRUM CLEF-2
            { Start = 0x1D129; Last = 0x1D164; Width = EastAsianWidth.OfText "N"  } // 1D129..1D164;N   # So    [60] MUSICAL SYMBOL MULTIPLE MEASURE REST..MUSICAL SYMBOL ONE HUNDRED TWENTY-EIGHTH NOTE
            { Start = 0x1D165; Last = 0x1D166; Width = EastAsianWidth.OfText "N"  } // 1D165..1D166;N   # Mc     [2] MUSICAL SYMBOL COMBINING STEM..MUSICAL SYMBOL COMBINING SPRECHGESANG STEM
            { Start = 0x1D167; Last = 0x1D169; Width = EastAsianWidth.OfText "N"  } // 1D167..1D169;N   # Mn     [3] MUSICAL SYMBOL COMBINING TREMOLO-1..MUSICAL SYMBOL COMBINING TREMOLO-3
            { Start = 0x1D16A; Last = 0x1D16C; Width = EastAsianWidth.OfText "N"  } // 1D16A..1D16C;N   # So     [3] MUSICAL SYMBOL FINGERED TREMOLO-1..MUSICAL SYMBOL FINGERED TREMOLO-3
            { Start = 0x1D16D; Last = 0x1D172; Width = EastAsianWidth.OfText "N"  } // 1D16D..1D172;N   # Mc     [6] MUSICAL SYMBOL COMBINING AUGMENTATION DOT..MUSICAL SYMBOL COMBINING FLAG-5
            { Start = 0x1D173; Last = 0x1D17A; Width = EastAsianWidth.OfText "N"  } // 1D173..1D17A;N   # Cf     [8] MUSICAL SYMBOL BEGIN BEAM..MUSICAL SYMBOL END PHRASE
            { Start = 0x1D17B; Last = 0x1D182; Width = EastAsianWidth.OfText "N"  } // 1D17B..1D182;N   # Mn     [8] MUSICAL SYMBOL COMBINING ACCENT..MUSICAL SYMBOL COMBINING LOURE
            { Start = 0x1D183; Last = 0x1D184; Width = EastAsianWidth.OfText "N"  } // 1D183..1D184;N   # So     [2] MUSICAL SYMBOL ARPEGGIATO UP..MUSICAL SYMBOL ARPEGGIATO DOWN
            { Start = 0x1D185; Last = 0x1D18B; Width = EastAsianWidth.OfText "N"  } // 1D185..1D18B;N   # Mn     [7] MUSICAL SYMBOL COMBINING DOIT..MUSICAL SYMBOL COMBINING TRIPLE TONGUE
            { Start = 0x1D18C; Last = 0x1D1A9; Width = EastAsianWidth.OfText "N"  } // 1D18C..1D1A9;N   # So    [30] MUSICAL SYMBOL RINFORZANDO..MUSICAL SYMBOL DEGREE SLASH
            { Start = 0x1D1AA; Last = 0x1D1AD; Width = EastAsianWidth.OfText "N"  } // 1D1AA..1D1AD;N   # Mn     [4] MUSICAL SYMBOL COMBINING DOWN BOW..MUSICAL SYMBOL COMBINING SNAP PIZZICATO
            { Start = 0x1D1AE; Last = 0x1D1E8; Width = EastAsianWidth.OfText "N"  } // 1D1AE..1D1E8;N   # So    [59] MUSICAL SYMBOL PEDAL MARK..MUSICAL SYMBOL KIEVAN FLAT SIGN
            { Start = 0x1D200; Last = 0x1D241; Width = EastAsianWidth.OfText "N"  } // 1D200..1D241;N   # So    [66] GREEK VOCAL NOTATION SYMBOL-1..GREEK INSTRUMENTAL NOTATION SYMBOL-54
            { Start = 0x1D242; Last = 0x1D244; Width = EastAsianWidth.OfText "N"  } // 1D242..1D244;N   # Mn     [3] COMBINING GREEK MUSICAL TRISEME..COMBINING GREEK MUSICAL PENTASEME
            { Start = 0x1D245; Last = 0x1D245; Width = EastAsianWidth.OfText "N"  } // 1D245;N          # So         GREEK MUSICAL LEIMMA
            { Start = 0x1D2E0; Last = 0x1D2F3; Width = EastAsianWidth.OfText "N"  } // 1D2E0..1D2F3;N   # No    [20] MAYAN NUMERAL ZERO..MAYAN NUMERAL NINETEEN
            { Start = 0x1D300; Last = 0x1D356; Width = EastAsianWidth.OfText "N"  } // 1D300..1D356;N   # So    [87] MONOGRAM FOR EARTH..TETRAGRAM FOR FOSTERING
            { Start = 0x1D360; Last = 0x1D378; Width = EastAsianWidth.OfText "N"  } // 1D360..1D378;N   # No    [25] COUNTING ROD UNIT DIGIT ONE..TALLY MARK FIVE
            { Start = 0x1D400; Last = 0x1D454; Width = EastAsianWidth.OfText "N"  } // 1D400..1D454;N   # L&    [85] MATHEMATICAL BOLD CAPITAL A..MATHEMATICAL ITALIC SMALL G
            { Start = 0x1D456; Last = 0x1D49C; Width = EastAsianWidth.OfText "N"  } // 1D456..1D49C;N   # L&    [71] MATHEMATICAL ITALIC SMALL I..MATHEMATICAL SCRIPT CAPITAL A
            { Start = 0x1D49E; Last = 0x1D49F; Width = EastAsianWidth.OfText "N"  } // 1D49E..1D49F;N   # Lu     [2] MATHEMATICAL SCRIPT CAPITAL C..MATHEMATICAL SCRIPT CAPITAL D
            { Start = 0x1D4A2; Last = 0x1D4A2; Width = EastAsianWidth.OfText "N"  } // 1D4A2;N          # Lu         MATHEMATICAL SCRIPT CAPITAL G
            { Start = 0x1D4A5; Last = 0x1D4A6; Width = EastAsianWidth.OfText "N"  } // 1D4A5..1D4A6;N   # Lu     [2] MATHEMATICAL SCRIPT CAPITAL J..MATHEMATICAL SCRIPT CAPITAL K
            { Start = 0x1D4A9; Last = 0x1D4AC; Width = EastAsianWidth.OfText "N"  } // 1D4A9..1D4AC;N   # Lu     [4] MATHEMATICAL SCRIPT CAPITAL N..MATHEMATICAL SCRIPT CAPITAL Q
            { Start = 0x1D4AE; Last = 0x1D4B9; Width = EastAsianWidth.OfText "N"  } // 1D4AE..1D4B9;N   # L&    [12] MATHEMATICAL SCRIPT CAPITAL S..MATHEMATICAL SCRIPT SMALL D
            { Start = 0x1D4BB; Last = 0x1D4BB; Width = EastAsianWidth.OfText "N"  } // 1D4BB;N          # Ll         MATHEMATICAL SCRIPT SMALL F
            { Start = 0x1D4BD; Last = 0x1D4C3; Width = EastAsianWidth.OfText "N"  } // 1D4BD..1D4C3;N   # Ll     [7] MATHEMATICAL SCRIPT SMALL H..MATHEMATICAL SCRIPT SMALL N
            { Start = 0x1D4C5; Last = 0x1D505; Width = EastAsianWidth.OfText "N"  } // 1D4C5..1D505;N   # L&    [65] MATHEMATICAL SCRIPT SMALL P..MATHEMATICAL FRAKTUR CAPITAL B
            { Start = 0x1D507; Last = 0x1D50A; Width = EastAsianWidth.OfText "N"  } // 1D507..1D50A;N   # Lu     [4] MATHEMATICAL FRAKTUR CAPITAL D..MATHEMATICAL FRAKTUR CAPITAL G
            { Start = 0x1D50D; Last = 0x1D514; Width = EastAsianWidth.OfText "N"  } // 1D50D..1D514;N   # Lu     [8] MATHEMATICAL FRAKTUR CAPITAL J..MATHEMATICAL FRAKTUR CAPITAL Q
            { Start = 0x1D516; Last = 0x1D51C; Width = EastAsianWidth.OfText "N"  } // 1D516..1D51C;N   # Lu     [7] MATHEMATICAL FRAKTUR CAPITAL S..MATHEMATICAL FRAKTUR CAPITAL Y
            { Start = 0x1D51E; Last = 0x1D539; Width = EastAsianWidth.OfText "N"  } // 1D51E..1D539;N   # L&    [28] MATHEMATICAL FRAKTUR SMALL A..MATHEMATICAL DOUBLE-STRUCK CAPITAL B
            { Start = 0x1D53B; Last = 0x1D53E; Width = EastAsianWidth.OfText "N"  } // 1D53B..1D53E;N   # Lu     [4] MATHEMATICAL DOUBLE-STRUCK CAPITAL D..MATHEMATICAL DOUBLE-STRUCK CAPITAL G
            { Start = 0x1D540; Last = 0x1D544; Width = EastAsianWidth.OfText "N"  } // 1D540..1D544;N   # Lu     [5] MATHEMATICAL DOUBLE-STRUCK CAPITAL I..MATHEMATICAL DOUBLE-STRUCK CAPITAL M
            { Start = 0x1D546; Last = 0x1D546; Width = EastAsianWidth.OfText "N"  } // 1D546;N          # Lu         MATHEMATICAL DOUBLE-STRUCK CAPITAL O
            { Start = 0x1D54A; Last = 0x1D550; Width = EastAsianWidth.OfText "N"  } // 1D54A..1D550;N   # Lu     [7] MATHEMATICAL DOUBLE-STRUCK CAPITAL S..MATHEMATICAL DOUBLE-STRUCK CAPITAL Y
            { Start = 0x1D552; Last = 0x1D6A5; Width = EastAsianWidth.OfText "N"  } // 1D552..1D6A5;N   # L&   [340] MATHEMATICAL DOUBLE-STRUCK SMALL A..MATHEMATICAL ITALIC SMALL DOTLESS J
            { Start = 0x1D6A8; Last = 0x1D6C0; Width = EastAsianWidth.OfText "N"  } // 1D6A8..1D6C0;N   # Lu    [25] MATHEMATICAL BOLD CAPITAL ALPHA..MATHEMATICAL BOLD CAPITAL OMEGA
            { Start = 0x1D6C1; Last = 0x1D6C1; Width = EastAsianWidth.OfText "N"  } // 1D6C1;N          # Sm         MATHEMATICAL BOLD NABLA
            { Start = 0x1D6C2; Last = 0x1D6DA; Width = EastAsianWidth.OfText "N"  } // 1D6C2..1D6DA;N   # Ll    [25] MATHEMATICAL BOLD SMALL ALPHA..MATHEMATICAL BOLD SMALL OMEGA
            { Start = 0x1D6DB; Last = 0x1D6DB; Width = EastAsianWidth.OfText "N"  } // 1D6DB;N          # Sm         MATHEMATICAL BOLD PARTIAL DIFFERENTIAL
            { Start = 0x1D6DC; Last = 0x1D6FA; Width = EastAsianWidth.OfText "N"  } // 1D6DC..1D6FA;N   # L&    [31] MATHEMATICAL BOLD EPSILON SYMBOL..MATHEMATICAL ITALIC CAPITAL OMEGA
            { Start = 0x1D6FB; Last = 0x1D6FB; Width = EastAsianWidth.OfText "N"  } // 1D6FB;N          # Sm         MATHEMATICAL ITALIC NABLA
            { Start = 0x1D6FC; Last = 0x1D714; Width = EastAsianWidth.OfText "N"  } // 1D6FC..1D714;N   # Ll    [25] MATHEMATICAL ITALIC SMALL ALPHA..MATHEMATICAL ITALIC SMALL OMEGA
            { Start = 0x1D715; Last = 0x1D715; Width = EastAsianWidth.OfText "N"  } // 1D715;N          # Sm         MATHEMATICAL ITALIC PARTIAL DIFFERENTIAL
            { Start = 0x1D716; Last = 0x1D734; Width = EastAsianWidth.OfText "N"  } // 1D716..1D734;N   # L&    [31] MATHEMATICAL ITALIC EPSILON SYMBOL..MATHEMATICAL BOLD ITALIC CAPITAL OMEGA
            { Start = 0x1D735; Last = 0x1D735; Width = EastAsianWidth.OfText "N"  } // 1D735;N          # Sm         MATHEMATICAL BOLD ITALIC NABLA
            { Start = 0x1D736; Last = 0x1D74E; Width = EastAsianWidth.OfText "N"  } // 1D736..1D74E;N   # Ll    [25] MATHEMATICAL BOLD ITALIC SMALL ALPHA..MATHEMATICAL BOLD ITALIC SMALL OMEGA
            { Start = 0x1D74F; Last = 0x1D74F; Width = EastAsianWidth.OfText "N"  } // 1D74F;N          # Sm         MATHEMATICAL BOLD ITALIC PARTIAL DIFFERENTIAL
            { Start = 0x1D750; Last = 0x1D76E; Width = EastAsianWidth.OfText "N"  } // 1D750..1D76E;N   # L&    [31] MATHEMATICAL BOLD ITALIC EPSILON SYMBOL..MATHEMATICAL SANS-SERIF BOLD CAPITAL OMEGA
            { Start = 0x1D76F; Last = 0x1D76F; Width = EastAsianWidth.OfText "N"  } // 1D76F;N          # Sm         MATHEMATICAL SANS-SERIF BOLD NABLA
            { Start = 0x1D770; Last = 0x1D788; Width = EastAsianWidth.OfText "N"  } // 1D770..1D788;N   # Ll    [25] MATHEMATICAL SANS-SERIF BOLD SMALL ALPHA..MATHEMATICAL SANS-SERIF BOLD SMALL OMEGA
            { Start = 0x1D789; Last = 0x1D789; Width = EastAsianWidth.OfText "N"  } // 1D789;N          # Sm         MATHEMATICAL SANS-SERIF BOLD PARTIAL DIFFERENTIAL
            { Start = 0x1D78A; Last = 0x1D7A8; Width = EastAsianWidth.OfText "N"  } // 1D78A..1D7A8;N   # L&    [31] MATHEMATICAL SANS-SERIF BOLD EPSILON SYMBOL..MATHEMATICAL SANS-SERIF BOLD ITALIC CAPITAL OMEGA
            { Start = 0x1D7A9; Last = 0x1D7A9; Width = EastAsianWidth.OfText "N"  } // 1D7A9;N          # Sm         MATHEMATICAL SANS-SERIF BOLD ITALIC NABLA
            { Start = 0x1D7AA; Last = 0x1D7C2; Width = EastAsianWidth.OfText "N"  } // 1D7AA..1D7C2;N   # Ll    [25] MATHEMATICAL SANS-SERIF BOLD ITALIC SMALL ALPHA..MATHEMATICAL SANS-SERIF BOLD ITALIC SMALL OMEGA
            { Start = 0x1D7C3; Last = 0x1D7C3; Width = EastAsianWidth.OfText "N"  } // 1D7C3;N          # Sm         MATHEMATICAL SANS-SERIF BOLD ITALIC PARTIAL DIFFERENTIAL
            { Start = 0x1D7C4; Last = 0x1D7CB; Width = EastAsianWidth.OfText "N"  } // 1D7C4..1D7CB;N   # L&     [8] MATHEMATICAL SANS-SERIF BOLD ITALIC EPSILON SYMBOL..MATHEMATICAL BOLD SMALL DIGAMMA
            { Start = 0x1D7CE; Last = 0x1D7FF; Width = EastAsianWidth.OfText "N"  } // 1D7CE..1D7FF;N   # Nd    [50] MATHEMATICAL BOLD DIGIT ZERO..MATHEMATICAL MONOSPACE DIGIT NINE
            { Start = 0x1D800; Last = 0x1D9FF; Width = EastAsianWidth.OfText "N"  } // 1D800..1D9FF;N   # So   [512] SIGNWRITING HAND-FIST INDEX..SIGNWRITING HEAD
            { Start = 0x1DA00; Last = 0x1DA36; Width = EastAsianWidth.OfText "N"  } // 1DA00..1DA36;N   # Mn    [55] SIGNWRITING HEAD RIM..SIGNWRITING AIR SUCKING IN
            { Start = 0x1DA37; Last = 0x1DA3A; Width = EastAsianWidth.OfText "N"  } // 1DA37..1DA3A;N   # So     [4] SIGNWRITING AIR BLOW SMALL ROTATIONS..SIGNWRITING BREATH EXHALE
            { Start = 0x1DA3B; Last = 0x1DA6C; Width = EastAsianWidth.OfText "N"  } // 1DA3B..1DA6C;N   # Mn    [50] SIGNWRITING MOUTH CLOSED NEUTRAL..SIGNWRITING EXCITEMENT
            { Start = 0x1DA6D; Last = 0x1DA74; Width = EastAsianWidth.OfText "N"  } // 1DA6D..1DA74;N   # So     [8] SIGNWRITING SHOULDER HIP SPINE..SIGNWRITING TORSO-FLOORPLANE TWISTING
            { Start = 0x1DA75; Last = 0x1DA75; Width = EastAsianWidth.OfText "N"  } // 1DA75;N          # Mn         SIGNWRITING UPPER BODY TILTING FROM HIP JOINTS
            { Start = 0x1DA76; Last = 0x1DA83; Width = EastAsianWidth.OfText "N"  } // 1DA76..1DA83;N   # So    [14] SIGNWRITING LIMB COMBINATION..SIGNWRITING LOCATION DEPTH
            { Start = 0x1DA84; Last = 0x1DA84; Width = EastAsianWidth.OfText "N"  } // 1DA84;N          # Mn         SIGNWRITING LOCATION HEAD NECK
            { Start = 0x1DA85; Last = 0x1DA86; Width = EastAsianWidth.OfText "N"  } // 1DA85..1DA86;N   # So     [2] SIGNWRITING LOCATION TORSO..SIGNWRITING LOCATION LIMBS DIGITS
            { Start = 0x1DA87; Last = 0x1DA8B; Width = EastAsianWidth.OfText "N"  } // 1DA87..1DA8B;N   # Po     [5] SIGNWRITING COMMA..SIGNWRITING PARENTHESIS
            { Start = 0x1DA9B; Last = 0x1DA9F; Width = EastAsianWidth.OfText "N"  } // 1DA9B..1DA9F;N   # Mn     [5] SIGNWRITING FILL MODIFIER-2..SIGNWRITING FILL MODIFIER-6
            { Start = 0x1DAA1; Last = 0x1DAAF; Width = EastAsianWidth.OfText "N"  } // 1DAA1..1DAAF;N   # Mn    [15] SIGNWRITING ROTATION MODIFIER-2..SIGNWRITING ROTATION MODIFIER-16
            { Start = 0x1E000; Last = 0x1E006; Width = EastAsianWidth.OfText "N"  } // 1E000..1E006;N   # Mn     [7] COMBINING GLAGOLITIC LETTER AZU..COMBINING GLAGOLITIC LETTER ZHIVETE
            { Start = 0x1E008; Last = 0x1E018; Width = EastAsianWidth.OfText "N"  } // 1E008..1E018;N   # Mn    [17] COMBINING GLAGOLITIC LETTER ZEMLJA..COMBINING GLAGOLITIC LETTER HERU
            { Start = 0x1E01B; Last = 0x1E021; Width = EastAsianWidth.OfText "N"  } // 1E01B..1E021;N   # Mn     [7] COMBINING GLAGOLITIC LETTER SHTA..COMBINING GLAGOLITIC LETTER YATI
            { Start = 0x1E023; Last = 0x1E024; Width = EastAsianWidth.OfText "N"  } // 1E023..1E024;N   # Mn     [2] COMBINING GLAGOLITIC LETTER YU..COMBINING GLAGOLITIC LETTER SMALL YUS
            { Start = 0x1E026; Last = 0x1E02A; Width = EastAsianWidth.OfText "N"  } // 1E026..1E02A;N   # Mn     [5] COMBINING GLAGOLITIC LETTER YO..COMBINING GLAGOLITIC LETTER FITA
            { Start = 0x1E800; Last = 0x1E8C4; Width = EastAsianWidth.OfText "N"  } // 1E800..1E8C4;N   # Lo   [197] MENDE KIKAKUI SYLLABLE M001 KI..MENDE KIKAKUI SYLLABLE M060 NYON
            { Start = 0x1E8C7; Last = 0x1E8CF; Width = EastAsianWidth.OfText "N"  } // 1E8C7..1E8CF;N   # No     [9] MENDE KIKAKUI DIGIT ONE..MENDE KIKAKUI DIGIT NINE
            { Start = 0x1E8D0; Last = 0x1E8D6; Width = EastAsianWidth.OfText "N"  } // 1E8D0..1E8D6;N   # Mn     [7] MENDE KIKAKUI COMBINING NUMBER TEENS..MENDE KIKAKUI COMBINING NUMBER MILLIONS
            { Start = 0x1E900; Last = 0x1E943; Width = EastAsianWidth.OfText "N"  } // 1E900..1E943;N   # L&    [68] ADLAM CAPITAL LETTER ALIF..ADLAM SMALL LETTER SHA
            { Start = 0x1E944; Last = 0x1E94A; Width = EastAsianWidth.OfText "N"  } // 1E944..1E94A;N   # Mn     [7] ADLAM ALIF LENGTHENER..ADLAM NUKTA
            { Start = 0x1E950; Last = 0x1E959; Width = EastAsianWidth.OfText "N"  } // 1E950..1E959;N   # Nd    [10] ADLAM DIGIT ZERO..ADLAM DIGIT NINE
            { Start = 0x1E95E; Last = 0x1E95F; Width = EastAsianWidth.OfText "N"  } // 1E95E..1E95F;N   # Po     [2] ADLAM INITIAL EXCLAMATION MARK..ADLAM INITIAL QUESTION MARK
            { Start = 0x1EC71; Last = 0x1ECAB; Width = EastAsianWidth.OfText "N"  } // 1EC71..1ECAB;N   # No    [59] INDIC SIYAQ NUMBER ONE..INDIC SIYAQ NUMBER PREFIXED NINE
            { Start = 0x1ECAC; Last = 0x1ECAC; Width = EastAsianWidth.OfText "N"  } // 1ECAC;N          # So         INDIC SIYAQ PLACEHOLDER
            { Start = 0x1ECAD; Last = 0x1ECAF; Width = EastAsianWidth.OfText "N"  } // 1ECAD..1ECAF;N   # No     [3] INDIC SIYAQ FRACTION ONE QUARTER..INDIC SIYAQ FRACTION THREE QUARTERS
            { Start = 0x1ECB0; Last = 0x1ECB0; Width = EastAsianWidth.OfText "N"  } // 1ECB0;N          # Sc         INDIC SIYAQ RUPEE MARK
            { Start = 0x1ECB1; Last = 0x1ECB4; Width = EastAsianWidth.OfText "N"  } // 1ECB1..1ECB4;N   # No     [4] INDIC SIYAQ NUMBER ALTERNATE ONE..INDIC SIYAQ ALTERNATE LAKH MARK
            { Start = 0x1EE00; Last = 0x1EE03; Width = EastAsianWidth.OfText "N"  } // 1EE00..1EE03;N   # Lo     [4] ARABIC MATHEMATICAL ALEF..ARABIC MATHEMATICAL DAL
            { Start = 0x1EE05; Last = 0x1EE1F; Width = EastAsianWidth.OfText "N"  } // 1EE05..1EE1F;N   # Lo    [27] ARABIC MATHEMATICAL WAW..ARABIC MATHEMATICAL DOTLESS QAF
            { Start = 0x1EE21; Last = 0x1EE22; Width = EastAsianWidth.OfText "N"  } // 1EE21..1EE22;N   # Lo     [2] ARABIC MATHEMATICAL INITIAL BEH..ARABIC MATHEMATICAL INITIAL JEEM
            { Start = 0x1EE24; Last = 0x1EE24; Width = EastAsianWidth.OfText "N"  } // 1EE24;N          # Lo         ARABIC MATHEMATICAL INITIAL HEH
            { Start = 0x1EE27; Last = 0x1EE27; Width = EastAsianWidth.OfText "N"  } // 1EE27;N          # Lo         ARABIC MATHEMATICAL INITIAL HAH
            { Start = 0x1EE29; Last = 0x1EE32; Width = EastAsianWidth.OfText "N"  } // 1EE29..1EE32;N   # Lo    [10] ARABIC MATHEMATICAL INITIAL YEH..ARABIC MATHEMATICAL INITIAL QAF
            { Start = 0x1EE34; Last = 0x1EE37; Width = EastAsianWidth.OfText "N"  } // 1EE34..1EE37;N   # Lo     [4] ARABIC MATHEMATICAL INITIAL SHEEN..ARABIC MATHEMATICAL INITIAL KHAH
            { Start = 0x1EE39; Last = 0x1EE39; Width = EastAsianWidth.OfText "N"  } // 1EE39;N          # Lo         ARABIC MATHEMATICAL INITIAL DAD
            { Start = 0x1EE3B; Last = 0x1EE3B; Width = EastAsianWidth.OfText "N"  } // 1EE3B;N          # Lo         ARABIC MATHEMATICAL INITIAL GHAIN
            { Start = 0x1EE42; Last = 0x1EE42; Width = EastAsianWidth.OfText "N"  } // 1EE42;N          # Lo         ARABIC MATHEMATICAL TAILED JEEM
            { Start = 0x1EE47; Last = 0x1EE47; Width = EastAsianWidth.OfText "N"  } // 1EE47;N          # Lo         ARABIC MATHEMATICAL TAILED HAH
            { Start = 0x1EE49; Last = 0x1EE49; Width = EastAsianWidth.OfText "N"  } // 1EE49;N          # Lo         ARABIC MATHEMATICAL TAILED YEH
            { Start = 0x1EE4B; Last = 0x1EE4B; Width = EastAsianWidth.OfText "N"  } // 1EE4B;N          # Lo         ARABIC MATHEMATICAL TAILED LAM
            { Start = 0x1EE4D; Last = 0x1EE4F; Width = EastAsianWidth.OfText "N"  } // 1EE4D..1EE4F;N   # Lo     [3] ARABIC MATHEMATICAL TAILED NOON..ARABIC MATHEMATICAL TAILED AIN
            { Start = 0x1EE51; Last = 0x1EE52; Width = EastAsianWidth.OfText "N"  } // 1EE51..1EE52;N   # Lo     [2] ARABIC MATHEMATICAL TAILED SAD..ARABIC MATHEMATICAL TAILED QAF
            { Start = 0x1EE54; Last = 0x1EE54; Width = EastAsianWidth.OfText "N"  } // 1EE54;N          # Lo         ARABIC MATHEMATICAL TAILED SHEEN
            { Start = 0x1EE57; Last = 0x1EE57; Width = EastAsianWidth.OfText "N"  } // 1EE57;N          # Lo         ARABIC MATHEMATICAL TAILED KHAH
            { Start = 0x1EE59; Last = 0x1EE59; Width = EastAsianWidth.OfText "N"  } // 1EE59;N          # Lo         ARABIC MATHEMATICAL TAILED DAD
            { Start = 0x1EE5B; Last = 0x1EE5B; Width = EastAsianWidth.OfText "N"  } // 1EE5B;N          # Lo         ARABIC MATHEMATICAL TAILED GHAIN
            { Start = 0x1EE5D; Last = 0x1EE5D; Width = EastAsianWidth.OfText "N"  } // 1EE5D;N          # Lo         ARABIC MATHEMATICAL TAILED DOTLESS NOON
            { Start = 0x1EE5F; Last = 0x1EE5F; Width = EastAsianWidth.OfText "N"  } // 1EE5F;N          # Lo         ARABIC MATHEMATICAL TAILED DOTLESS QAF
            { Start = 0x1EE61; Last = 0x1EE62; Width = EastAsianWidth.OfText "N"  } // 1EE61..1EE62;N   # Lo     [2] ARABIC MATHEMATICAL STRETCHED BEH..ARABIC MATHEMATICAL STRETCHED JEEM
            { Start = 0x1EE64; Last = 0x1EE64; Width = EastAsianWidth.OfText "N"  } // 1EE64;N          # Lo         ARABIC MATHEMATICAL STRETCHED HEH
            { Start = 0x1EE67; Last = 0x1EE6A; Width = EastAsianWidth.OfText "N"  } // 1EE67..1EE6A;N   # Lo     [4] ARABIC MATHEMATICAL STRETCHED HAH..ARABIC MATHEMATICAL STRETCHED KAF
            { Start = 0x1EE6C; Last = 0x1EE72; Width = EastAsianWidth.OfText "N"  } // 1EE6C..1EE72;N   # Lo     [7] ARABIC MATHEMATICAL STRETCHED MEEM..ARABIC MATHEMATICAL STRETCHED QAF
            { Start = 0x1EE74; Last = 0x1EE77; Width = EastAsianWidth.OfText "N"  } // 1EE74..1EE77;N   # Lo     [4] ARABIC MATHEMATICAL STRETCHED SHEEN..ARABIC MATHEMATICAL STRETCHED KHAH
            { Start = 0x1EE79; Last = 0x1EE7C; Width = EastAsianWidth.OfText "N"  } // 1EE79..1EE7C;N   # Lo     [4] ARABIC MATHEMATICAL STRETCHED DAD..ARABIC MATHEMATICAL STRETCHED DOTLESS BEH
            { Start = 0x1EE7E; Last = 0x1EE7E; Width = EastAsianWidth.OfText "N"  } // 1EE7E;N          # Lo         ARABIC MATHEMATICAL STRETCHED DOTLESS FEH
            { Start = 0x1EE80; Last = 0x1EE89; Width = EastAsianWidth.OfText "N"  } // 1EE80..1EE89;N   # Lo    [10] ARABIC MATHEMATICAL LOOPED ALEF..ARABIC MATHEMATICAL LOOPED YEH
            { Start = 0x1EE8B; Last = 0x1EE9B; Width = EastAsianWidth.OfText "N"  } // 1EE8B..1EE9B;N   # Lo    [17] ARABIC MATHEMATICAL LOOPED LAM..ARABIC MATHEMATICAL LOOPED GHAIN
            { Start = 0x1EEA1; Last = 0x1EEA3; Width = EastAsianWidth.OfText "N"  } // 1EEA1..1EEA3;N   # Lo     [3] ARABIC MATHEMATICAL DOUBLE-STRUCK BEH..ARABIC MATHEMATICAL DOUBLE-STRUCK DAL
            { Start = 0x1EEA5; Last = 0x1EEA9; Width = EastAsianWidth.OfText "N"  } // 1EEA5..1EEA9;N   # Lo     [5] ARABIC MATHEMATICAL DOUBLE-STRUCK WAW..ARABIC MATHEMATICAL DOUBLE-STRUCK YEH
            { Start = 0x1EEAB; Last = 0x1EEBB; Width = EastAsianWidth.OfText "N"  } // 1EEAB..1EEBB;N   # Lo    [17] ARABIC MATHEMATICAL DOUBLE-STRUCK LAM..ARABIC MATHEMATICAL DOUBLE-STRUCK GHAIN
            { Start = 0x1EEF0; Last = 0x1EEF1; Width = EastAsianWidth.OfText "N"  } // 1EEF0..1EEF1;N   # Sm     [2] ARABIC MATHEMATICAL OPERATOR MEEM WITH HAH WITH TATWEEL..ARABIC MATHEMATICAL OPERATOR HAH WITH DAL
            { Start = 0x1F000; Last = 0x1F003; Width = EastAsianWidth.OfText "N"  } // 1F000..1F003;N   # So     [4] MAHJONG TILE EAST WIND..MAHJONG TILE NORTH WIND
            { Start = 0x1F004; Last = 0x1F004; Width = EastAsianWidth.OfText "W"  } // 1F004;W          # So         MAHJONG TILE RED DRAGON
            { Start = 0x1F005; Last = 0x1F02B; Width = EastAsianWidth.OfText "N"  } // 1F005..1F02B;N   # So    [39] MAHJONG TILE GREEN DRAGON..MAHJONG TILE BACK
            { Start = 0x1F030; Last = 0x1F093; Width = EastAsianWidth.OfText "N"  } // 1F030..1F093;N   # So   [100] DOMINO TILE HORIZONTAL BACK..DOMINO TILE VERTICAL-06-06
            { Start = 0x1F0A0; Last = 0x1F0AE; Width = EastAsianWidth.OfText "N"  } // 1F0A0..1F0AE;N   # So    [15] PLAYING CARD BACK..PLAYING CARD KING OF SPADES
            { Start = 0x1F0B1; Last = 0x1F0BF; Width = EastAsianWidth.OfText "N"  } // 1F0B1..1F0BF;N   # So    [15] PLAYING CARD ACE OF HEARTS..PLAYING CARD RED JOKER
            { Start = 0x1F0C1; Last = 0x1F0CE; Width = EastAsianWidth.OfText "N"  } // 1F0C1..1F0CE;N   # So    [14] PLAYING CARD ACE OF DIAMONDS..PLAYING CARD KING OF DIAMONDS
            { Start = 0x1F0CF; Last = 0x1F0CF; Width = EastAsianWidth.OfText "W"  } // 1F0CF;W          # So         PLAYING CARD BLACK JOKER
            { Start = 0x1F0D1; Last = 0x1F0F5; Width = EastAsianWidth.OfText "N"  } // 1F0D1..1F0F5;N   # So    [37] PLAYING CARD ACE OF CLUBS..PLAYING CARD TRUMP-21
            { Start = 0x1F100; Last = 0x1F10A; Width = EastAsianWidth.OfText "A"  } // 1F100..1F10A;A   # No    [11] DIGIT ZERO FULL STOP..DIGIT NINE COMMA
            { Start = 0x1F10B; Last = 0x1F10C; Width = EastAsianWidth.OfText "N"  } // 1F10B..1F10C;N   # No     [2] DINGBAT CIRCLED SANS-SERIF DIGIT ZERO..DINGBAT NEGATIVE CIRCLED SANS-SERIF DIGIT ZERO
            { Start = 0x1F110; Last = 0x1F12D; Width = EastAsianWidth.OfText "A"  } // 1F110..1F12D;A   # So    [30] PARENTHESIZED LATIN CAPITAL LETTER A..CIRCLED CD
            { Start = 0x1F12E; Last = 0x1F12F; Width = EastAsianWidth.OfText "N"  } // 1F12E..1F12F;N   # So     [2] CIRCLED WZ..COPYLEFT SYMBOL
            { Start = 0x1F130; Last = 0x1F169; Width = EastAsianWidth.OfText "A"  } // 1F130..1F169;A   # So    [58] SQUARED LATIN CAPITAL LETTER A..NEGATIVE CIRCLED LATIN CAPITAL LETTER Z
            { Start = 0x1F16A; Last = 0x1F16B; Width = EastAsianWidth.OfText "N"  } // 1F16A..1F16B;N   # So     [2] RAISED MC SIGN..RAISED MD SIGN
            { Start = 0x1F170; Last = 0x1F18D; Width = EastAsianWidth.OfText "A"  } // 1F170..1F18D;A   # So    [30] NEGATIVE SQUARED LATIN CAPITAL LETTER A..NEGATIVE SQUARED SA
            { Start = 0x1F18E; Last = 0x1F18E; Width = EastAsianWidth.OfText "W"  } // 1F18E;W          # So         NEGATIVE SQUARED AB
            { Start = 0x1F18F; Last = 0x1F190; Width = EastAsianWidth.OfText "A"  } // 1F18F..1F190;A   # So     [2] NEGATIVE SQUARED WC..SQUARE DJ
            { Start = 0x1F191; Last = 0x1F19A; Width = EastAsianWidth.OfText "W"  } // 1F191..1F19A;W   # So    [10] SQUARED CL..SQUARED VS
            { Start = 0x1F19B; Last = 0x1F1AC; Width = EastAsianWidth.OfText "A"  } // 1F19B..1F1AC;A   # So    [18] SQUARED THREE D..SQUARED VOD
            { Start = 0x1F1E6; Last = 0x1F1FF; Width = EastAsianWidth.OfText "N"  } // 1F1E6..1F1FF;N   # So    [26] REGIONAL INDICATOR SYMBOL LETTER A..REGIONAL INDICATOR SYMBOL LETTER Z
            { Start = 0x1F200; Last = 0x1F202; Width = EastAsianWidth.OfText "W"  } // 1F200..1F202;W   # So     [3] SQUARE HIRAGANA HOKA..SQUARED KATAKANA SA
            { Start = 0x1F210; Last = 0x1F23B; Width = EastAsianWidth.OfText "W"  } // 1F210..1F23B;W   # So    [44] SQUARED CJK UNIFIED IDEOGRAPH-624B..SQUARED CJK UNIFIED IDEOGRAPH-914D
            { Start = 0x1F240; Last = 0x1F248; Width = EastAsianWidth.OfText "W"  } // 1F240..1F248;W   # So     [9] TORTOISE SHELL BRACKETED CJK UNIFIED IDEOGRAPH-672C..TORTOISE SHELL BRACKETED CJK UNIFIED IDEOGRAPH-6557
            { Start = 0x1F250; Last = 0x1F251; Width = EastAsianWidth.OfText "W"  } // 1F250..1F251;W   # So     [2] CIRCLED IDEOGRAPH ADVANTAGE..CIRCLED IDEOGRAPH ACCEPT
            { Start = 0x1F260; Last = 0x1F265; Width = EastAsianWidth.OfText "W"  } // 1F260..1F265;W   # So     [6] ROUNDED SYMBOL FOR FU..ROUNDED SYMBOL FOR CAI
            { Start = 0x1F300; Last = 0x1F320; Width = EastAsianWidth.OfText "W"  } // 1F300..1F320;W   # So    [33] CYCLONE..SHOOTING STAR
            { Start = 0x1F321; Last = 0x1F32C; Width = EastAsianWidth.OfText "N"  } // 1F321..1F32C;N   # So    [12] THERMOMETER..WIND BLOWING FACE
            { Start = 0x1F32D; Last = 0x1F335; Width = EastAsianWidth.OfText "W"  } // 1F32D..1F335;W   # So     [9] HOT DOG..CACTUS
            { Start = 0x1F336; Last = 0x1F336; Width = EastAsianWidth.OfText "N"  } // 1F336;N          # So         HOT PEPPER
            { Start = 0x1F337; Last = 0x1F37C; Width = EastAsianWidth.OfText "W"  } // 1F337..1F37C;W   # So    [70] TULIP..BABY BOTTLE
            { Start = 0x1F37D; Last = 0x1F37D; Width = EastAsianWidth.OfText "N"  } // 1F37D;N          # So         FORK AND KNIFE WITH PLATE
            { Start = 0x1F37E; Last = 0x1F393; Width = EastAsianWidth.OfText "W"  } // 1F37E..1F393;W   # So    [22] BOTTLE WITH POPPING CORK..GRADUATION CAP
            { Start = 0x1F394; Last = 0x1F39F; Width = EastAsianWidth.OfText "N"  } // 1F394..1F39F;N   # So    [12] HEART WITH TIP ON THE LEFT..ADMISSION TICKETS
            { Start = 0x1F3A0; Last = 0x1F3CA; Width = EastAsianWidth.OfText "W"  } // 1F3A0..1F3CA;W   # So    [43] CAROUSEL HORSE..SWIMMER
            { Start = 0x1F3CB; Last = 0x1F3CE; Width = EastAsianWidth.OfText "N"  } // 1F3CB..1F3CE;N   # So     [4] WEIGHT LIFTER..RACING CAR
            { Start = 0x1F3CF; Last = 0x1F3D3; Width = EastAsianWidth.OfText "W"  } // 1F3CF..1F3D3;W   # So     [5] CRICKET BAT AND BALL..TABLE TENNIS PADDLE AND BALL
            { Start = 0x1F3D4; Last = 0x1F3DF; Width = EastAsianWidth.OfText "N"  } // 1F3D4..1F3DF;N   # So    [12] SNOW CAPPED MOUNTAIN..STADIUM
            { Start = 0x1F3E0; Last = 0x1F3F0; Width = EastAsianWidth.OfText "W"  } // 1F3E0..1F3F0;W   # So    [17] HOUSE BUILDING..EUROPEAN CASTLE
            { Start = 0x1F3F1; Last = 0x1F3F3; Width = EastAsianWidth.OfText "N"  } // 1F3F1..1F3F3;N   # So     [3] WHITE PENNANT..WAVING WHITE FLAG
            { Start = 0x1F3F4; Last = 0x1F3F4; Width = EastAsianWidth.OfText "W"  } // 1F3F4;W          # So         WAVING BLACK FLAG
            { Start = 0x1F3F5; Last = 0x1F3F7; Width = EastAsianWidth.OfText "N"  } // 1F3F5..1F3F7;N   # So     [3] ROSETTE..LABEL
            { Start = 0x1F3F8; Last = 0x1F3FA; Width = EastAsianWidth.OfText "W"  } // 1F3F8..1F3FA;W   # So     [3] BADMINTON RACQUET AND SHUTTLECOCK..AMPHORA
            { Start = 0x1F3FB; Last = 0x1F3FF; Width = EastAsianWidth.OfText "W"  } // 1F3FB..1F3FF;W   # Sk     [5] EMOJI MODIFIER FITZPATRICK TYPE-1-2..EMOJI MODIFIER FITZPATRICK TYPE-6
            { Start = 0x1F400; Last = 0x1F43E; Width = EastAsianWidth.OfText "W"  } // 1F400..1F43E;W   # So    [63] RAT..PAW PRINTS
            { Start = 0x1F43F; Last = 0x1F43F; Width = EastAsianWidth.OfText "N"  } // 1F43F;N          # So         CHIPMUNK
            { Start = 0x1F440; Last = 0x1F440; Width = EastAsianWidth.OfText "W"  } // 1F440;W          # So         EYES
            { Start = 0x1F441; Last = 0x1F441; Width = EastAsianWidth.OfText "N"  } // 1F441;N          # So         EYE
            { Start = 0x1F442; Last = 0x1F4FC; Width = EastAsianWidth.OfText "W"  } // 1F442..1F4FC;W   # So   [187] EAR..VIDEOCASSETTE
            { Start = 0x1F4FD; Last = 0x1F4FE; Width = EastAsianWidth.OfText "N"  } // 1F4FD..1F4FE;N   # So     [2] FILM PROJECTOR..PORTABLE STEREO
            { Start = 0x1F4FF; Last = 0x1F53D; Width = EastAsianWidth.OfText "W"  } // 1F4FF..1F53D;W   # So    [63] PRAYER BEADS..DOWN-POINTING SMALL RED TRIANGLE
            { Start = 0x1F53E; Last = 0x1F54A; Width = EastAsianWidth.OfText "N"  } // 1F53E..1F54A;N   # So    [13] LOWER RIGHT SHADOWED WHITE CIRCLE..DOVE OF PEACE
            { Start = 0x1F54B; Last = 0x1F54E; Width = EastAsianWidth.OfText "W"  } // 1F54B..1F54E;W   # So     [4] KAABA..MENORAH WITH NINE BRANCHES
            { Start = 0x1F54F; Last = 0x1F54F; Width = EastAsianWidth.OfText "N"  } // 1F54F;N          # So         BOWL OF HYGIEIA
            { Start = 0x1F550; Last = 0x1F567; Width = EastAsianWidth.OfText "W"  } // 1F550..1F567;W   # So    [24] CLOCK FACE ONE OCLOCK..CLOCK FACE TWELVE-THIRTY
            { Start = 0x1F568; Last = 0x1F579; Width = EastAsianWidth.OfText "N"  } // 1F568..1F579;N   # So    [18] RIGHT SPEAKER..JOYSTICK
            { Start = 0x1F57A; Last = 0x1F57A; Width = EastAsianWidth.OfText "W"  } // 1F57A;W          # So         MAN DANCING
            { Start = 0x1F57B; Last = 0x1F594; Width = EastAsianWidth.OfText "N"  } // 1F57B..1F594;N   # So    [26] LEFT HAND TELEPHONE RECEIVER..REVERSED VICTORY HAND
            { Start = 0x1F595; Last = 0x1F596; Width = EastAsianWidth.OfText "W"  } // 1F595..1F596;W   # So     [2] REVERSED HAND WITH MIDDLE FINGER EXTENDED..RAISED HAND WITH PART BETWEEN MIDDLE AND RING FINGERS
            { Start = 0x1F597; Last = 0x1F5A3; Width = EastAsianWidth.OfText "N"  } // 1F597..1F5A3;N   # So    [13] WHITE DOWN POINTING LEFT HAND INDEX..BLACK DOWN POINTING BACKHAND INDEX
            { Start = 0x1F5A4; Last = 0x1F5A4; Width = EastAsianWidth.OfText "W"  } // 1F5A4;W          # So         BLACK HEART
            { Start = 0x1F5A5; Last = 0x1F5FA; Width = EastAsianWidth.OfText "N"  } // 1F5A5..1F5FA;N   # So    [86] DESKTOP COMPUTER..WORLD MAP
            { Start = 0x1F5FB; Last = 0x1F5FF; Width = EastAsianWidth.OfText "W"  } // 1F5FB..1F5FF;W   # So     [5] MOUNT FUJI..MOYAI
            { Start = 0x1F600; Last = 0x1F64F; Width = EastAsianWidth.OfText "W"  } // 1F600..1F64F;W   # So    [80] GRINNING FACE..PERSON WITH FOLDED HANDS
            { Start = 0x1F650; Last = 0x1F67F; Width = EastAsianWidth.OfText "N"  } // 1F650..1F67F;N   # So    [48] NORTH WEST POINTING LEAF..REVERSE CHECKER BOARD
            { Start = 0x1F680; Last = 0x1F6C5; Width = EastAsianWidth.OfText "W"  } // 1F680..1F6C5;W   # So    [70] ROCKET..LEFT LUGGAGE
            { Start = 0x1F6C6; Last = 0x1F6CB; Width = EastAsianWidth.OfText "N"  } // 1F6C6..1F6CB;N   # So     [6] TRIANGLE WITH ROUNDED CORNERS..COUCH AND LAMP
            { Start = 0x1F6CC; Last = 0x1F6CC; Width = EastAsianWidth.OfText "W"  } // 1F6CC;W          # So         SLEEPING ACCOMMODATION
            { Start = 0x1F6CD; Last = 0x1F6CF; Width = EastAsianWidth.OfText "N"  } // 1F6CD..1F6CF;N   # So     [3] SHOPPING BAGS..BED
            { Start = 0x1F6D0; Last = 0x1F6D2; Width = EastAsianWidth.OfText "W"  } // 1F6D0..1F6D2;W   # So     [3] PLACE OF WORSHIP..SHOPPING TROLLEY
            { Start = 0x1F6D3; Last = 0x1F6D4; Width = EastAsianWidth.OfText "N"  } // 1F6D3..1F6D4;N   # So     [2] STUPA..PAGODA
            { Start = 0x1F6E0; Last = 0x1F6EA; Width = EastAsianWidth.OfText "N"  } // 1F6E0..1F6EA;N   # So    [11] HAMMER AND WRENCH..NORTHEAST-POINTING AIRPLANE
            { Start = 0x1F6EB; Last = 0x1F6EC; Width = EastAsianWidth.OfText "W"  } // 1F6EB..1F6EC;W   # So     [2] AIRPLANE DEPARTURE..AIRPLANE ARRIVING
            { Start = 0x1F6F0; Last = 0x1F6F3; Width = EastAsianWidth.OfText "N"  } // 1F6F0..1F6F3;N   # So     [4] SATELLITE..PASSENGER SHIP
            { Start = 0x1F6F4; Last = 0x1F6F9; Width = EastAsianWidth.OfText "W"  } // 1F6F4..1F6F9;W   # So     [6] SCOOTER..SKATEBOARD
            { Start = 0x1F700; Last = 0x1F773; Width = EastAsianWidth.OfText "N"  } // 1F700..1F773;N   # So   [116] ALCHEMICAL SYMBOL FOR QUINTESSENCE..ALCHEMICAL SYMBOL FOR HALF OUNCE
            { Start = 0x1F780; Last = 0x1F7D8; Width = EastAsianWidth.OfText "N"  } // 1F780..1F7D8;N   # So    [89] BLACK LEFT-POINTING ISOSCELES RIGHT TRIANGLE..NEGATIVE CIRCLED SQUARE
            { Start = 0x1F800; Last = 0x1F80B; Width = EastAsianWidth.OfText "N"  } // 1F800..1F80B;N   # So    [12] LEFTWARDS ARROW WITH SMALL TRIANGLE ARROWHEAD..DOWNWARDS ARROW WITH LARGE TRIANGLE ARROWHEAD
            { Start = 0x1F810; Last = 0x1F847; Width = EastAsianWidth.OfText "N"  } // 1F810..1F847;N   # So    [56] LEFTWARDS ARROW WITH SMALL EQUILATERAL ARROWHEAD..DOWNWARDS HEAVY ARROW
            { Start = 0x1F850; Last = 0x1F859; Width = EastAsianWidth.OfText "N"  } // 1F850..1F859;N   # So    [10] LEFTWARDS SANS-SERIF ARROW..UP DOWN SANS-SERIF ARROW
            { Start = 0x1F860; Last = 0x1F887; Width = EastAsianWidth.OfText "N"  } // 1F860..1F887;N   # So    [40] WIDE-HEADED LEFTWARDS LIGHT BARB ARROW..WIDE-HEADED SOUTH WEST VERY HEAVY BARB ARROW
            { Start = 0x1F890; Last = 0x1F8AD; Width = EastAsianWidth.OfText "N"  } // 1F890..1F8AD;N   # So    [30] LEFTWARDS TRIANGLE ARROWHEAD..WHITE ARROW SHAFT WIDTH TWO THIRDS
            { Start = 0x1F900; Last = 0x1F90B; Width = EastAsianWidth.OfText "N"  } // 1F900..1F90B;N   # So    [12] CIRCLED CROSS FORMEE WITH FOUR DOTS..DOWNWARD FACING NOTCHED HOOK WITH DOT
            { Start = 0x1F910; Last = 0x1F93E; Width = EastAsianWidth.OfText "W"  } // 1F910..1F93E;W   # So    [47] ZIPPER-MOUTH FACE..HANDBALL
            { Start = 0x1F940; Last = 0x1F970; Width = EastAsianWidth.OfText "W"  } // 1F940..1F970;W   # So    [49] WILTED FLOWER..SMILING FACE WITH SMILING EYES AND THREE HEARTS
            { Start = 0x1F973; Last = 0x1F976; Width = EastAsianWidth.OfText "W"  } // 1F973..1F976;W   # So     [4] FACE WITH PARTY HORN AND PARTY HAT..FREEZING FACE
            { Start = 0x1F97A; Last = 0x1F97A; Width = EastAsianWidth.OfText "W"  } // 1F97A;W          # So         FACE WITH PLEADING EYES
            { Start = 0x1F97C; Last = 0x1F9A2; Width = EastAsianWidth.OfText "W"  } // 1F97C..1F9A2;W   # So    [39] LAB COAT..SWAN
            { Start = 0x1F9B0; Last = 0x1F9B9; Width = EastAsianWidth.OfText "W"  } // 1F9B0..1F9B9;W   # So    [10] EMOJI COMPONENT RED HAIR..SUPERVILLAIN
            { Start = 0x1F9C0; Last = 0x1F9C2; Width = EastAsianWidth.OfText "W"  } // 1F9C0..1F9C2;W   # So     [3] CHEESE WEDGE..SALT SHAKER
            { Start = 0x1F9D0; Last = 0x1F9FF; Width = EastAsianWidth.OfText "W"  } // 1F9D0..1F9FF;W   # So    [48] FACE WITH MONOCLE..NAZAR AMULET
            { Start = 0x1FA60; Last = 0x1FA6D; Width = EastAsianWidth.OfText "N"  } // 1FA60..1FA6D;N   # So    [14] XIANGQI RED GENERAL..XIANGQI BLACK SOLDIER
            { Start = 0x20000; Last = 0x2A6D6; Width = EastAsianWidth.OfText "W"  } // 20000..2A6D6;W   # Lo [42711] CJK UNIFIED IDEOGRAPH-20000..CJK UNIFIED IDEOGRAPH-2A6D6
            { Start = 0x2A6D7; Last = 0x2A6FF; Width = EastAsianWidth.OfText "W"  } // 2A6D7..2A6FF;W   # Cn    [41] <reserved-2A6D7>..<reserved-2A6FF>
            { Start = 0x2A700; Last = 0x2B734; Width = EastAsianWidth.OfText "W"  } // 2A700..2B734;W   # Lo  [4149] CJK UNIFIED IDEOGRAPH-2A700..CJK UNIFIED IDEOGRAPH-2B734
            { Start = 0x2B735; Last = 0x2B73F; Width = EastAsianWidth.OfText "W"  } // 2B735..2B73F;W   # Cn    [11] <reserved-2B735>..<reserved-2B73F>
            { Start = 0x2B740; Last = 0x2B81D; Width = EastAsianWidth.OfText "W"  } // 2B740..2B81D;W   # Lo   [222] CJK UNIFIED IDEOGRAPH-2B740..CJK UNIFIED IDEOGRAPH-2B81D
            { Start = 0x2B81E; Last = 0x2B81F; Width = EastAsianWidth.OfText "W"  } // 2B81E..2B81F;W   # Cn     [2] <reserved-2B81E>..<reserved-2B81F>
            { Start = 0x2B820; Last = 0x2CEA1; Width = EastAsianWidth.OfText "W"  } // 2B820..2CEA1;W   # Lo  [5762] CJK UNIFIED IDEOGRAPH-2B820..CJK UNIFIED IDEOGRAPH-2CEA1
            { Start = 0x2CEA2; Last = 0x2CEAF; Width = EastAsianWidth.OfText "W"  } // 2CEA2..2CEAF;W   # Cn    [14] <reserved-2CEA2>..<reserved-2CEAF>
            { Start = 0x2CEB0; Last = 0x2EBE0; Width = EastAsianWidth.OfText "W"  } // 2CEB0..2EBE0;W   # Lo  [7473] CJK UNIFIED IDEOGRAPH-2CEB0..CJK UNIFIED IDEOGRAPH-2EBE0
            { Start = 0x2EBE1; Last = 0x2F7FF; Width = EastAsianWidth.OfText "W"  } // 2EBE1..2F7FF;W   # Cn  [3103] <reserved-2EBE1>..<reserved-2F7FF>
            { Start = 0x2F800; Last = 0x2FA1D; Width = EastAsianWidth.OfText "W"  } // 2F800..2FA1D;W   # Lo   [542] CJK COMPATIBILITY IDEOGRAPH-2F800..CJK COMPATIBILITY IDEOGRAPH-2FA1D
            { Start = 0x2FA1E; Last = 0x2FA1F; Width = EastAsianWidth.OfText "W"  } // 2FA1E..2FA1F;W   # Cn     [2] <reserved-2FA1E>..<reserved-2FA1F>
            { Start = 0x2FA20; Last = 0x2FFFD; Width = EastAsianWidth.OfText "W"  } // 2FA20..2FFFD;W   # Cn  [1502] <reserved-2FA20>..<reserved-2FFFD>
            { Start = 0x30000; Last = 0x3FFFD; Width = EastAsianWidth.OfText "W"  } // 30000..3FFFD;W   # Cn [65534] <reserved-30000>..<reserved-3FFFD>
            { Start = 0xE0001; Last = 0xE0001; Width = EastAsianWidth.OfText "N"  } // E0001;N          # Cf         LANGUAGE TAG
            { Start = 0xE0020; Last = 0xE007F; Width = EastAsianWidth.OfText "N"  } // E0020..E007F;N   # Cf    [96] TAG SPACE..CANCEL TAG
            { Start = 0xE0100; Last = 0xE01EF; Width = EastAsianWidth.OfText "A"  } // E0100..E01EF;A   # Mn   [240] VARIATION SELECTOR-17..VARIATION SELECTOR-256
            { Start = 0xF0000; Last = 0xFFFFD; Width = EastAsianWidth.OfText "A"  } // F0000..FFFFD;A   # Co [65534] <private-use-F0000>..<private-use-FFFFD>
            { Start = 0x100000; Last = 0x10FFFD; Width = EastAsianWidth.OfText "A"  } // 100000..10FFFD;A # Co [65534] <private-use-100000>..<private-use-10FFFD>
        |]

    let IsInBmp codePoint = codePoint <= 0xFFFF

    let WideBmpIntervalTree, WideAstralIntervalTree = 
        let mutable rootBmp = IntervalTree.Empty
        let mutable rootAstral = IntervalTree.Empty
        let all = CreateUnicodeRangeEntries()
        for i = 0 to all.Length - 1 do 
            let current = all.[i]
            if current.Width = EastAsianWidth.Wide then
                if IsInBmp current.Last then
                    rootBmp <- rootBmp.Insert current
                else
                    rootAstral <- rootAstral.Insert current
        rootBmp, rootAstral

    let IsWideBmp codePoint = 
        match WideBmpIntervalTree.Find codePoint with
        | Some _ -> true
        | None -> false

    let IsWideAstral codePoint = 
        match WideAstralIntervalTree.Find codePoint with
        | Some _ -> true
        | None -> false

    let IsWide codePoint =
        if IsInBmp codePoint then IsWideBmp codePoint
        else IsWideAstral codePoint

