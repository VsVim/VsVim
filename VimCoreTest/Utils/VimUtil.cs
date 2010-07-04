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
            var fsharpFunc = FSharpFuncUtil.Create(func);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return Command.NewSimpleCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static Command CreateLongCommand(string name, Func<FSharpOption<int>, Register, LongCommandResult> func, CommandFlags flags = CommandFlags.None)
        {
            var fsharpFunc = FSharpFuncUtil.Create(func);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
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
            var fsharpFunc = FSharpFuncUtil.Create(func);
            var list = name.Select(InputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            return Command.NewMotionCommand(commandName, CommandFlags.None, fsharpFunc);
        }

        internal static MotionCommand CreateSimpleMotion(string name, Func<MotionData> func)
        {
            var fsharpFunc = FSharpFuncUtil.Create<FSharpOption<int>, FSharpOption<MotionData>>(unused => FSharpOption.Create(func()));
            var commandName = CommandUtil.CreateCommandName(name);
            return MotionCommand.NewSimpleMotionCommand(
                commandName,
                fsharpFunc);
        }

        internal static CommandRunData CreateCommandRunData(
            Command command,
            Register register,
            int? count = null,
            MotionRunData motionRunData = null)
        {
            var countOpt = count != null ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            var motion = motionRunData != null
                ? FSharpOption.Create(motionRunData)
                : FSharpOption<MotionRunData>.None;
            return new CommandRunData(
                command,
                register,
                countOpt,
                motion);
        }

        internal static MotionRunData CreateMotionRunData(
            MotionCommand motionCommand,
            int? count = null,
            Func<MotionData> func = null)
        {
            func = func ?? (() => null);
            Converter<FSharpOption<int>, FSharpOption<MotionData>> conv = unused =>
                {
                    var res = func();
                    if (res == null) { return FSharpOption<MotionData>.None; }
                    else { return FSharpOption.Create(res); }
                };
            var countOpt = count != null ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            return new MotionRunData(
                motionCommand,
                countOpt,
                FSharpFuncUtil.Create(conv));
        }
            
    }
}
