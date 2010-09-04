using System;
using Microsoft.FSharp.Core;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public static class FuncUtil
    {
        public static FSharpFunc<FSharpOption<int>, FSharpOption<MotionData>> CreateMotionFunc(Action<int> action)
        {
            Func<FSharpOption<int>, FSharpOption<MotionData>> func = count =>
                {
                    action(CommandUtil.CountOrDefault(count));
                    return FSharpOption<MotionData>.None;
                };
            return func.ToFSharpFunc();
        }

        public static FSharpFunc<FSharpOption<int>, FSharpOption<MotionData>> CreateMotionFunc(Func<int, MotionData> action)
        {
            Func<FSharpOption<int>, FSharpOption<MotionData>> func = count =>
                {
                    return FSharpOption.Create(action(CommandUtil.CountOrDefault(count)));
                };
            return func.ToFSharpFunc();
        }
    }
}
