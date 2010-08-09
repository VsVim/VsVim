using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Collections;
using Moq;
using Vim;

namespace Vim.UnitTest
{
    internal static class MatchUtil
    {
        internal static FSharpList<char> CreateForCharList(string input)
        {
            Predicate<FSharpList<char>> pred = otherList => Enumerable.SequenceEqual(input, otherList);
            return CreateMatch(pred);
        }

        internal static FSharpList<KeyInput> CreateForKeyInputList(string input)
        {
            var list = input.Select(InputUtil.CharToKeyInput);
            Predicate<FSharpList<KeyInput>> pred = otherList => Enumerable.SequenceEqual(list, otherList);
            return CreateMatch(pred);
        }

        internal static IEnumerable<KeyInput> CreateForKeyInputSequence(string input)
        {
            var list = input.Select(InputUtil.CharToKeyInput);
            Predicate<IEnumerable<KeyInput>> pred = otherList => Enumerable.SequenceEqual(list, otherList);
            return CreateMatch(pred);
        }

        internal static T CreateMatch<T>(Predicate<T> pred)
        {
            return Match<T>.Create(pred);
        }
    }
}
