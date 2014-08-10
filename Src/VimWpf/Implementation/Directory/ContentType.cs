using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf.Implementation.Directory
{
    internal sealed class ContentType
    {
        [Export]
        [Name(VimWpfConstants.DirectoryContentType)]
        [BaseDefinition("text")]
        internal ContentTypeDefinition DirectoryContentTypeDefinition;
    }
}
