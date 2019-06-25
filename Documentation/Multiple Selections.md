### Multiple Selections

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

#### The multi-selection API

Unfortunately, not all versions of the editor API support multiple selections.
In order to handle this, there are three API entry points in the Vim host:

- `IVimHost.GetSelectedSpans` - member
- `IVimHost.SetSelectedSpans` - member

If there is no multiple-selection support, getting the selected spans always
returns a single (possibly empty) span and setting multiple selected spans
ignores all of the secondary selections.

If there is multiple-selection support, then the first selected span is the
primary selection and any subsequent selected spans are secondary selections,
sorted in ascending order by position within the text buffer.

In order to insulate the code base from the host, all consumers who use
the multi-selection API access the selections using related APIs in the
common operations module:

- `ICommonOperations.SelectedSpans` - property
- `ICommonOperations.SetSelectedSpans` - member
- `ICommonOperations.SelectedSpansSet` - event
