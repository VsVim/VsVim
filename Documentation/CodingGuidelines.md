Coding Guidelines
===

## Style

The C# code in this project follows the .NET Foundation's [coding style guidelines](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md).

The F# code follows these conventions:

- **Prefix** private field names with an underscore (`_`).
- **Add** a space between:
  - Values and arithmetic operators and comparisons.
  - Names and values in record initializer expressions.
  - Names and explicit types `(text: string)`.
  - Keywords and open parentheses, e.g., `with get (`, `if (`, `elif (`.
  - Discriminated Union names and open parentheses.

- **Do not** use semicolons (`;`) for multi-line object initializers.

## Naming and Terminology

The following patterns are used consistently in the VsVim codebase and often align with the Visual Studio editor:

- "Last" is inclusive.
- "End" is exclusive.
- Utility classes should contain `Create` methods to instantiate objects.
- Editor APIs:
  - APIs taking a count should return an option or explicitly guard against counts being too large, as users often control this number.
  - APIs taking a line number should consider returning an option, as line numbers are less likely to be user-controlled.

## Comments and Documentation

- Use XML documentation comments (`///`) for public APIs.
- Write clear and concise comments explaining the purpose and behavior of complex code.
- Avoid redundant comments that restate the obvious.

## Error Handling and Logging

- Use exceptions for unexpected errors.
- Validate inputs and provide meaningful error messages.
- Log important events and errors to assist in debugging.

## Testing

- Write unit tests for all new features and bug fixes.
- Follow existing test patterns and naming conventions.
- Ensure tests are deterministic and independent.

## Code Reviews and Contributions

- Follow the project's [CONTRIBUTING.md](../CONTRIBUTING.md) guidelines.
- Ensure code adheres to style and quality standards before submitting.
- Provide clear descriptions and rationale in pull requests.
