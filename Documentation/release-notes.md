# Release Notes

### Version 2.8.0
[Issues closed in 2.8.0 Milestone](https://github.com/VsVim/VsVim/milestone/49?closed=1)
Features
* Integrated multiple caret support #2656
* Interact with multi-caret selections #2362
* Support 'isident' option #2645
* Support 'iskeyword' option #1823

Primary Issues Addressed
* Making a list of numbers, adds the empty line #2718 
* Yanking with ':set clipboard=unnamed' misplaces the caret #2735
* Caret is misplaced in lines that display codelens bug #2709
* VsVim causes UI delay in Navigate To provider bug #2715
* Prevent displaying a line number for the phantom line #2687
* Using 'f', 't', 'F', 'T' to find multi-byte characters fails #2702
* Paste of multi-line visual block selection inserts into one line #2694
* Visual Assist X and "cf<some char>" repitition #2692
* Mouse select and drag does not scroll #2661
* visual block prepend/append (C-V, I) on empty lines will skip last line #2675
* Giant caret with inline breakpoint conditions dialog #2649
* Development builds inadvertently being published to the Open VSIX Gallery project #2685
* Mapping <Left> produces invalid characters #2683
* Support command line cursor movement by mappings #1103
* Pressing 'A' when in Visual Block mode moves caret to wrong position #2667
* Highlighting text with the mouse in split view causes the other view to move #2664
* VsVim should not process external caret movements when disabled #2643
* F1 to open MSDN documentation only works in insert mode #2671
* Caret sized incorrectly at end of line following Code Lens annotation Visual Studio 2019 #2657
* VS 2019 : Exception on start #2646
* VsVim fell for the trap some users fall for: <C-w> <C-c> #2623
* Support initiating find in files and navigating find results using ':vimgrep' #2641
* Implement :cr[ewind] #2637
* Exception encountered in visual selection tracker #2618
* TargetInvocationException while editing at the bottom of a git diff #2640
* Command margin focus problems #2625
* Incremental search as a motion from visual mode misbehaves #2617
* Strange intermittent hybrid focus state #2627
* Relative line numbers not updated correctly for large caret jumps #2634
* Block caret overlay misaligned with underlying text #2615
* Macro leaves text object selection behavior out of sync with cursor position #2632
* Problem with Completion (C-n) in VS2017: Picking completion with space #2619
* Window settings specified in the modeline are not applied #2613
* Caret position changed handler running before layout is complete #2629
* Map command does not display all mappings #2612
* <ESC> in insert mode in inline rename is exiting inline rename #2587
* Implement nowrap-specific cursor and scroll commands #787
* VsVim copy does not include syntax highlighting #1920
* Toggle all folds generates an exception when there are no folds #2607
* Add a UI option to disable VsVim #1545
* Imap to delete <del> does not work (anymore) #2608
* Host command does not allow double quotes #2622
* Chaining _vsvimrc to _vimrc to *real* _vimrc #1355
* Go up / down a line in insert mode and reset the caret #2596
* VS2017 crash : GetTextViewLineRelativeToPoint #2601
* Possible exception calling Caret.ContainingTextViewLine #2583
* Combine the benefits of atomic insert and effective change #2593
* System.ArgumentOutOfRangeException when pasting in VS2019 #2591
* Clicking past end of file doesn't move cursor to end of file anymore #2586
* hlsearch highlighting doesn't match search matches because of universe negation #1471
* v2.7.0.0 - vimrc errors reported despite setting being off #2585
* Selection not cleared after control-click to go to #2580

### Version 2.7.0
[Issues closed in 2.7.0 Milestone](https://github.com/VsVim/VsVim/milestone/48)
Primary Issues Addressed
* % motion failing if there's an ' on the same line as a match
* Unit tests pause for several minutes after completing
* Format paragraph does not respect unix line endings
* Display the '^' character when entering literal characters
* Vim help files use the user's tab settings rather than those specified in the files
* Loading a file doesn't scroll the new window correctly unless 'scrolloff' is set
* Improve help
* System.ArgumentException in VB.Net
* Search behavior has changed
* In C++, CTRL-] opens the Find Symbol Results panel instead of going to the definition directly.
* Runtime exception on file close
* Number increment (Ctrl+A) jumps over decimal numbers to next hex number
* Status messages do not appear in the trace log
* Reduce the volume of output in the trace window
* Ctrl+Click does not follow link in spite of tooltip
* Visual Studio 2019 always Display \"Welcome to Vim\"
* verbatim strings break block commands di) da)
* CTRL+A in blockwise-visual mode increments numbers not in selection
* suddenly today VS2019 cannot start vsvim 
* CleanVsix no longer runs on appveyor
* Publishing to the vsixgallery fails
* The 'retab' command mangles non-leading tabs
* Backspace issue in peek definition window
* Increasing / Decreasing a number does not work within words
* Append yanked line does not append to unnamed register
* Deleting by line to the end of the buffer leaves caret on phantom line
* map visual studio command ExpandSelectiontoContainingBlock
* Detecting 2017 vs 2019 in vsvimrc
* Neither Ctrl-V not Ctrl-Q quote Ctrl characters or the escape key
* <C-N> (word completion) exception in VS 2019
* Make incremental search asynchronous
* inconsistent Ctrl-ú (Ctrl-[) handling in different modes (Czech keyboard)
* Echoing environment variables
* Searching with /, then pressing Ctrl-A and typing FOO inserts /O/O/F
* CTRL-W Window navigaton bug
* CTRL + P completion list in wrong order
* Command Mode should respect words
* Can't press <Esc> to exit F2-rename window
* What the setting Hide Marks Value
* Delay before showing insertion caret after 'o' command
* Parameter name: span was out of range of valid values [vs2015/2.6.0.0] 
* Inserting tabs in block selection still does not work properly
* markers margin
* normal mode across all files
* Remove VS2012 and VS2013 support
* Bug: t<char>  followed by ; does not go to the next match
* Double-click to select word then drag doesn't continue to select whole words
* Commands issued as part of a mapping should not be recorded in the command history
* map nn <S-$> does not work
* Insert mode unicode input
* CompositionFailedException: This part (Vim.Vim) cannot be instantiated
* Commands sometimes appear in the source code 
* Crash in DisplayLineDown
* vs2017, set clipboard=unnamed,vs encounter an exception
* Weird behavior after clicking into an escape sequence
* VsVim is active in Powershell Interactive but probably should'nt be
* Half page navigation using <C-D>,<C-U> is very slow
* Highlighting in Insert Mode goes to Visual Mode, but goes back to Command Mode
* Show line numbers in parse errors
* Support vim modelines

### Version 2.6.0
[Issues closed in milestone 2.6.0](https://github.com/VsVim/VsVim/milestone/45?closed=1)
Primary Issues Addressed
* Visual Studio 2019 support
* Mark display in the editor margin
* Significantly improved surrogate pair support
* In total nearly 150 issues were addressed in this release leading to improvements across the board


### Version 2.5.0
[Issues closed in milestone 2.5.0](https://github.com/VsVim/VsVim/milestone/47?closed=1)

Primary Issues Addressed
* Support for the `.` register 
* Several bugs around `.` command repeating
* Stopped erasing error information from status bar 
* Lots of infrastructure issues 

### Version 2.4.1
[Issues closed in milestone 2.4.1](https://github.com/VsVim/VsVim/milestone/47?closed=1)

Primary Issues Addressed
* `Escape` not leaving Visual Mode
* License file changed to txt from rtf

### Version 2.4.0
[Issues closed in milestone 2.4.0](https://github.com/VsVim/VsVim/milestone/46?closed=1)

Primary Issues Addressed
* `gt` and `gT` support in VS 2017
* Exception opening files in VS 2017

### Version 2.2.0
[Issues closed in milestone 2.2.0](https://github.com/VsVim/VsVim/milestone/44?closed=1)

Primary Issues Addressed
* Basic telemetry support
* Register issues around yank / delete 
* Key mappings and completion in insert mode

### Version 2.1.0
[Issues closed in milestone 2.1.0](https://github.com/VsVim/VsVim/issues?q=milestone%3A2.1.0+is%3Aclosed)

Primary Issues Addressed
* Clean macro recording

Patched Issues in 2.1.1
* Many selection issues involving end of line

### Version 2.0.0
[Issues closed in milestone 2.0.0](https://github.com/VsVim/VsVim/issues?q=milestone%3A2.0.0)

Primary Issues Addressed
* Many fixes to the block motion
* VS 2015 support 

### Version 1.8.0
[Issues closed in milestone 1.8.0](https://github.com/VsVim/VsVim/issues?q=milestone%3A1.8.0+is%3Aclosed)

Primary Issues Addressed
* Rewrote regex replace code to support many more Vim features
* Paragraph and sentence motion fixes
* Undo bug fixes

### Version 1.7.1
[Issues closed in milestone 1.7.1](https://github.com/VsVim/VsVim/issues?q=milestone%3A1.7.1+is%3Aclosed)

Primary Issues Addressed
* Better comment support in vimrc
* Windows clipboard stalls 
* Undo / Redo bugs 
* tag blocks

Patched Issues in 1.7.1.1
* Double click selection issue

### Version 1.7.0
[Issues closed in milestone 1.7.0](https://github.com/VsVim/VsVim/issues?milestone=40&page=1&state=closed)

Primary Issues Addressed
* VsVim now has a proper options page (Tools -> Options) 
* VsVim is now a proper package
* <kbd>Ctrl+R</kbd> support in command line mode 
* Support for `softtabstop`
* Word wrap glyph support 

### Version 1.6.0
[Issues closed in milestone 1.6.0](https://github.com/VsVim/VsVim/issues?milestone=39&page=1&state=closed)

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
[Issues closed in milestone 1.5.0](https://github.com/VsVim/VsVim/issues?milestone=38&page=1&state=closed)

Primary Issues Addressed
* Search offsets 
* <kbd>Ctrl+T</kbd> in insert mode
* <kbd>Ctrl+E</kbd> and <kbd>Ctrl+Y</kbd> scroll bugs
* End of line issues with <kbd>r</kbd>
* Support for Peek Definition Window

### Version 1.4.2
[Issues closed in milestone 1.4.2](https://github.com/VsVim/VsVim/issues?milestone=36&page=1&state=closed)

Primary Issues Addressed
* Support for scrolloff setting
* R# integration issues
* Several block insert bugs
* :wa not saving properly in html files

### Version 1.4.1
[Issues closed in milestone 1.4.1](https://github.com/VsVim/VsVim/issues?milestone=35&page=1&state=closed)

Primary Issues Addressed
* Block selection and tab / wide character issues
* Enter / Backspace key broken in non-edit windows
* o / O support in Visual Mode
* Display of control characters such as `^B` ([[Details|Control Character Display]])
* Height of block caret in diff view
* Perf issues around block caret drawing

### Version 1.4.0
[Issues closed in milestone 1.4.0](https://github.com/VsVim/VsVim/issues?milestone=33&page=1&state=closed)

Primary Issues Addressed
* Basic autocmd support ([[Details|AutoCmd support]])
* Visual Studio 2012 compatibility issues
* Editing of `ex` command line
* Better support for `)` motions

### Version 1.3.3
[Issues closed in milestone 1.3.3](https://github.com/VsVim/VsVim/issues?milestone=32&page=1&state=closed)

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
[Issues closed in milestone 1.3.2](https://github.com/VsVim/VsVim/issues?milestone=29&page=1&state=closed)

Primary Issues Addressed
* Navigation issues with C style pragma directives
* Go to Definition causing VsVim to switch to Visual mode
* Visual Studio Dark Theme issues
* Support for `<leader>` mapping
* Several bugs around handling of `shiftwidth` and `tabstop`

### Version 1.3.1
[Issues closed in milestone 1.3.1](https://github.com/VsVim/VsVim/issues?milestone=27&page=1&state=closed)

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
[Issues closed in milestone 1.3](https://github.com/VsVim/VsVim/issues?milestone=23&state=closed)

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
[Issues closed in milestone 1.2.2](https://github.com/VsVim/VsVim/issues?milestone=25&state=closed)

Primary Issues Addressed

* Substitute with quotes behaved incorrectly
* Substitute at end of line behaved incorrectly
* Support for Mind Scape workbench files

### Version 1.2.1
[Issues closed in milestone 1.2.1](https://github.com/VsVim/VsVim/issues?milestone=24&state=closed)

Primary Issues Addressed

* Support for the clipboard option
* Could not 'j' over blank lines in Visual Character Mode
* Comments were breaking vimrc loading
* Repeating commands resulted in intellisense being displayed

### Version 1.2
[Issues closed in milestone 1.2](https://github.com/VsVim/VsVim/issues?milestone=20&state=closed)

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
[Issues closed in milestone 1.1.2](https://github.com/VsVim/VsVim/issues?sort=created&direction=desc&state=closed&page=1&milestone=22)

Primary Issues Addressed

* Performance of :hlseach
* Maintaining vertical caret column
* Several tabs / spaces issues including cc, caret column and repeat of 'cw',
* Tab on new line inserts tab on previous line

### Version 1.1.1
[Issues closed in milestone 1.1.1](https://github.com/VsVim/VsVim/issues?milestone=19&sort=created&direction=desc&state=closed)

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
[Issues closed in milestone 1.1](https://github.com/VsVim/VsVim/issues?sort=created&direction=desc&state=closed&page=1&milestone=10)

Primary Issues Addressed

* Insert mode largely rewritten. Better support for repeat and commands like CTRL-I, CTRL-M, CTRL-H, etc ...
* CTRL-N and CTRL-P support added.
* CTRL-A and CTRL-X support added.  Keys must be manually mapped to VsVim to avoid unexpected conflicts with select all and cut.
* Added support for the gv command
* VsVim will now preserve non-CRLF line endings
* Support added for Dev11 preview

### Version 1.0.3
[Issues closed in milestone 1.0.3](https://github.com/VsVim/VsVim/issues?milestone=17&state=closed)

Primary Issues Addressed

* Shift-C was placing the caret in the incorrect position.
* End key was cancelling Visual Mode
* Enter and Back were causing issues with R# in certain scenarios

### Version 1.0.2
[Issues closed in milestone 1.0.2](https://github.com/VsVim/VsVim/issues?milestone=16&state=closed)

Primary Issues Addressed

* Crash which occurred after closing tabs.  Most often repro'd when combined with Pro Power Tools
* Key mappings which contained multiple inputs didn't behave properly in insert mode
