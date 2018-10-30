

#light
namespace Vim

open System.Reflection
open System.Runtime.CompilerServices

[<assembly:Extension()>]
[<assembly:AssemblyVersion(VimConstants.VersionNumber)>]
[<assembly:InternalsVisibleTo("Vim.Core.UnitTest")>]
[<assembly:InternalsVisibleTo("Vim.UnitTest.Utils")>]
[<assembly:InternalsVisibleTo("Vim.UI.Wpf.UnitTest")>]
[<assembly:InternalsVisibleTo("DynamicProxyGenAssembly2")>] // Moq
do()

