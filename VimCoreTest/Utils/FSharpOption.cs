using Microsoft.FSharp.Core;

namespace VimCoreTest.Utils
{
    public static class FSharpOption
    {
        public static FSharpOption<T> Create<T>(T value)
        {
            return new FSharpOption<T>(value);
        }
        public static bool IsSome<T>(this FSharpOption<T> opt)
        {
            return FSharpOption<T>.get_IsSome(opt);
        }
        public static bool IsNone<T>(this FSharpOption<T> opt)
        {
            return FSharpOption<T>.get_IsNone(opt);
        }
    }
}
