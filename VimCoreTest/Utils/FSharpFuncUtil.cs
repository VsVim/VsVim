using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Core;

namespace VimCore.Test.Utils
{
    public static class FSharpFuncUtil
    {
        public static FSharpFunc<T,TResult> Create<T,TResult>(Converter<T,TResult> func)
        {
            return FSharpFunc<T,TResult>.FromConverter(func);
        }

        public static FSharpFunc<T1, FSharpFunc<T2,TResult>> Create<T1, T2, TResult>(Func<T1,T2,TResult> func)
        {
            Converter<T1, FSharpFunc<T2, TResult>> conv = value1 =>
                {
                    return Create<T2,TResult>(value2 => func(value1, value2));
                };
            return FSharpFunc<T1, FSharpFunc<T2, TResult>>.FromConverter(conv);
        }

        public static FSharpFunc<T1, FSharpFunc<T2,FSharpFunc<T3, TResult>>> Create<T1, T2, T3, TResult>(Func<T1,T2,T3,TResult> func)
        {
            Converter<T1, FSharpFunc<T2, FSharpFunc<T3,TResult>>> conv = value1 =>
                {
                    return Create<T2, T3, TResult>((value2, value3) => func(value1, value2, value3));
                };
            return FSharpFunc<T1, FSharpFunc<T2, FSharpFunc<T3,TResult>>>.FromConverter(conv);
        }
    }
}
