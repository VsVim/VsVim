using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;

namespace VsVim.Implementation.Options
{
    internal sealed class DefaultOptionPage : DialogPage
    {
        public DefaultSettings DefaultSettings
        {
            get;
            set;
        }
    }
}
