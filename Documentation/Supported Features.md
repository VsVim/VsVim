Note: Features implemented but not part of a release are prefixed with a version number such as [0.9.6] 

Each section has the set of sorted commands listed alphabetically. Aliases for the same command are separated with '/' characters and are sorted by the more common alias

## Motions

This is the set of motions which are currently supported listed alphabetically. Aliases for the same motion are separated with '/' characters and are sorted by the more common alias

- aw : A normal word
- aW : A big word
- as : A sentence 
- ap : A paragraph 
- a" / a' / a` : A quoted string
- b / &lt;S-Left&gt; : Normal word backward
- B / &lt;C-Left&gt; : Big word backward  
- e : End of normal word
- E : End of big word 
- gg
- g_ : Last non white space on the line 
- ge : Backward to end of word
- gE : Backward to end of WORD
- g* [0.9.6]
- g# [0.9.6]
- g0 : First character of screen line
- g^ : First visible character on line
- g$ : Last visible character on line
- G 
- h / &lt;Left&gt; / &lt;Bs&gt; / &lt;C-H&gt; : character left
- H
- i" / i' / i` : inner quoted string contents
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
- \* [0.9.6]
- \# [0.9.6]

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
 - gp
 - gP
 - gugu
 - guu
 - gUgU
 - gUU
 - g~g~
 - g~~
 - g?g?
 - g??
 - i
 - I
 - J
 - o
 - O
 - p
 - P
 - s
 - S
 - x
 - X
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


- Text object selections (aw, (, {,) do not properly handle white space in all cases.  Additionally they cannot be used as selection operators in Visual Mode.  This support is planned for 0.9.7.  
- Motions \*, \#, g* and g# are available as commands prior to 0.9.6 but are just not available as motions








