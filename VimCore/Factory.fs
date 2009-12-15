#light


namespace Vim
open Microsoft.VisualStudio.Text

module Factory =

    /// Create an instance of the IVim interface
    let CreateVim host = (Vim(host)) :> IVim

    // Create a standalone IVimBuffer instance
    let CreateVimBuffer host view name caret =
        let vim = CreateVim host
        vim.CreateBuffer view name caret
      