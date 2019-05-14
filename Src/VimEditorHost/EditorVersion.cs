﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.EditorHost
{
    /// <summary>
    /// The supported list of editor versions 
    /// </summary>
    /// <remarks>These must be listed in ascending version order</remarks>
    public enum EditorVersion
    {
        Vs2012,
        Vs2013,
        Vs2015,
        Vs2017,
        Vs2019,
    }
}
