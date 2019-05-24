Each section has the set of sorted commands listed alphabetically. Aliases for the same command are separated with '/' characters and are sorted by the more common alias

## Motions

This is the set of motions which are currently supported listed alphabetically. Aliases for the same motion are separated with '/' characters and are sorted by the more common alias

- b / &lt;S-Left&gt; : Normal word backward
- B / &lt;C-Left&gt; : Big word backward  
- e : End of normal word
- E : End of big word 
- gg
- g_ : Last non white space on the line 
- ge : Backward to end of word
- gE : Backward to end of WORD
- gn : operate on next match
- gN
- g* 
- g# 
- g0 : First character of screen line
- g^ : First visible character on line
- g$ : Last visible character on line
- G 
- h / &lt;Left&gt; / &lt;Bs&gt; / &lt;C-H&gt; : character left
- H
- j / &lt;Down&gt; / &lt;C-N&gt; / &lt;C-J&gt; : line down
- k / &lt;Up&gt; / &lt;C-P&gt;: line up
- l / &lt;Right&gt; / &lt;Space&gt; : character right
- L
- M
- n
- N
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
- ]]
- [[
- []
- ][
- ; 
- %
- \* 
- \# 

## Text Objects

All text objects (words, sentences, paragraphs, strings (",'), pair delimited ( [, {, (, <, xml )) are supported with their a- and i- form.

## Normal Mode 

- a
- A
- C
- cc
- dd
- D
- gf
- gJ
- gI
- gn
- gN
- gp
- gP
- gugu
- guu
- gUgU
- gUU
- g~ : change case motion
- g~g~
- g~~
- g?g?
- g??
- g8
- i
- I
- J
- o
- O
- p
- P
- q : macro recording
- r
- R : replace mode
- s
- S
- x
- X
- z_ : folding. most commands are supported. works only syntax based, if VS supports the filetype.
- z_ : scrolling (zt, zb, zz, ...). 
- &lt;C-w&gt;&lt;C-j&gt;
- &lt;C-w&gt;j
- &lt;C-w&gt;&lt;C-k&gt;
- &lt;C-w&gt;k
- &lt;C-w&gt;&lt;C-l&gt;
- &lt;C-w&gt;l
- &lt;C-w&gt;&lt;C-h&gt;
- &lt;C-w&gt;h
- &lt;C-w&gt;&lt;C-s&gt;
- &lt;C-w&gt;s
- &lt;C-w&gt;&lt;C-v&gt;
- &lt;C-w&gt;v
- &lt;C-w&gt;&lt;C-g&gt;&lt;C-f&gt;
- &lt;C-w&gt;gf
- &lt;Del&gt;
- .
- &lt;lt&gt;&lt;lt&gt;
- &gt;&gt;
- ==

## Marks and Registers

Most marks and registers that vim automatically manages, are supported.

## Commands

- substitute
- global (g) and converse (v)
- Ex-commands to (m)ove, (co)py, (d)elete and join lines
- practically all forms of ranges: line numbers (incl. relative), marks and pattern(!)
- mapping: all map, unmap, remap and mapclear commands (not for commandmode: cnoremap ...)
- normal: execute key sequence in normal mode
- registers
- cwindow

## Misc

- Status line display is supported through `statusline` and `laststatus` settings, with the following caveats: `statusline` is static text (no expansion is performed) and `laststatus` only supports values of `0` (hide status line) and non-zero (show status line).








