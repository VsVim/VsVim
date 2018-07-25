
## Terminology 

Describes meaning of terms used in the API and code base.

### Locations in the buffer

- Position: refers to the position notation in underlying editor. For example `SnapshotPoint` is
a specific position in an `ITextSnapshot`. It may be in the middle of a line feed, code point,
etc ...
- Code point: refers to a Unicode code point value. In addition to valid code points it can refer
to broken code points (high or low surrogate without the matching value).
- Columns: vim based unit of measure. The granularity of a left or right motion in vim. A tab is 
a single column, as is a astral code point.
- Spaces: vim based unit of measure for columns. That is each column takes up a specific number of 
spaces. This is used often when calculating indentation / columns between lines.
    - Unicode control and non spacing values: 0 
    - Tab: current value of `tabstop`
    - Wide characters: 2
    - Everything else: 1

### TODO:

Wide characters: are they handled at SnapshotCodePoint or SnapshotCharacterSpan. 
