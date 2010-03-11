using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Core;

namespace Vim.UI.Wpf
{
    internal static class Extensions
    {
        #region Option

        public static bool IsNone<T>(this FSharpOption<T> option)
        {
            return FSharpOption<T>.get_IsNone(option);
        }

        public static bool IsSome<T>(this FSharpOption<T> option)
        {
            return FSharpOption<T>.get_IsSome(option);
        }

        public static bool HasValue<T>(this FSharpOption<T> option)
        {
            return FSharpOption<T>.get_IsSome(option);
        }

        #endregion

    }
}
