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
            bool pred(FSharpList<char> otherList) => Enumerable.SequenceEqual(input, otherList);
            return CreateMatch<FSharpList<char>>(pred);
        }

        public static FSharpList<KeyInput> CreateForKeyInputList(string input)
        {
            var list = input.Select(KeyInputUtil.CharToKeyInput);
            bool pred(FSharpList<KeyInput> otherList) => Enumerable.SequenceEqual(list, otherList);
            return CreateMatch<FSharpList<KeyInput>>(pred);
        }

        public static IEnumerable<KeyInput> CreateForKeyInputSequence(string input)
        {
            var list = input.Select(KeyInputUtil.CharToKeyInput);
            bool pred(IEnumerable<KeyInput> otherList) => Enumerable.SequenceEqual(list, otherList);
            return CreateMatch<IEnumerable<KeyInput>>(pred);
        }

        public static T CreateMatch<T>(Predicate<T> pred)
        {
            return Match<T>.Create(pred);
        }
    }
}
