using System;
using Microsoft.FSharp.Core;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public static class FuncUtil
    {
        public static FSharpFunc<MotionUse, FSharpFunc<FSharpOption<int>, FSharpOption<MotionData>>> CreateMotionFunc(Action<int> action)
        {
            Func<MotionUse, FSharpOption<int>, FSharpOption<MotionData>> func = (motionUse, count) =>
                {
                    action(CommandUtil.CountOrDefault(count));
                    return FSharpOption<MotionData>.None;
                };
            return func.ToFSharpFunc();
        }

        public static FSharpFunc<MotionUse, FSharpFunc<FSharpOption<int>, FSharpOption<MotionData>>> CreateMotionFunc(Func<int, MotionData> action)
        {
            Func<MotionUse, FSharpOption<int>, FSharpOption<MotionData>> func = (motionUse, count) =>
                {
                    return FSharpOption.Create(action(CommandUtil.CountOrDefault(count)));
                };
            return func.ToFSharpFunc();
        }

        public static FSharpFunc<MotionUse, FSharpFunc<FSharpOption<int>, FSharpOption<MotionData>>> CreateMotionFunc(Func<MotionData> func)
        {
            Func<MotionUse, FSharpOption<int>, FSharpOption<MotionData>> inner = (motionUse, count) =>
            {
                var ret = func();
                return ret != null
                    ? FSharpOption.Create(ret)
                    : FSharpOption<MotionData>.None;
            };

            return inner.ToFSharpFunc();
        }
    }
}
