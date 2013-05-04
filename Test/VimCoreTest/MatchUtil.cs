using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using Moq;

namespace Vim.UnitTest
{
    public static class MatchUtil
    {
        public static FSharpList<char> CreateForCharList(string input)
        {
            Predicate<FSharpList<char>> pred = otherList => Enumerable.SequenceEqual(input, otherList);
            return CreateMatch(pred);
        }

        public static FSharpList<KeyInput> CreateForKeyInputList(string input)
        {
            var list = input.Select(KeyInputUtil.CharToKeyInput);
            Predicate<FSharpList<KeyInput>> pred = otherList => Enumerable.SequenceEqual(list, otherList);
            return CreateMatch(pred);
        }

        public static IEnumerable<KeyInput> CreateForKeyInputSequence(string input)
        {
            var list = input.Select(KeyInputUtil.CharToKeyInput);
            Predicate<IEnumerable<KeyInput>> pred = otherList => Enumerable.SequenceEqual(list, otherList);
            return CreateMatch(pred);
        }

        public static T CreateMatch<T>(Predicate<T> pred)
        {
            return Match<T>.Create(pred);
        }
    }
}
