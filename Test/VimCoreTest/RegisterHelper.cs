using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Core;

namespace Vim.UnitTest
{
    internal static class RegisterHelper
    {
        public static FSharpOption<Register> ToOption(this Register reg)
        {
            return new FSharpOption<Register>(reg);
        }
    }
}
