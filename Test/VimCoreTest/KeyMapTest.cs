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
        protected readonly IVimLocalKeyMap _map;
        protected readonly Dictionary<string, VariableValue> _variableMap;

        public KeyMapTest()
        {
            _globalSettings = new GlobalSettings();
            _variableMap = new Dictionary<string, VariableValue>();
            _map = new LocalKeyMap(new GlobalKeyMap(_variableMap), _globalSettings, _variableMap);
        }

        protected void AssertNoMapping(string lhs, KeyRemapMode mode = null)
        {
            AssertNoMapping(KeyNotationUtil.StringToKeyInputSet(lhs), mode);
        }

        protected void AssertNoMapping(KeyInputSet lhs, KeyRemapMode mode = null)
        {
            mode = mode ?? KeyRemapMode.Normal;

            Assert.DoesNotContain(_map.GetKeyMappings(mode), keyMapping => keyMapping.Left == lhs);

            var result = _map.Map(lhs, mode);
            if (lhs.Length == 1)
            {
                Assert.True(result.IsMapped || result.IsUnmapped);
            }
            else
            {
                Assert.True(result.IsPartiallyMapped);
                Assert.Equal(lhs.FirstKeyInput.Value, result.AsPartiallyMapped().MappedKeyInputSet.FirstKeyInput.Value);
                Assert.Equal(1, result.AsPartiallyMapped().MappedKeyInputSet.Length);
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
            var ret = _map.Map(lhs, mode);
            Assert.True(ret.IsMapped || ret.IsUnmapped);
            Assert.Equal(KeyInputSetUtil.OfString(expected), ret.GetMappedKeyInputs());
        }

        protected void AssertPartialMapping(string lhs, string expectedMapped, string expectedRemaining, KeyRemapMode mode = null)
        {
            AssertPartialMapping(KeyInputSetUtil.OfString(lhs), expectedMapped, expectedRemaining, mode);
        }

        protected void AssertPartialMapping(KeyInputSet lhs, string expectedMapped, string expectedRemaining, KeyRemapMode mode = null)
        {
            mode = mode ?? KeyRemapMode.Normal;
            var ret = _map.Map(lhs, mode);
            Assert.True(ret.IsPartiallyMapped);

            var partiallyMapped = ret.AsPartiallyMapped();
            Assert.Equal(KeyInputSetUtil.OfString(expectedMapped), partiallyMapped.MappedKeyInputSet);
            Assert.Equal(KeyInputSetUtil.OfString(expectedRemaining), partiallyMapped.RemainingKeyInputSet);
        }

        private KeyInput MapSingle(string lhs, KeyRemapMode mode = null)
        {
            mode = mode ?? KeyRemapMode.Normal;
            var result = _map.Map(KeyNotationUtil.StringToKeyInputSet(lhs), mode);
            return result.AsMapped().KeyInputSet.KeyInputs.Single();
        }

        public sealed class MapWithNoRemapTest : KeyMapTest
        {
            private void Map(string lhs, string rhs)
            {
                Assert.True(_map.AddKeyMapping(lhs, rhs, allowRemap: false, KeyRemapMode.Normal));
            }

            [Fact]
            public void AlphaToAlpha()
            {
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Normal));
                var ret = MapSingle("a");
                Assert.Equal(KeyInputUtil.CharToKeyInput('b'), ret);
            }

            [Fact]
            public void AlphaToDigit()
            {
                Assert.True(_map.AddKeyMapping("a", "1", allowRemap: false, KeyRemapMode.Normal));
                var ret = MapSingle("a");
                Assert.Equal(KeyInputUtil.CharToKeyInput('1'), ret);
            }

            [Fact]
            public void ManyAlphaToSingle()
            {
                Assert.True(_map.AddKeyMapping("ab", "b", allowRemap: false, KeyRemapMode.Normal));
                var ret = MapSingle("ab");
                Assert.Equal(KeyInputUtil.CharToKeyInput('b'), ret);
            }

            [Fact]
            public void SymbolToSymbol()
            {
                Assert.True(_map.AddKeyMapping("&", "!", allowRemap: false, KeyRemapMode.Normal));
                var ret = MapSingle("&", KeyRemapMode.Normal);
                Assert.Equal(KeyInputUtil.CharToKeyInput('!'), ret);
            }

            [Fact]
            public void OneAlphaToTwo()
            {
                Assert.True(_map.AddKeyMapping("a", "bc", allowRemap: false, KeyRemapMode.Normal));
                var ret = _map.Map("a", KeyRemapMode.Normal).AsMapped().KeyInputSet.KeyInputs.ToList();
                Assert.Equal(2, ret.Count);
                Assert.Equal('b', ret[0].Char);
                Assert.Equal('c', ret[1].Char);
            }

            [Fact]
            public void OneAlphaToThree()
            {
                Assert.True(_map.AddKeyMapping("a", "bcd", allowRemap: false, KeyRemapMode.Normal));
                var ret = _map.Map("a", KeyRemapMode.Normal).AsMapped().KeyInputSet.KeyInputs.ToList();
                Assert.Equal(3, ret.Count);
                Assert.Equal('b', ret[0].Char);
                Assert.Equal('c', ret[1].Char);
                Assert.Equal('d', ret[2].Char);
            }

            [Fact]
            public void DontRemapEmptyString()
            {
                Assert.False(_map.AddKeyMapping("a", "", allowRemap: false, KeyRemapMode.Normal));
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
                keyInput = KeyInputUtil.ChangeKeyModifiersDangerous(keyInput, VimKeyModifiers.Shift);
                Assert.True(_map.Map(keyInput, KeyRemapMode.Normal).IsMapped);
            }

            [Fact]
            public void LessThanChar()
            {
                Assert.True(_map.AddKeyMapping("<", "pound", allowRemap: false, KeyRemapMode.Normal));
            }

            /// <summary>
            /// By default the '\' character isn't special in key mappings.  It's treated like any
            /// other character.  It only achieves special meaning when 'B' is excluded from the 
            /// 'cpoptions' setting
            /// </summary>
            [Fact]
            public void EscapeLessThanSymbol()
            {
                Assert.True(_map.AddKeyMapping("a", @"\<Home>", allowRemap: false, KeyRemapMode.Normal));
                var result = _map.Map("a", KeyRemapMode.Normal);
                Assert.Equal(KeyNotationUtil.StringToKeyInputSet(@"\<Home>"), result.AsMapped().KeyInputSet);
            }

            [Fact]
            public void HandleLessThanEscapeLiteral()
            {
                Assert.True(_map.AddKeyMapping("a", "<lt>lt>", allowRemap: false, KeyRemapMode.Normal));
                var result = _map.Map("a", KeyRemapMode.Normal);
                Assert.Equal(KeyInputSetUtil.OfString("<lt>"), result.AsMapped().KeyInputSet);
            }

            [Fact]
            public void ControlAlphaIsCaseInsensitive()
            {
                Assert.True(_map.AddKeyMapping("<C-a>", "1", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("<C-A>", "2", allowRemap: false, KeyRemapMode.Normal));
                var ret = MapSingle("<C-a>", KeyRemapMode.Normal);
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
                ret = MapSingle("<C-A>", KeyRemapMode.Normal);
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
            }

            [Fact]
            public void AltAlphaIsCaseInsensitive()
            {
                Assert.True(_map.AddKeyMapping("<A-a>", "1", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("<A-A>", "2", allowRemap: false, KeyRemapMode.Normal));
                var ret = MapSingle("<A-a>", KeyRemapMode.Normal);
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
                ret = MapSingle("<A-A>", KeyRemapMode.Normal);
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
            }

            [Fact]
            public void AltAlphaSupportsShift()
            {
                Assert.True(_map.AddKeyMapping("<A-A>", "1", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("<A-S-A>", "2", allowRemap: false, KeyRemapMode.Normal));
                var ret = MapSingle("<A-A>", KeyRemapMode.Normal);
                Assert.Equal(KeyInputUtil.CharToKeyInput('1'), ret);
                ret = MapSingle("<A-S-A>", KeyRemapMode.Normal);
                Assert.Equal(KeyInputUtil.CharToKeyInput('2'), ret);
            }

            /// <summary>
            /// When two mappnigs have the same prefix then they are ambiguous and require a
            /// tie breaker input
            /// </summary>
            [Fact]
            public void Ambiguous()
            {
                Assert.True(_map.AddKeyMapping("aa", "foo", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("aaa", "bar", allowRemap: false, KeyRemapMode.Normal));
                var ret = _map.Map("aa", KeyRemapMode.Normal);
                Assert.True(ret.IsNeedsMoreInput);
            }

            /// <summary>
            /// Resloving the ambiguity should cause both the original plus the next input to be 
            /// returned
            /// </summary>
            [Fact]
            public void Ambiguous_ResolveShorter()
            {
                Assert.True(_map.AddKeyMapping("aa", "foo", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("aaa", "bar", allowRemap: false, KeyRemapMode.Normal));
                AssertPartialMapping("aab", "foo", "b");
            }

            [Fact]
            public void Ambiguous_ResolveLonger()
            {
                Assert.True(_map.AddKeyMapping("aa", "foo", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("aaa", "bar", allowRemap: false, KeyRemapMode.Normal));
                var ret = _map.Map("aaa", KeyRemapMode.Normal);
                Assert.True(ret.IsMapped);
                Assert.Equal(KeyInputSetUtil.OfString("bar"), ret.AsMapped().KeyInputSet);
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
                Assert.True(_map.AddKeyMapping(lhs, rhs, allowRemap: true, KeyRemapMode.Normal));
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
                var ki = new KeyInput(VimKey.RawCharacter, VimKeyModifiers.Command, FSharpOption.Create('k'));
                var kiSet = new KeyInputSet(ki);
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
                Assert.True(_map.Map("k", KeyRemapMode.Normal).IsRecursive);
                Assert.True(_map.Map("j", KeyRemapMode.Normal).IsRecursive);
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
                _map.AddKeyMapping("<Leader>", "y", allowRemap: false, KeyRemapMode.Normal);
                AssertMapping("x", "y");
            }

            [Fact]
            public void SimpleLeftWithNoMapping()
            {
                _map.AddKeyMapping("<Leader>", "y", allowRemap: false, KeyRemapMode.Normal);
                AssertMapping(@"\", "y");
            }

            [Fact]
            public void SimpleRight()
            {
                _variableMap["mapleader"] = VariableValue.NewString("y");
                _map.AddKeyMapping("x", "<Leader>", allowRemap: false, KeyRemapMode.Normal);
                AssertMapping("x", "y");
            }

            [Fact]
            public void SimpleRightWithNoMapping()
            {
                _map.AddKeyMapping("x", "<Leader>", allowRemap: false, KeyRemapMode.Normal);
                AssertMapping("x", @"\");
            }

            [Fact]
            public void LowerCaseLeader()
            {
                _map.AddKeyMapping("x", "<leader>", allowRemap: false, KeyRemapMode.Normal);
                AssertMapping("x", @"\");
            }

            [Fact]
            public void MixedCaseLeader()
            {
                _map.AddKeyMapping("x", "<lEaDer>", allowRemap: false, KeyRemapMode.Normal);
                AssertMapping("x", @"\");
            }
        }

        public sealed class ZeroCountTest : KeyMapTest
        {
            private void Map(string lhs, string rhs)
            {
                Assert.True(_map.AddKeyMapping(lhs, rhs, allowRemap: true, KeyRemapMode.Normal));
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
                Assert.True(_map.AddKeyMapping(lhs, rhs, allowRemap: true, KeyRemapMode.Normal));
            }

            [Fact]
            public void GetKeyMapping1()
            {
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: true, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("b", "a", allowRemap: true, KeyRemapMode.Normal));
                var ret = Extensions.Map(_map, 'a', KeyRemapMode.Normal);
                Assert.True(ret.IsRecursive);
            }

            [Fact]
            public void GetKeyMappingResult1()
            {
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: true, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("b", "a", allowRemap: true, KeyRemapMode.Normal));
                var ret = _map.Map(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
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
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Normal));
                var res = _map.Map(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.True(res.IsMapped);
                Assert.Equal('b', res.AsMapped().KeyInputSet.KeyInputs.Single().Char);
            }

            [Fact]
            public void GetKeyMappingResult4()
            {
                Assert.True(_map.AddKeyMapping("a", "bc", allowRemap: false, KeyRemapMode.Normal));
                var res = _map.Map(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.True(res.IsMapped);
                var list = res.AsMapped().KeyInputSet.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal('b', list[0].Char);
                Assert.Equal('c', list[1].Char);
            }

            [Fact]
            public void GetKeyMappingResult5()
            {
                Assert.True(_map.AddKeyMapping("aa", "b", allowRemap: false, KeyRemapMode.Normal));
                var res = _map.Map(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.True(res.IsNeedsMoreInput);
            }

            /// <summary>
            /// If GetKeyMapping is called with a string that has no mapping then only the first
            /// key is considered unmappable.  The rest is still eligable for mapping 
            /// </summary>
            [Fact]
            public void GetKeyMappingResult_RemainderIsMappable()
            {
                var result = _map.Map("dog", KeyRemapMode.Normal).AsPartiallyMapped();
                Assert.Equal(KeyInputSetUtil.OfString("d"), result.MappedKeyInputSet);
                Assert.Equal(KeyInputSetUtil.OfString("og"), result.RemainingKeyInputSet);
            }

            /// <summary>
            /// Once the mapping is cleared it goes back to an identity mapping since there isn't anything
            /// </summary>
            [Fact]
            public void Clear1()
            {
                MapWithRemap("a", "b");
                _map.ClearKeyMappings(KeyRemapMode.Normal);
                AssertNoMapping("a");
            }

            /// <summary>
            /// Make sure we only clear the specified mode 
            /// </summary>
            [Fact]
            public void Clear2()
            {
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Insert));
                _map.ClearKeyMappings(KeyRemapMode.Normal);
                var res = _map.Map(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Insert);
                Assert.True(res.IsMapped);
                Assert.Equal('b', res.AsMapped().KeyInputSet.KeyInputs.Single().Char);
            }

            [Fact]
            public void ClearAll()
            {
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Insert));
                _map.ClearKeyMappings();
                AssertNoMapping("a", KeyRemapMode.Normal);
                AssertNoMapping("a", KeyRemapMode.Insert);
            }

            /// <summary>
            /// Unmapping a specific entry removes it completely
            /// </summary>
            [Fact]
            public void Unmap1()
            {
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Normal));
                Assert.True(_map.RemoveKeyMapping("a", KeyRemapMode.Normal));
                AssertNoMapping("a");
            }

            [Fact]
            public void Unmap2()
            {
                Assert.True(_map.AddKeyMapping("a", "b", allowRemap: false, KeyRemapMode.Normal));
                Assert.False(_map.RemoveKeyMapping("a", KeyRemapMode.Insert));
                Assert.True(_map.Map(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).IsMapped);
            }

            [Fact]
            public void GetKeyMappingResultFromMultiple1()
            {
                _map.AddKeyMapping("aa", "b", allowRemap: false, KeyRemapMode.Normal);

                var input = "aa".Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
                var res = _map.Map(new KeyInputSet(input), KeyRemapMode.Normal);
                Assert.Equal('b', res.AsMapped().KeyInputSet.KeyInputs.Single().Char);
            }

            [Fact]
            public void GetKeyMappingResultFromMultiple2()
            {
                _map.AddKeyMapping("aa", "b", allowRemap: false, KeyRemapMode.Normal);

                var input = "a".Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
                var res = _map.Map(new KeyInputSet(input), KeyRemapMode.Normal);
                Assert.True(res.IsNeedsMoreInput);
            }

            [Fact]
            public void Issue328()
            {
                Assert.True(_map.AddKeyMapping("<S-SPACE>", "<ESC>", allowRemap: false, KeyRemapMode.Insert));
                var res = _map.Map(KeyInputUtil.ApplyKeyModifiersToChar(' ', VimKeyModifiers.Shift), KeyRemapMode.Insert).AsMapped().KeyInputSet.KeyInputs.Single();
                Assert.Equal(KeyInputUtil.EscapeKey, res);
            }

            [Fact]
            public void Issue1059()
            {
                Assert.True(_map.AddKeyMapping("/v", "<hello>", allowRemap: false, KeyRemapMode.Insert));
                AssertMapping("/v", "<hello>", KeyRemapMode.Insert);
            }
        }
    }
}
