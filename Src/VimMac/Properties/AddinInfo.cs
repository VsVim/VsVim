using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin(
	"VsVim",
	Namespace = "Vim.Mac",
	Version = "2.8.0.0"
)]

[assembly: AddinName("VsVim")]
[assembly: AddinCategory("IDE extensions")]
[assembly: AddinUrl("https://github.com/VsVim/VsVim")]
[assembly: AddinDescription("VIM emulation layer for Visual Studio")]
[assembly: AddinAuthor("Jared Parsons")]
[assembly: AddinDependency("::MonoDevelop.Core", "8.3")]
[assembly: AddinDependency("::MonoDevelop.Ide", "8.3")]
