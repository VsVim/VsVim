Coding Guidelines
===

## Style

The C# code in this project follows the DotNet organization [coding style](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md).  

The F# code is the following:

- **DO** prefix private field names with an underscore (`_`)
- **DO** add a space between
    - Values and arithmic operators and comparisions
    - Names and Values in record initializer expressions
    - Names and explicit types `(text: string)`
    - Keywords and open parens `with get (`, `if (`, `elif (`
    - Discriminated Union Names and open parens

 - **DO NOT** use ; for multi-line object initializers

 ## Naming / Terminology

 The following patterns are used in the VsVim code base, and often the editor as well.  When at all possible
 the two code bases should use the same pattern to make it simpler to understand the code.

- Last is inclusive
- End is exclusive
- Util classes should contain Create methods to create 
- Editor APIs 
    - APIs taking a count should return an option or explicitly guard against count being too large.  Users
      too often control this number
    - APIs taking a line number should consider returning an option.  Line numbers are less likely to be controlled
      by the user
