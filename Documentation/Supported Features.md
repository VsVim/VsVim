Each section has the set of sorted commands listed alphabetically. Aliases for the same command are separated with '/' characters and are sorted by the more common alias

## Operators
These operators are supported. They work with all motions and text objects in the next paragraphs.

- c : Change (replace text between motion or object)
- d : Delete (delete text between motion or object)
- gU : Uppercase (uppercase text between motion or object)
- gu : Lowercase (lowercase text between motion or object)
- g? : Rot13 (encode text in rot13 between motion or object)
- g~ : Swap case (invert case of text between motion or object)
- y : Yank (copy text between motion or object)
- zf : Fold (create fold based on motion)
- &lt; : Shift left (indent text to the left based on motion)
- &gt; : Shift right (indent text to the right based on motion)
- ! : Filter (run an external command on the text selected by motion)
- = : Format (auto-indent the text based on motion)
- gq : Format text (format based on the motion or text object)
- gw : Wrap lines (wrap the lines of text based on the motion)

## Motions

This is the set of motions which are currently supported listed alphabetically. Aliases for the same motion are separated with '/' characters and are sorted by the more common alias

- b / &lt;S-Left&gt; : Normal word backward
- B / &lt;C-Left&gt; : Big word backward  
- e : End of normal word
- E : End of big word 
- f : move forward to character
- F : Move backward to a specific character in the line
- ge : Backward to end of word
- gE : Backward to end of WORD
- gg : Go to the top of the file
- G : Go to the bottom of the file
- gn : operate on next match
- gN : Operate on the previous search match
- g* : Search for the word under the cursor forward
- g# : Search for the word under the cursor backward
- g_ : Last non white space on the line 
- g0 : First character of screen line
- g^ : First visible character on line
- g$ : Last visible character on line
- h / &lt;Left&gt; / &lt;Bs&gt; / &lt;C-H&gt; : character left
- H : Go to the top of the screen
- j / &lt;Down&gt; / &lt;C-N&gt; / &lt;C-J&gt; : line down
- k / &lt;Up&gt; / &lt;C-P&gt;: line up
- l / &lt;Right&gt; / &lt;Space&gt; : character right
- L : Go to the bottom of the screen
- M : Go to the middle of the screen
- n : Repeat the last search forward
- N : Repeat the last search backward
- t : Move forward until character (stops just before the character)
- T : Move backward until character (stops just after the character)
- v : modifier to a motion to act as in visual mode. Together with operator. Example: dvb.
- w / &lt;S-Right&gt; : Normal Word 
- W / &lt;C-Right&gt; : Big Word
- 0 : Beginning of the line
- $ / &lt;End&gt; : End of line 
- ^ : First non-white space on line
- + / &lt;C-M&gt; : Line down to first non white space
- _ : First non-white space on line (with count)
- ( : Sentence backward 
- ) : Sentence forward 
- { : Paragraph backward
- } : Paragraph forward
- ]] : Jump to next section
- [[ : Jump to previous section
- [] : Move to previous function (or block)
- ][ : Move to next function (or block)
- ; : Repeat the last f, F, t, or T command forward
- % : Jump between matching brackets, parentheses, or tags
- \* : Search forward for the word under the cursor
- \# : Search backward for the word under the cursor

## Text Objects

All text objects: words, sentences, paragraphs, strings (",'), pair delimited ( [], {}, (), <>, xmltags ) are supported with their a- and i- form.

## Normal Mode 

- a : Append after the cursor
- A : Append at the end of the line
- C : Change to the end of the line
- cc : Change the entire line
- dd : Delete the entire line
- D : Delete from cursor to the end of the line
- ga : Display character under cursor (ASCII or Unicode)
- gd : using Goto.Definition from visual studio
- gf : Go to the file under the cursor
- gJ : Join lines without space
- gh : Insert text at the beginning of the line (ignores leading whitespace)
- gH 
- g&lt;C-h&gt;
- gI : Insert text at the beginning of the line (ignores leading whitespace)
- gp : Put (paste) text after the cursor (without moving cursor)
- gP : Put (paste) text before the cursor (without moving cursor)
- gt : Go to the next tab
- gT : Go to the previous tab
- gv : Reselect the last visual selection
- gn 
- gN
- gugu 
- guu : Lowercase the entire line
- gUgU 
- gUU : Swap case for the entire line
- g~ g~ : Swap case for the entire line
- g~~
- g?g? : Encode the entire line in rot13
- g?? 
- g& : Repeat the last substitute command
- g8
- i : Insert text before the cursor
- I : Insert text at the beginning of the line
- J : Join the next line with the current line
- o : Open a new line below the current line
- O : Open a new line above the current line
- p : Paste after the cursor
- P : Paste before the cursor
- q : macro recording
- r : Replace the character under the cursor
- R : Replace mode
- s : Substitute one character
- S : Substitute the entire line
- u : Undo the last change
- U : Redo (undo all changes on the line)
- v : Visual mode
- V : Visual line mode
- x : Delete the character under the cursor
- X : Delete the character before the cursor
- Y : Yank the entire line
- yy : Yank the entire line
- za
- zA
- zo
- zO
- zc
- zC
- zd
- zD
- zE
- zF
- zM
- zR
- ZQ
- ZZ
- &lt;C-a&gt;
- &lt;C-g&gt;
- &lt;C-i&gt;
- &lt;C-o&gt;
- &lt;C-PageDown&gt;
- &lt;C-PageUp&gt;
- &lt;C-q&gt;
- &lt;C-r&gt;
- &lt;C-v&gt;
- &lt;C-w&gt;&lt;C-c&gt;
- &lt;C-w&gt;c
- &lt;C-w&gt;&lt;C-j&gt;
- &lt;C-w&gt;j
- &lt;C-w&gt;&lt;C-Down&gt;
- &lt;C-w&gt;&lt;Down&gt;
- &lt;C-w&gt;&lt;C-k&gt;
- &lt;C-w&gt;k
- &lt;C-w&gt;&lt;C-Up&gt;
- &lt;C-w&gt;&lt;Up&gt;
- &lt;C-w&gt;&lt;C-l&gt;
- &lt;C-w&gt;l
- &lt;C-w&gt;&lt;C-Right&gt;
- &lt;C-w&gt;&lt;Right&gt;
- &lt;C-w&gt;&lt;C-h&gt;
- &lt;C-w&gt;h
- &lt;C-w&gt;&lt;C-Left&gt;
- &lt;C-w&gt;&lt;Left&gt;
- &lt;C-w&gt;J
- &lt;C-w&gt;K
- &lt;C-w&gt;L
- &lt;C-w&gt;H
- &lt;C-w&gt;&lt;C-t&gt;
- &lt;C-w&gt;t
- &lt;C-w&gt;&lt;C-b&gt;
- &lt;C-w&gt;b
- &lt;C-w&gt;&lt;C-p&gt;
- &lt;C-w&gt;p
- &lt;C-w&gt;&lt;C-w&gt;
- &lt;C-w&gt;w
- &lt;C-w&gt;W
- &lt;C-w&gt;&lt;C-s&gt;
- &lt;C-w&gt;s
- &lt;C-w&gt;S
- &lt;C-w&gt;&lt;C-v&gt;
- &lt;C-w&gt;v
- &lt;C-w&gt;&lt;C-g&gt;&lt;C-f&gt;
- &lt;C-w&gt;gf
- &lt;C-x&gt;
- &lt;C-]&gt;
- &lt;Del&gt;
- [p
- [P
- ]p
- ]P
- &
- .
- &lt;&lt;
- &gt;&gt;
- ==
- gqgq
- gqq
- gwgw
- gww
- !!
- :
- &lt;C-^&gt;

## Marks
Local (small letter) and global marks are supported (linewise and exact character). Movements to marks can be used with operators.
Most marks that vim automatically manages, are supported.

## Registers
Named registers are supported. Append with uppercase letters is supported. Recording to and executing registers as macros is supported.
Most registers that vim automatically manages, are supported.

## Commands

- substitute
- global (g) and converse (v)
- Ex-commands to (m)ove, (co)py, (d)elete and join lines
- practically all forms of ranges: line numbers (incl. relative), marks and pattern(!)
- mapping: all map, unmap, remap and mapclear commands (not for commandmode: cnoremap ...)
- abbreviations
- normal: execute key sequence in normal mode
- registers
- cwindow

## Misc

- Status line display is supported through `statusline` and `laststatus` settings, with the following caveats: `statusline` is static text (no expansion is performed) and `laststatus` only supports values of `0` (hide status line) and non-zero (show status line).








