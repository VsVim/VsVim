using System;
using Microsoft.FSharp.Core;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public static class FuncUtil
    {
        public static FSharpFunc<MotionArgument, FSharpOption<MotionResult>> CreateMotionFunc(Action<int> action)
        {
            Func<MotionArgument, FSharpOption<MotionResult>> func = arg =>
                {
                    action(arg.Count);
                    return FSharpOption<MotionResult>.None;
                };
            return func.ToFSharpFunc();
        }

        public static FSharpFunc<MotionArgument, FSharpOption<MotionResult>> CreateMotionFunc(Func<int, MotionResult> action)
        {
            Func<MotionArgument, FSharpOption<MotionResult>> func = arg =>
                {
                    return FSharpOption.Create(action(arg.Count));
                };
            return func.ToFSharpFunc();
        }

        public static FSharpFunc<MotionArgument, FSharpOption<MotionResult>> CreateMotionFunc(Func<MotionResult> func)
        {
            Func<MotionArgument, FSharpOption<MotionResult>> inner = arg =>
            {
                var ret = func();
                return ret != null
                    ? FSharpOption.Create(ret)
                    : FSharpOption<MotionResult>.None;
            };

            return inner.ToFSharpFunc();
        }
    }
}
