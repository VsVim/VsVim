Each section has the set of sorted commands listed alphabetically. Aliases for the same command are separated with '/' characters and are sorted by the more common alias

## Operators
These operators are supported. They work with all motions and text objects in the next paragraphs.

- c
- d
- gU
- gu
- g?
- g~
- y
- zf
- &lt;
- &gt;
- !
- =
- gq
- gw

## Motions

This is the set of motions which are currently supported listed alphabetically. Aliases for the same motion are separated with '/' characters and are sorted by the more common alias

- b / &lt;S-Left&gt; : Normal word backward
- B / &lt;C-Left&gt; : Big word backward  
- e : End of normal word
- E : End of big word 
- f : move forward to character
- F
- ge : Backward to end of word
- gE : Backward to end of WORD
- gg
- G 
- gn : operate on next match
- gN
- g* 
- g#
- g_ : Last non white space on the line 
- g0 : First character of screen line
- g^ : First visible character on line
- g$ : Last visible character on line
- h / &lt;Left&gt; / &lt;Bs&gt; / &lt;C-H&gt; : character left
- H
- j / &lt;Down&gt; / &lt;C-N&gt; / &lt;C-J&gt; : line down
- k / &lt;Up&gt; / &lt;C-P&gt;: line up
- l / &lt;Right&gt; / &lt;Space&gt; : character right
- L
- M
- n
- N
- t
- T
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
- ]]
- [[
- []
- ][
- ; 
- %
- \* 
- \# 

## Text Objects

All text objects: words, sentences, paragraphs, strings (",'), pair delimited ( [], {}, (), <>, xmltags ) are supported with their a- and i- form.

## Normal Mode 

- a
- A
- C
- cc
- dd
- D
- ga
- gd : using Goto.Definition from visual studio
- gf
- gJ
- gh
- gH
- g&lt;C-h&gt;
- gI
- gp
- gP
- gt
- gT
- gv
- gn
- gN
- gugu
- guu
- gUgU
- gUU
- g~ g~
- g~~
- g?g?
- g??
- g&
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
- R : Replace mode
- s
- S
- u
- U
- v
- V
- x
- X
- Y
- yy
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
- normal: execute key sequence in normal mode
- registers
- cwindow

## Misc

- Status line display is supported through `statusline` and `laststatus` settings, with the following caveats: `statusline` is static text (no expansion is performed) and `laststatus` only supports values of `0` (hide status line) and non-zero (show status line).








