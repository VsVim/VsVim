#light


namespace VimCore
open Microsoft.VisualStudio.Text

module Factory =

    /// Create an instance of the IVim interface
    let CreateVim () = (Vim()) :> IVim

    // Create a standalone IVimBuffer instance
    let CreateVimBuffer host view name caret =
        let vim = CreateVim()
        vim.CreateBuffer host view name caret
      