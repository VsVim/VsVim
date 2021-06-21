

#light
namespace Vim

open System.Runtime.CompilerServices

[<assembly:Extension()>]
[<assembly:InternalsVisibleTo("Vim.Core.UnitTest")>]
[<assembly:InternalsVisibleTo("Vim.UnitTest.Utils")>]
[<assembly:InternalsVisibleTo("Vim.UI.Wpf.UnitTest")>]
[<assembly:InternalsVisibleTo("Vim.VisualStudio.Shared.2017.UnitTest")>]
[<assembly:InternalsVisibleTo("DynamicProxyGenAssembly2")>] // Moq
do()

