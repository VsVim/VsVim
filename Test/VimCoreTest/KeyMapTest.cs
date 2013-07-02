using System.Collections.Generic;
using System.Linq;
using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class KeyMapTest
    {
        protected readonly IVimGlobalSettings _globalSettings;
        protected readonly IKeyMap _map;
        protected readonly Dictionary<string, VariableValue> _variableMap;
        internal readonly KeyMap _mapRaw;

        public KeyMapTest()
        {
            _globalSettings = new GlobalSettings();
            _variableMap = new Dictionary<string, VariableValue>();
            _mapRaw = new KeyMap(_globalSettings, _variableMap);
            _map = _mapRaw;
        }

        protected void AssertNoMapping(string lhs, KeyRemapMode mode = null)
        {
            AssertNoMapping(KeyNotationUtil.StringToKeyInputSet(lhs), mode);
        }

        protected void AssertNoMapping(KeyInputSet lhs, KeyRemapMode mode = null)
        {
            Assert.False(_map.GetKeyMappingsForMode(mode).Any(keyMapping => keyMapping.Left == lhs));

            mode = mode ?? KeyRemapMode.Normal;
            var result = _map.GetKeyMappingResult(lhs, mode);
            if (lhs.Length == 1)
            {
                Assert.True(result.IsMapped);
            }
            else 
            {
                Assert.True(result.IsPartiallyMapped);
                Assert.Equal(lhs.FirstKeyInput.Value, result.AsPartiallyMapped().Item1.FirstKeyInput.Value);
                Assert.Equal(1, result.AsPartiallyMapped().Item1.Length);
            }

            Assert.Equal(lhs, result.GetMappedKeyInputs());
        }

        protected void AssertMapping(string lhs, string expected, KeyRemapMode mode = null)
        {
            AssertMapping(KeyNotationUtil.StringToKeyInputSet(lhs), expected, mode);
        }

        protected void AssertMapping(KeyInputSet lhs, string expected, KeyRemapMode mode = null)
        {
            mode = mode ?? KeyRemapMode.Normal;
            var ret = _map.GetKeyMappingResult(lhs, mode);
            Assert.True(ret.IsMapped);
            Assert.Equal(KeyInputSetUtil.OfString(expected), ret.GetMappedKeyInputs());
        }

        protected void AssertPartialMapping(string lhs, string expectedMapped, string expectedRemaining, KeyRemapMode mode = null)
        {
            AssertPartialMapping(KeyInputSetUtil.OfString(lhs), expectedMapped, expectedRemaining, mode);
        }

        protected void AssertPartialMapping(KeyInputSet lhs, string expectedMapped, string expectedRemaining, KeyRemapMode mode = null)
        {
            mode = mode ?? KeyRemapMode.Normal;
            var ret = _map.GetKeyMappingResult(lhs, mode);
            Assert.True(ret.IsPartiallyMapped);

            var partiallyMapped = ret.AsPartiallyMapped();
            Assert.Equal(KeyInputSetUtil.OfString(expectedMapped), partiallyMapped.Item1);
            Assert.Equal(KeyInputSetUtil.OfString(expectedRemaining), partiallyMapped.Item2);
        }

        public sealed class MapWithNoRemapTest : KeyMapTest
        {
            private void Map(string lhs, string rhs)
            {
                Assert.True(_map.MapWithNoRemap(lhs, rhs, KeyRemapMode.Normal));
            }

            [Fact]
            public void AlphaToAlpha()
            {
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('a', KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('b'), ret);
            }

            [Fact]
            public void AlphaToDigit()
            {
                Assert.True(_map.MapWithNoRemap("a", "1", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('a', KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('1'), ret);
            }

            [Fact]
            public void ManyAlphaToSingle()
            {
                Assert.True(_map.MapWithNoRemap("ab", "b", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping("ab", KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('b'), ret);
            }

            [Fact]
            public void SymbolToSymbol()
            {
                Assert.True(_map.MapWithNoRemap("&", "!", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('&', KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('!'), ret);
            }

            [Fact]
            public void OneAlphaToTwo()
            {
                Assert.True(_map.MapWithNoRemap("a", "bc", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('a', KeyRemapMode.Normal).ToList();
                Assert.Equal(2, ret.Count);
                Assert.Equal('b', ret[0].Char);
                Assert.Equal('c', ret[1].Char);
            }

            [Fact]
            public void OneAlphaToThree()
            {
                Assert.True(_map.MapWithNoRemap("a", "bcd", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).ToList();
                Assert.Equal(3, ret.Count);
                Assert.Equal('b', ret[0].Char);
                Assert.Equal('c', ret[1].Char);
                Assert.Equal('d', ret[2].Char);
            }

            [Fact]
            public void DontRemapEmptyString()
            {
                Assert.False(_map.MapWithNoRemap("a", "", KeyRemapMode.Normal));
            }

            [Fact]
            public void ShiftPromotesAlpha()
            {
                Map("<S-a>", "#");
                AssertNoMapping("a");
                AssertMapping("A", "#");
            }

            [Fact]
            public void ShiftWithUpperAlphaIsJustUpperAlpha()
            {
                Map("<S-a>", "#");
                AssertNoMapping("a");
                AssertMapping("A", "#");
            }

            [Fact]
            public void ShiftSymbolDoesNotChangeChar()
            {
                Map("<S-#>", "pound");
                AssertNoMapping("#");
                var keyInput = KeyInputUtil.CharToKeyInput('#');
                keyInput = KeyInputUtil.ChangeKeyModifiersDangerous(keyInput, KeyModifiers.Shift);
                Assert.True(_map.GetKeyMappingResult(keyInput, KeyRemapMode.Normal).IsMapped);
            }

            [Fact]
            public void LessThanChar()
            {
                Assert.True(_map.MapWithNoRemap("<", "pound", KeyRemapMode.Normal));
            }

            /// <summary>
            /// By default the '\' character isn't special in key mappings.  It's treated like any
            /// other character.  It only achieves special meaning when 'B' is excluded from the 
            /// 'cpoptions' setting
            /// </summary>
            [Fact]
            public void EscapeLessThanSymbol()
            {
                Assert.True(_map.MapWithNoRemap("a", @"\<Home>", KeyRemapMode.Normal));
                var result = _map.GetKeyMappingResult("a", KeyRemapMode.Normal);
                Assert.Equal(KeyNotationUtil.StringToKeyInputSet(@"\<Home>"), result.AsMapped().Item);
            }

            [Fact]
            public void HandleLessThanEscapeLiteral()
            {
                Assert.True(_map.MapWithNoRemap("a", "<lt>lt>", KeyRemapMode.Normal));
                var result = _map.GetKeyMappingResult("a", KeyRemapMode.Normal);
                Assert.Equal(KeyInputSetUtil.OfString("<lt>"), result.AsMapped().Item);
            }

            [Fact]
            public void ControlAlphaIsCaseInsensitive()
            {
                Assert.True(_map.MapWithNoRemap("<C-a>", "1", KeyRemapMode.Normal));
                Assert.True(_map.MapWithNoRemap("<C-A>", "2", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping("<C-a>", KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
                ret = _map.GetKeyMapping("<C-A>", KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
            }

            [Fact]
            public void AltAlphaIsCaseSensitive()
            {
                Assert.True(_map.MapWithNoRemap("<A-a>", "1", KeyRemapMode.Normal));
                Assert.True(_map.MapWithNoRemap("<A-A>", "2", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping("<A-a>", KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('1'), ret);
                ret = _map.GetKeyMapping("<A-A>", KeyRemapMode.Normal).Single();
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
            }

            /// <summary>
            /// When two mappnigs have the same prefix then they are ambiguous and require a
            /// tie breaker input
            /// </summary>
            [Fact]
            public void Ambiguous()
            {
                Assert.True(_map.MapWithNoRemap("aa", "foo", KeyRemapMode.Normal));
                Assert.True(_map.MapWithNoRemap("aaa", "bar", KeyRemapMode.Normal));
                var ret = _map.GetKeyMappingResult("aa", KeyRemapMode.Normal);
                Assert.True(ret.IsNeedsMoreInput);
            }

            /// <summary>
            /// Resloving the ambiguity should cause both the original plus the next input to be 
            /// returned
            /// </summary>
            [Fact]
            public void Ambiguous_ResolveShorter()
            {
                Assert.True(_map.MapWithNoRemap("aa", "foo", KeyRemapMode.Normal));
                Assert.True(_map.MapWithNoRemap("aaa", "bar", KeyRemapMode.Normal));
                AssertPartialMapping("aab", "foo", "b");
            }

            [Fact]
            public void Ambiguous_ResolveLonger()
            {
                Assert.True(_map.MapWithNoRemap("aa", "foo", KeyRemapMode.Normal));
                Assert.True(_map.MapWithNoRemap("aaa", "bar", KeyRemapMode.Normal));
                var ret = _map.GetKeyMappingResult("aaa", KeyRemapMode.Normal);
                Assert.True(ret.IsMapped);
                Assert.Equal(KeyInputSetUtil.OfString("bar"), ret.AsMapped().Item);
            }

            /// <summary>
            /// In the ambiguous double case we will end up with the best possible mapping
            /// for the given input here.  The first mapping can be resolved but not the
            /// second
            /// </summary>
            [Fact]
            public void Ambiguous_Double()
            {
                Map("aa", "one");
                Map("aaa", "two");
                Map("b", "three");
                Map("bb", "four");
                AssertPartialMapping("aab", "one", "b");
                AssertMapping("aaa", "two");
            }

            [Fact]
            public void Ambiguous_DoubleResolve()
            {
                Map("aa", "one");
                Map("aaa", "two");
                Map("b", "three");
                Map("bb", "four");
                AssertPartialMapping("aabc", "one", "bc");
            }
        }

        public sealed class MapWithRemapTest : KeyMapTest
        {
            private void Map(string lhs, string rhs)
            {
                Assert.True(_map.MapWithRemap(lhs, rhs, KeyRemapMode.Normal));
            }

            /// <summary>
            /// Simple mapping with no remap involved
            /// </summary>
            [Fact]
            public void Simple()
            {
                Map("a", "b");
                AssertMapping("a", "b");
            }

            [Fact]
            public void SimpleLong()
            {
                Map("a", "bcd");
                AssertPartialMapping("a", "b", "cd");
            }

            [Fact]
            public void SimpleChain()
            {
                Map("a", "b");
                Map("b", "c");
                AssertMapping("a", "c");
                AssertMapping("b", "c");
            }

            [Fact]
            public void RemapOfPartOfRight()
            {
                Map("a", "bc");
                Map("b", "d");
                AssertPartialMapping("a", "d", "c");
            }

            /// <summary>
            /// When considering the full expansion in key input processing this particular combination
            /// posses a problem because it causes infinite mapping.  
            ///
            ///  - Step 1: maps 'j' to '3j'
            ///  - Step 2: processes '3'
            ///  - Step 3: maps 'j' to '3j'
            ///
            /// In just the mapping case though it's completely resolvable
            /// </summary>
            [Fact]
            public void MapWithRemapSameKey()
            {
                Map("j", "3j");
                AssertPartialMapping("j", "3", "j");
            }

            /// <summary>
            /// If the rhs begins with the lhs then it shouldn't produce an infinite recursive 
            /// mapping.  
            /// </summary>
            [Fact]
            public void RightStartsWithLeft()
            {
                Map("a", "ab");
                AssertPartialMapping("a", "a", "b");
            }

            /// <summary>
            /// If the rhs begins with the lhs then only the first character shouldn't be considered
            /// on the rhs for mapping.  Starting immediately with the second character remapping 
            /// should occur
            /// </summary>
            [Fact]
            public void RightStartsWithLeftComplex()
            {
                Map("ab", "abcd");
                Map("b", "hit");
                AssertPartialMapping("ab", "a", "bcd");
            }

            [Fact]
            public void HandleCommandKey()
            {
                Map("<D-k>", "gk");
                var ki = new KeyInput(VimKey.RawCharacter, KeyModifiers.Command, FSharpOption.Create('k'));
                var kiSet = KeyInputSet.NewOneKeyInput(ki);
                AssertPartialMapping(kiSet, "g", "k");
            }

            /// <summary>
            /// When processing a remap if the {rhs} doesn't map and has length greater
            /// than 0 then we shouldn't be considering the remainder here.  Once the {lhs} isn't 
            /// mappable at column 0 we are done.  It's up to the IVimBuffer to give us back the 
            /// remainder in the proper mode based on the processing of the first value
            /// </summary>
            [Fact]
            public void RightSecondKeyHasMap()
            {
                Map("a", "bc");
                Map("c", "d");
                AssertPartialMapping("a", "b", "c");
            }

            /// <summary>
            /// Make sure we don't fall for the recursive prefix trick.  
            ///
            ///  :imap ab abcd
            /// 
            /// Typing 'ab' in insert mode shouldn't produce an infinite or recursive mapping.  The
            /// prefix should be ignored here (:help recursive_mapping)
            /// </summary>
            [Fact]
            public void RecursivePrefix()
            {
                Map("ab", "abcd");
                AssertPartialMapping("ab", "a", "bcd");
            }

            /// <summary>
            /// Mutually recursive mappings should cause a recursive result once we hit the 
            /// maxmapdept number of recursions (:help recursive_mapping)
            /// </summary>
            [Fact]
            public void RecursiveMutually()
            {
                // Make the depth high in the unit tests to ensure that we aren't doing head recursion 
                // here
                _globalSettings.MaxMapDepth = 1000;
                Map("k", "j");
                Map("j", "k");
                Assert.True(_map.GetKeyMappingResult("k", KeyRemapMode.Normal).IsRecursive);
                Assert.True(_map.GetKeyMappingResult("j", KeyRemapMode.Normal).IsRecursive);
            }

            [Fact]
            public void CleverMap()
            {
                Map("a", "b");
                Map("aa", "none");
                Map("bc", "done");
                AssertPartialMapping("ac", "d", "one");
            }

            /// <summary>
            /// When a remap operation completes and has length greater than 0 the remainder 
            /// should be remappable
            /// </summary>
            [Fact]
            public void RestShouldBeRemappable()
            {
                Map("a", "could");
                AssertPartialMapping("a", "c", "ould");
            }

            /// <summary>
            /// If a remap operation resolves to a single value which itself is unmappable then 
            /// the mapping is complete
            /// </summary>
            [Fact]
            public void SingleIsComplete()
            {
                Map("cat", "a");
                Map("a", "b");
                AssertMapping("cat", "b");
            }
        }

        public sealed class MapLeaderTest : KeyMapTest
        {
            [Fact]
            public void SimpleLeft()
            {
                _variableMap["mapleader"] = VariableValue.NewString("x");
                _map.MapWithNoRemap("<Leader>", "y", KeyRemapMode.Normal);
                AssertMapping("x", "y");
            }

            [Fact]
            public void SimpleLeftWithNoMapping()
            {
                _map.MapWithNoRemap("<Leader>", "y", KeyRemapMode.Normal);
                AssertMapping(@"\", "y");
            }

            [Fact]
            public void SimpleRight()
            {
                _variableMap["mapleader"] = VariableValue.NewString("y");
                _map.MapWithNoRemap("x", "<Leader>", KeyRemapMode.Normal);
                AssertMapping("x", "y");
            }

            [Fact]
            public void SimpleRightWithNoMapping()
            {
                _map.MapWithNoRemap("x", "<Leader>", KeyRemapMode.Normal);
                AssertMapping("x", @"\");
            }

            [Fact]
            public void LowerCaseLeader()
            {
                _map.MapWithNoRemap("x", "<leader>", KeyRemapMode.Normal);
                AssertMapping("x", @"\");
            }

            [Fact]
            public void MixedCaseLeader()
            {
                _map.MapWithNoRemap("x", "<lEaDer>", KeyRemapMode.Normal);
                AssertMapping("x", @"\");
            }
        }

        public sealed class ZeroCountTest : KeyMapTest
        {
            private void Map(string lhs, string rhs)
            {
                Assert.True(_map.MapWithRemap(lhs, rhs, KeyRemapMode.Normal));
            }

            [Fact]
            public void Default()
            {
                Map("0", "cat");
                AssertPartialMapping("0", "c", "at");
            }

            [Fact]
            public void Simple()
            {
                Map("0", "cat");
                _map.IsZeroMappingEnabled = false;
                AssertMapping("0", "0");
            }

            [Fact]
            public void Complex()
            {
                Map("01", "cat");
                _map.IsZeroMappingEnabled = false;
                AssertPartialMapping("01", "0", "1");
            }

            [Fact]
            public void MapToZeroStillDisableZero()
            {
                Map("0", "cat");
                Map("a", "0");
                _map.IsZeroMappingEnabled = false;
                AssertMapping("a", "0");
            }
        }

        public sealed class MiscTest : KeyMapTest
        {
            private void MapWithRemap(string lhs, string rhs)
            {
                Assert.True(_map.MapWithRemap(lhs, rhs, KeyRemapMode.Normal));
            }

            [Fact]
            public void GetKeyMapping1()
            {
                Assert.True(_map.MapWithRemap("a", "b", KeyRemapMode.Normal));
                Assert.True(_map.MapWithRemap("b", "a", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping(KeyInputSetUtil.OfChar('a'), KeyRemapMode.Normal);
                Assert.True(ret.IsRecursive);
            }

            [Fact]
            public void GetKeyMappingResult1()
            {
                Assert.True(_map.MapWithRemap("a", "b", KeyRemapMode.Normal));
                Assert.True(_map.MapWithRemap("b", "a", KeyRemapMode.Normal));
                var ret = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.True(ret.IsRecursive);
            }

            /// <summary>
            /// If a particular value isn't mapped then the GetKeyMappingResult functions 
            /// should just be an identity function
            /// </summary>
            [Fact]
            public void GetKeyMappingResult2()
            {
                AssertNoMapping("b");
                AssertNoMapping("a");
            }

            [Fact]
            public void GetKeyMapppingResult3()
            {
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.True(res.IsMapped);
                Assert.Equal('b', res.AsMapped().Item.KeyInputs.Single().Char);
            }

            [Fact]
            public void GetKeyMappingResult4()
            {
                Assert.True(_map.MapWithNoRemap("a", "bc", KeyRemapMode.Normal));
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.True(res.IsMapped);
                var list = res.AsMapped().Item.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal('b', list[0].Char);
                Assert.Equal('c', list[1].Char);
            }

            [Fact]
            public void GetKeyMappingResult5()
            {
                Assert.True(_map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal));
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.True(res.IsNeedsMoreInput);
            }

            /// <summary>
            /// If GetKeyMapping is called with a string that has no mapping then only the first
            /// key is considered unmappable.  The rest is still eligable for mapping 
            /// </summary>
            [Fact]
            public void GetKeyMappingResult_RemainderIsMappable()
            {
                var result = _map.GetKeyMappingResult("dog", KeyRemapMode.Normal).AsPartiallyMapped();
                Assert.Equal(KeyInputSetUtil.OfString("d"), result.Item1);
                Assert.Equal(KeyInputSetUtil.OfString("og"), result.Item2);
            }

            /// <summary>
            /// Once the mapping is cleared it goes back to an identity mapping since there isn't anything
            /// </summary>
            [Fact]
            public void Clear1()
            {
                MapWithRemap("a", "b");
                _map.Clear(KeyRemapMode.Normal);
                AssertNoMapping("a");
            }

            /// <summary>
            /// Make sure we only clear the specified mode 
            /// </summary>
            [Fact]
            public void Clear2()
            {
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Insert));
                _map.Clear(KeyRemapMode.Normal);
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Insert);
                Assert.True(res.IsMapped);
                Assert.Equal('b', res.AsMapped().Item.KeyInputs.Single().Char);
            }

            [Fact]
            public void ClearAll()
            {
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Insert));
                _map.ClearAll();
                AssertNoMapping("a", KeyRemapMode.Normal);
                AssertNoMapping("a", KeyRemapMode.Insert);
            }

            /// <summary>
            /// Unmapping a specific entry removes it completely
            /// </summary>
            [Fact]
            public void Unmap1()
            {
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.True(_map.Unmap("a", KeyRemapMode.Normal));
                AssertNoMapping("a");
            }

            [Fact]
            public void Unmap2()
            {
                Assert.True(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.False(_map.Unmap("a", KeyRemapMode.Insert));
                Assert.True(_map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).IsMapped);
            }

            /// <summary>
            /// Straight forward unmap of via the mapping instead of the key 
            /// </summary>
            [Fact]
            public void UnmapByMapping_Simple()
            {
                Assert.True(_map.MapWithNoRemap("cat", "dog", KeyRemapMode.Insert));
                Assert.True(_map.UnmapByMapping("dog", KeyRemapMode.Insert));
                AssertNoMapping("cat");
            }

            /// <summary>
            /// Don't allow the unmapping by the key
            /// </summary>
            [Fact]
            public void UnmapByMapping_Bad()
            {
                Assert.True(_map.MapWithNoRemap("cat", "dog", KeyRemapMode.Insert));
                Assert.False(_map.UnmapByMapping("cat", KeyRemapMode.Insert));
            }

            [Fact]
            public void GetKeyMappingResultFromMultiple1()
            {
                _map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal);

                var input = "aa".Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
                var res = _map.GetKeyMapping(KeyInputSet.NewManyKeyInputs(input), KeyRemapMode.Normal);
                Assert.Equal('b', res.AsMapped().Item.KeyInputs.Single().Char);
            }

            [Fact]
            public void GetKeyMappingResultFromMultiple2()
            {
                _map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal);

                var input = "a".Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
                var res = _map.GetKeyMapping(KeyInputSet.NewManyKeyInputs(input), KeyRemapMode.Normal);
                Assert.True(res.IsNeedsMoreInput);
            }

            [Fact]
            public void Issue328()
            {
                Assert.True(_map.MapWithNoRemap("<S-SPACE>", "<ESC>", KeyRemapMode.Insert));
                var res = _map.GetKeyMapping(KeyInputUtil.ApplyModifiersToChar(' ', KeyModifiers.Shift), KeyRemapMode.Insert);
                Assert.Equal(KeyInputUtil.EscapeKey, res.Single());
            }

            [Fact]
            public void Issue1059()
            {
                Assert.True(_map.MapWithNoRemap("/v", "<hello>", KeyRemapMode.Insert));
                AssertMapping("/v", "<hello>", KeyRemapMode.Insert);
            }
        }
    }
}
