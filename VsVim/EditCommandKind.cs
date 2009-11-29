using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VsVim
{
    /// <summary>
    /// Kind of Command in Visual Studio.  It's an attempt to unify the different command groups which 
    /// are in different versions of Visual Studio to which VsVim is concerned about
    /// </summary>
    internal enum EditCommandKind
    {
        TypeChar,
        Return,
        Cancel,
        Delete,
        Backspace,
        Unknown
    }
}
