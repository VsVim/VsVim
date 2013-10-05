using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VimApp
{
    interface IVimAppOptions
    {
        bool DisplayNewLines { get; set; } 

        event EventHandler Changed;
    }
}
