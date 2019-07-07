### Multiple Carets / Multiple Cursors / Multiple Selections

If the host supports multiple selections, VsVim will also support them.

#### Architecture

Most parts of VsVim are unware of multiple selections. In order to keep things
simple and preserve the single selection model for most of VsVim, there are two
major components that handle multiple selections:

- Running mode commands for all selections (`ICommonOperations.RunForAllSelections`)
- The multi-selection tracker (`MultiSelectionTracker`)

For commands where it makes sense to run them for each caret or selection, the
corresponding command runner will wrap the running of a single command into a
batch execution for each selection using `RunForAllSelections`. Then, while
running an individual command, the multi-selection infrastructure will:

- Set a single temporary selection, including a caret position
- Run the command as if there were a single selection
- Determine the desired resulting selection from the command result

When all the individual commands have run, the accumulated selections will be
set all at once. This causes the non-multi-selection aware code to still see
the primary selection as the real and only selection, while maintaining the
secondary selections behind the scenes.

#### The Multi-Selection API

Unfortunately, not all versions of the editor API support multiple selections.
In order to handle this, there are two API entry points in the Vim host:

- `IVimHost.GetSelectedSpans` - member
- `IVimHost.SetSelectedSpans` - member

A selected span is a triple of caret point, anchor point, and active point.
All three values are virtual snapshot points.  For multiple caret support,
a caret is just a zero-width selection. Selected spans are associated with
a specific text view, just like the normal caret and normal selection.

If there is no multi-selection support, getting the selected spans always
returns a single (possibly empty) span and setting multiple selected spans
ignores all of the secondary selections.

If there is multi-selection support, then the first selected span is the
primary selection and any subsequent selected spans are secondary selections,
sorted in ascending order by position within the text buffer.

In order to insulate the code base from the host, all consumers who use the
multi-selection API access the selections using related APIs in the common
operations module:

- `ICommonOperations.SelectedSpans` - property
- `ICommonOperations.SetSelectedSpans` - member
- `ICommonOperations.SelectedSpansSet` - event

#### VsVim Key Bindings

- `<C-A-LeftMouse>` - add or remove caret (normal, visual, select, insert)
- `<C-A-2-LeftMouse>` - add a new selected word or token (normal, visual, select, insert)
- `<C-A-Up>` and `<C-A-Down>` - add a new caret on an adjecent line (normal, insert)
- `<C-A-Up>` and `<C-A-Down>` - add a new selection on an adjecent line (visual, select)
- `<C-A-I>` - split selection into carets (visual, select)
- `<C-A-N>` - select word or token at caret (normal)
- `<C-A-N>` - add next occurrence of primary selection (visual, select)
- `<C-A-P>` - restore previous multi-carets or multi-selections (normal)
- `<Esc>` - clear secondary carets (normal)
- `<C-c>` - clear secondary carets or selections (normal, visual, insert)

#### The Unnamaed Register

Each caret gets their own unnamed register. All secondary caret's unnamed
registers are copied from the "real" unnamed register whenever a caret is
added or removed. The result is that if you put from the unnamed register at
all carets immediately after creating them, all carets will put the same
value. But if you yank text and the put it while there are multiple carets,
each caret will put what that caret yanked.

#### Interoperation with Visual Studio

Visual Studio itself provides some commands for multiple carets. VsVim
supports multi-selection operations performed by external components such
as the Visual Studio editor, a language service, a code assistant like
Resharper, or event another Visual Studio extension.

#### Visual Studio Key Bindings

- `<S-A-.>` - add selection for the next occurrence of the current word
- `<S-A-;>` - add selections for all occurrences of the current word

#### Testing

The multi-selection feature is accessed through the Vim host interface. During
testing the `MockVimHost` conditionally supports the the full multi-selection
APIs (controlled by the `IsMultSelectionSupported` property), which are
emulated by `MockMultiSelection`.
