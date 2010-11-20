using System;
using Microsoft.FSharp.Core;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public static class FuncUtil
    {
        public static FSharpFunc<MotionArgument, FSharpOption<MotionData>> CreateMotionFunc(Action<int> action)
        {
            Func<MotionArgument, FSharpOption<MotionData>> func = arg =>
                {
                    action(arg.Count);
                    return FSharpOption<MotionData>.None;
                };
            return func.ToFSharpFunc();
        }

        public static FSharpFunc<MotionArgument, FSharpOption<MotionData>> CreateMotionFunc(Func<int, MotionData> action)
        {
            Func<MotionArgument, FSharpOption<MotionData>> func = arg =>
                {
                    return FSharpOption.Create(action(arg.Count));
                };
            return func.ToFSharpFunc();
        }

        public static FSharpFunc<MotionArgument, FSharpOption<MotionData>> CreateMotionFunc(Func<MotionData> func)
        {
            Func<MotionArgument, FSharpOption<MotionData>> inner = arg =>
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
