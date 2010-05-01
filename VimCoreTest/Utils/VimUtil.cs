using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Microsoft.FSharp.Core;
using Vim.Extensions;

namespace VimCore.Test.Utils
{
    internal static class VimUtil
    {

        internal static Command CreateSimpleCommand(string name, Action<FSharpOption<int>, Register> del)
        {
            return CreateSimpleCommand(
                name,
                (x, y) =>
                {
                    del(x, y);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }


        internal static Command CreateSimpleCommand(string name, Func<FSharpOption<int>, Register, CommandResult> func)
        {
            Converter<FSharpOption<int>, FSharpFunc<Register, CommandResult>> outerFunc = count =>
                {
                    Converter<Register, CommandResult> del = register => func(count, register);
                    return FSharpFuncUtil.Create(del);
                };
            var fsharpFunc = FSharpFuncUtil.Create(outerFunc);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = CommandName.NewManyKeyInputs(list);
            return Command.NewSimpleCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static Command CreateLongCommand(string name, Func<FSharpOption<int>, Register, LongCommandResult> func, CommandFlags flags = CommandFlags.None)
        {
            Converter<FSharpOption<int>, FSharpFunc<Register, LongCommandResult>> outerFunc = count =>
                {
                    Converter<Register, LongCommandResult> del = register => func(count, register);
                    return FSharpFuncUtil.Create(del);
                };
            var fsharpFunc = FSharpFuncUtil.Create(outerFunc);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = CommandName.NewManyKeyInputs(list);
            return Command.NewLongCommand(commandName, flags, fsharpFunc);
        }

        internal static Command CreateLongCommand(string name, Func<KeyInput, bool> func, CommandFlags flags = CommandFlags.None)
        {
            return CreateLongCommand(
                name,
                (x, y) =>
                {
                    FSharpFunc<KeyInput, LongCommandResult> realFunc = null;
                    Converter<KeyInput, LongCommandResult> func2 = ki =>
                        {
                            if (func(ki))
                            {
                                return LongCommandResult.NewFinished(CommandResult.NewCompleted(ModeSwitch.NoSwitch));
                            }
                            else
                            {
                                return LongCommandResult.NewNeedMoreInput(realFunc);
                            }
                        };
                    realFunc = func2;
                    return LongCommandResult.NewNeedMoreInput(realFunc);
                },
                flags);
        }

        internal static Command CreateMotionCommand(string name, Action<FSharpOption<int>, Register, MotionData> del)
        {
            return CreateMotionCommand(
                name,
                (x, y, z) =>
                {
                    del(x, y, z);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static Command CreateMotionCommand(string name, Func<FSharpOption<int>, Register, MotionData, CommandResult> func)
        {
            Converter<FSharpOption<int>, FSharpFunc<Register, FSharpFunc<MotionData, CommandResult>>> func1 = count =>
                {
                    Converter<Register, FSharpFunc<MotionData, CommandResult>> func2 = register =>
                    {
                        Converter<MotionData, CommandResult> func3 = data => func(count, register, data);
                        return FSharpFuncUtil.Create(func3);
                    };

                    return FSharpFuncUtil.Create(func2);
                };
            var fsharpFunc = FSharpFuncUtil.Create(func1);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = CommandName.NewManyKeyInputs(list);
            return Command.NewMotionCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static CommandRunData CreateCommandRunData(
            Command command,
            Register register,
            int? count = null,
            MotionData data = null)
        {
            var opt = data != null ? FSharpOption.Create(data) : FSharpOption<MotionData>.None;
            var countOpt = count != null ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            return new CommandRunData(
                command,
                register,
                countOpt,
                opt);
        }
    }
}
