using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Core;
using Vim;
using System.Windows.Input;

namespace VimCoreTest
{
    public static class Util
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

        #region IMode

        public static bool CanProcess(this IMode mode, Key key)
        {
            return mode.CanProcess(InputUtil.KeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, Key key)
        {
            return mode.Process(InputUtil.KeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, char c)
        {
            return mode.Process((InputUtil.CharToKeyInput(c)));
        }

        public static void Process(this IMode mode, string input)
        {
            foreach (var c in input)
            {
                var i = InputUtil.CharToKeyInput(c);
                mode.Process(c);
            }
        }

        #endregion

        public static void ProcessInputAsString(this IVimBuffer buf, string input)
        {
            foreach (var c in input)
            {
                var i = InputUtil.CharToKeyInput(c);
                buf.ProcessInput(i);
            }
        }
    }
}
