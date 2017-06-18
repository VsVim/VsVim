# Release Notes

### Version 2.4.1
[Issues closed in milestone 2.4.1](https://github.com/jaredpar/VsVim/milestone/47?closed=1)

Primary Issues Addressed
* `Escape` not leaving Visual Mode
* License file changed to txt from rtf

### Version 2.4.0
[Issues closed in milestone 2.4.0](https://github.com/jaredpar/VsVim/milestone/46?closed=1)

Primary Issues Addressed
* `gt` and `gT` support in VS 2017
* Exception opening files in VS 2017

### Version 2.2.0
[Issues closed in milestone 2.2.0](https://github.com/jaredpar/VsVim/milestone/44?closed=1)

Primary Issues Addressed
* Basic telemetry support
* Register issues around yank / delete 
* Key mappings and completion in insert mode

### Version 2.1.0
[Issues closed in milestone 2.1.0](https://github.com/jaredpar/VsVim/issues?q=milestone%3A2.1.0+is%3Aclosed)

Primary Issues Addressed
* Clean macro recording

Patched Issues in 2.1.1
* Many selection issues involving end of line

### Version 2.0.0
[Issues closed in milestone 2.0.0](https://github.com/jaredpar/VsVim/issues?q=milestone%3A2.0.0)

Primary Issues Addressed
* Many fixes to the block motion
* VS 2015 support 

### Version 1.8.0
[Issues closed in milestone 1.8.0](https://github.com/jaredpar/VsVim/issues?q=milestone%3A1.8.0+is%3Aclosed)

Primary Issues Addressed
* Rewrote regex replace code to support many more Vim features
* Paragraph and sentence motion fixes
* Undo bug fixes

### Version 1.7.1
[Issues closed in milestone 1.7.1](https://github.com/jaredpar/VsVim/issues?q=milestone%3A1.7.1+is%3Aclosed)

Primary Issues Addressed
* Better comment support in vimrc
* Windows clipboard stalls 
* Undo / Redo bugs 
* tag blocks

Patched Issues in 1.7.1.1
* Double click selection issue

### Version 1.7.0
[Issues closed in milestone 1.7.0](https://github.com/jaredpar/VsVim/issues?milestone=40&page=1&state=closed)

Primary Issues Addressed
* VsVim now has a proper options page (Tools -> Options) 
* VsVim is now a proper package
* <kbd>Ctrl+R</kbd> support in command line mode 
* Support for `softtabstop`
* Word wrap glyph support 

### Version 1.6.0
[Issues closed in milestone 1.6.0](https://github.com/jaredpar/VsVim/issues?milestone=39&page=1&state=closed)

Primary Issues Addressed
* Added support for `backspace` and `whichwrap`
* Major make over of select mode 
* <kbd>Ctrl+F</kbd> search window no longer conflicts with VsVim
* Better support for Peek Definition window
* Mouse clicks now supported as keyboard commands
* <kbd>Ctrl+U</kbd> in insert mode 

Patched Issues in 1.6.0.1
* Beep when selecting with mouse 

Patched Issues in 1.6.0.2
* Backspace issues over virtual space in insert mode
* Unhandled exception when using `:wq`

Patched Issues in 1.6.0.3
* Support for Dev14 

### Version 1.5.0
[Issues closed in milestone 1.5.0](https://github.com/jaredpar/VsVim/issues?milestone=38&page=1&state=closed)

Primary Issues Addressed
* Search offsets 
* <kbd>Ctrl+T</kbd> in insert mode
* <kbd>Ctrl+E</kbd> and <kbd>Ctrl+Y</kbd> scroll bugs
* End of line issues with <kbd>r</kbd>
* Support for Peek Definition Window

### Version 1.4.2
[Issues closed in milestone 1.4.2](https://github.com/jaredpar/VsVim/issues?milestone=36&page=1&state=closed)

Primary Issues Addressed
* Support for scrolloff setting
* R# integration issues
* Several block insert bugs
* :wa not saving properly in html files

### Version 1.4.1
[Issues closed in milestone 1.4.1](https://github.com/jaredpar/VsVim/issues?milestone=35&page=1&state=closed)

Primary Issues Addressed
* Block selection and tab / wide character issues
* Enter / Backspace key broken in non-edit windows
* o / O support in Visual Mode
* Display of control characters such as `^B` ([[Details|Control Character Display]])
* Height of block caret in diff view
* Perf issues around block caret drawing

### Version 1.4.0
[Issues closed in milestone 1.4.0](https://github.com/jaredpar/VsVim/issues?milestone=33&page=1&state=closed)

Primary Issues Addressed
* Basic autocmd support ([[Details|AutoCmd support]])
* Visual Studio 2012 compatibility issues
* Editing of `ex` command line
* Better support for `)` motions

### Version 1.3.3
[Issues closed in milestone 1.3.3](https://github.com/jaredpar/VsVim/issues?milestone=32&page=1&state=closed)

Primary Issues Addressed
* vimrc entries not being properly handled
* Home doesn't work in normal / visual mode
* Commenting code switches to Visual mode
* Paste to the ex command line via Ctrl-V
* VsVim doesn't stay disabled in new files

Patched Issues (1.3.3.1)
* Inconsistent loading between 2010 and 2012 due to a timing bug

Patched Issues (1.3.3.2)
* The `r` command used on tags in HTML pages caused an unhandled exception
* The `dw` command used on a blank line caused an unhandled exception
* Added `vsvim_useeditordefaults` setting to explicitly let Visual Studio defaults win

Patched Issues (1.3.3.3)
* Fixed live template support in Resharper

### Version 1.3.2
[Issues closed in milestone 1.3.2](https://github.com/jaredpar/VsVim/issues?milestone=29&page=1&state=closed)

Primary Issues Addressed
* Navigation issues with C style pragma directives
* Go to Definition causing VsVim to switch to Visual mode
* Visual Studio Dark Theme issues
* Support for `<leader>` mapping
* Several bugs around handling of `shiftwidth` and `tabstop`

### Version 1.3.1
[Issues closed in milestone 1.3.1](https://github.com/jaredpar/VsVim/issues?milestone=27&page=1&state=closed)

Primary Issues Addressed
* Non-English keyboard handling
* More key mapping fixes
* VS 2012 fixes

Patched Issues (1.3.1.1)
* AltGr handling was producing incorrect chars on certain layouts

Patched Issues (1.3.1.2)
* :wa was incorrectly saving non-dirty files
* `<C-T>` wasn't working in insert mode
* Visual Assist issues with inclusive selection

Patched Issues (1.3.1.3)
* gt, gT, tabn weren't working on 2010

### Version 1.3.0
[Issues closed in milestone 1.3](https://github.com/jaredpar/VsVim/issues?milestone=23&state=closed)

Primary Issues Addressed
* Key Handling
  * Number pad keys now act as their equivalent
  * Multiple key mapping fixes
* Better Visual Assist Support
* Mindscape Support
* Bugs present in Dev11 only installations
* C# event handler pattern '+=' doesn't go to Visual Mode
* Select Mode

Patched Issues (1.3.0.2)
* Select mode deletion added an unprintable character
* Macro playback failed when SpecFlow was also installed
* The `gk` command caused a down movement when word wrap was disabled
* Exception on startup
* Certain key mappings not working in insert mode

### Version 1.2.2
[Issues closed in milestone 1.2.2](https://github.com/jaredpar/VsVim/issues?milestone=25&state=closed)

Primary Issues Addressed

* Substitute with quotes behaved incorrectly
* Substitute at end of line behaved incorrectly
* Support for Mind Scape workbench files

### Version 1.2.1
[Issues closed in milestone 1.2.1](https://github.com/jaredpar/VsVim/issues?milestone=24&state=closed)

Primary Issues Addressed

* Support for the clipboard option
* Could not 'j' over blank lines in Visual Character Mode
* Comments were breaking vimrc loading
* Repeating commands resulted in intellisense being displayed

### Version 1.2
[Issues closed in milestone 1.2](https://github.com/jaredpar/VsVim/issues?milestone=20&state=closed)

Primary Issues Addressed

* Support for block motions
* Support for `a{text-object}` and `i{text-object}` in Visual Mode (`aB`, `a<`, etc ...)
* Support for :global command
* Support for block insertion
* Support for 'viw'.
* Support for exclusive selections in Visual Mode
* Many key mapping issues involving key modifiers and non-alpha keys
* Repeating of Enter now better handles indentation
* Continued performance tuning of :hlsearch

### Version 1.1.2
[Issues closed in milestone 1.1.2](https://github.com/jaredpar/VsVim/issues?sort=created&direction=desc&state=closed&page=1&milestone=22)

Primary Issues Addressed

* Performance of :hlseach
* Maintaining vertical caret column
* Several tabs / spaces issues including cc, caret column and repeat of 'cw',
* Tab on new line inserts tab on previous line

### Version 1.1.1
[Issues closed in milestone 1.1.1](https://github.com/jaredpar/VsVim/issues?milestone=19&sort=created&direction=desc&state=closed)

Primary Issues Addressed

* Exception thrown while using CTRL-N
* Upper case marks broken in 1.1
* Replace command not working for international characters
* Intellisense, quick info causing an exception in Visual Studio 11
* Crash when dragging the window splitter from visual mode
* CTRL-O didn't support commands that switched to visual or command mode

**Patch 1**

* Vim undo command causing too many Visual Studio undos to occur.
* VsVim not running on machines with only Visual Studio 11 installed

### Version 1.1.0
[Issues closed in milestone 1.1](https://github.com/jaredpar/VsVim/issues?sort=created&direction=desc&state=closed&page=1&milestone=10)

Primary Issues Addressed

* Insert mode largely rewritten. Better support for repeat and commands like CTRL-I, CTRL-M, CTRL-H, etc ...
* CTRL-N and CTRL-P support added.
* CTRL-A and CTRL-X support added.  Keys must be manually mapped to VsVim to avoid unexpected conflicts with select all and cut.
* Added support for the gv command
* VsVim will now preserve non-CRLF line endings
* Support added for Dev11 preview

### Version 1.0.3
[Issues closed in milestone 1.0.3](https://github.com/jaredpar/VsVim/issues?milestone=17&state=closed)

Primary Issues Addressed

* Shift-C was placing the caret in the incorrect position.
* End key was cancelling Visual Mode
* Enter and Back were causing issues with R# in certain scenarios

### Version 1.0.2
[Issues closed in milestone 1.0.2](https://github.com/jaredpar/VsVim/issues?milestone=16&state=closed)

Primary Issues Addressed

* Crash which occurred after closing tabs.  Most often repro'd when combined with Pro Power Tools
* Key mappings which contained multiple inputs didn't behave properly in insert mode
