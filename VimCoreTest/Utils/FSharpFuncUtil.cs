using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Core;

namespace VimCoreTest.Utils
{
    public static class FSharpFuncUtil
    {
        private static Converter<T, TResult> CreateConverter<T, TResult>(Func<T, TResult> func)
        {
            return (arg) => func(arg);
        }

        public static FSharpFunc<T,TResult> Create<T,TResult>(Func<T,TResult> func)
        {
            var c1 = CreateConverter(func);
            return FSharpFunc<T,TResult>.FromConverter(c1);
        }
    }
}
