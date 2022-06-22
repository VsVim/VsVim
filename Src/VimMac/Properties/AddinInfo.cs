﻿using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin(
    "VsVim",
    Namespace = "Vim.Mac",
    Version = "0.8.0.15"
)]

[assembly: AddinName("VsVim")]
[assembly: AddinCategory("IDE extensions")]
[assembly: AddinUrl("https://github.com/VsVim/VsVim")]
[assembly: AddinDescription("VIM emulation layer for Visual Studio")]
[assembly: AddinAuthor("Jared Parsons")]
