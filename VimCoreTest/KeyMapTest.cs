using System.Linq;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class KeyMapTest
    {
        protected IVimGlobalSettings _globalSettings;
        protected IKeyMap _map;
        internal KeyMap _mapRaw;

        [SetUp]
        public void SetUp()
        {
            _globalSettings = new GlobalSettings();
            _mapRaw = new KeyMap(_globalSettings);
            _map = _mapRaw;
        }

        protected void AssertNoMapping(string lhs, KeyRemapMode mode = null)
        {
            AssertNoMapping(KeyNotationUtil.StringToKeyInputSet(lhs), mode);
        }

        protected void AssertNoMapping(KeyInputSet lhs, KeyRemapMode mode = null)
        {
            Assert.IsFalse(_map.GetKeyMappingsForMode(mode).Any(keyMapping => keyMapping.Left == lhs));

            mode = mode ?? KeyRemapMode.Normal;
            var result = _map.GetKeyMappingResult(lhs, mode);
            if (lhs.Length == 1)
            {
                Assert.IsTrue(result.IsMapped);
            }
            else 
            {
                Assert.IsTrue(result.IsPartiallyMapped);
                Assert.AreEqual(lhs.FirstKeyInput.Value, result.AsPartiallyMapped().Item1.FirstKeyInput.Value);
                Assert.AreEqual(1, result.AsPartiallyMapped().Item1.Length);
            }

            Assert.AreEqual(lhs, result.GetMappedKeyInputs());
        }

        protected void AssertMapping(string lhs, string expected, KeyRemapMode mode = null)
        {
            AssertMapping(KeyNotationUtil.StringToKeyInputSet(lhs), expected, mode);
        }

        protected void AssertMapping(KeyInputSet lhs, string expected, KeyRemapMode mode = null)
        {
            mode = mode ?? KeyRemapMode.Normal;
            var ret = _map.GetKeyMappingResult(lhs, mode);
            Assert.IsTrue(ret.IsMapped);
            Assert.AreEqual(KeyInputSetUtil.OfString(expected), ret.GetMappedKeyInputs());
        }

        protected void AssertPartialMapping(string lhs, string expectedMapped, string expectedRemaining, KeyRemapMode mode = null)
        {
            AssertPartialMapping(KeyInputSetUtil.OfString(lhs), expectedMapped, expectedRemaining, mode);
        }

        protected void AssertPartialMapping(KeyInputSet lhs, string expectedMapped, string expectedRemaining, KeyRemapMode mode = null)
        {
            mode = mode ?? KeyRemapMode.Normal;
            var ret = _map.GetKeyMappingResult(lhs, mode);
            Assert.IsTrue(ret.IsPartiallyMapped);

            var partiallyMapped = ret.AsPartiallyMapped();
            Assert.AreEqual(KeyInputSetUtil.OfString(expectedMapped), partiallyMapped.Item1);
            Assert.AreEqual(KeyInputSetUtil.OfString(expectedRemaining), partiallyMapped.Item2);
        }

        [TestFixture]
        public sealed class MapWithNoRemapTest : KeyMapTest
        {
            private void Map(string lhs, string rhs)
            {
                Assert.IsTrue(_map.MapWithNoRemap(lhs, rhs, KeyRemapMode.Normal));
            }

            [Test]
            public void AlphaToAlpha()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('a', KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('b'), ret);
            }

            [Test]
            public void AlphaToDigit()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "1", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('a', KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('1'), ret);
            }

            [Test]
            public void ManyAlphaToSingle()
            {
                Assert.IsTrue(_map.MapWithNoRemap("ab", "b", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping("ab", KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('b'), ret);
            }

            [Test]
            public void SymbolToSymbol()
            {
                Assert.IsTrue(_map.MapWithNoRemap("&", "!", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('&', KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('!'), ret);
            }

            [Test]
            public void OneAlphaToTwo()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "bc", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping('a', KeyRemapMode.Normal).ToList();
                Assert.AreEqual(2, ret.Count);
                Assert.AreEqual('b', ret[0].Char);
                Assert.AreEqual('c', ret[1].Char);
            }

            [Test]
            public void OneAlphaToThree()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "bcd", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).ToList();
                Assert.AreEqual(3, ret.Count);
                Assert.AreEqual('b', ret[0].Char);
                Assert.AreEqual('c', ret[1].Char);
                Assert.AreEqual('d', ret[2].Char);
            }

            [Test]
            public void DontRemapEmptyString()
            {
                Assert.IsFalse(_map.MapWithNoRemap("a", "", KeyRemapMode.Normal));
            }

            [Test]
            public void ShiftPromotesAlpha()
            {
                Map("<S-a>", "#");
                AssertNoMapping("a");
                AssertMapping("A", "#");
            }

            [Test]
            public void ShiftWithUpperAlphaIsJustUpperAlpha()
            {
                Map("<S-a>", "#");
                AssertNoMapping("a");
                AssertMapping("A", "#");
            }

            [Test]
            public void ShiftSymbolDoesNotChangeChar()
            {
                Map("<S-#>", "pound");
                AssertNoMapping("#");
                var keyInput = KeyInputUtil.CharToKeyInput('#');
                keyInput = KeyInputUtil.ChangeKeyModifiersDangerous(keyInput, KeyModifiers.Shift);
                Assert.IsTrue(_map.GetKeyMappingResult(keyInput, KeyRemapMode.Normal).IsMapped);
            }

            [Test]
            public void LessThanChar()
            {
                Assert.IsTrue(_map.MapWithNoRemap("<", "pound", KeyRemapMode.Normal));
            }

            /// <summary>
            /// By default the '\' character isn't special in key mappings.  It's treated like any
            /// other character.  It only achieves special meaning when 'B' is excluded from the 
            /// 'cpoptions' setting
            /// </summary>
            [Test]
            public void EscapeLessThanSymbol()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", @"\<Home>", KeyRemapMode.Normal));
                var result = _map.GetKeyMappingResult("a", KeyRemapMode.Normal);
                Assert.AreEqual(KeyInputSetUtil.OfVimKeyArray(VimKey.Backslash, VimKey.Home), result.AsMapped().Item);
            }

            [Test]
            public void HandleLessThanEscapeLiteral()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "<lt>lt>", KeyRemapMode.Normal));
                var result = _map.GetKeyMappingResult("a", KeyRemapMode.Normal);
                Assert.AreEqual(KeyInputSetUtil.OfString("<lt>"), result.AsMapped().Item);
            }

            [Test]
            public void ControlAlphaIsCaseInsensitive()
            {
                Assert.IsTrue(_map.MapWithNoRemap("<C-a>", "1", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithNoRemap("<C-A>", "2", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping("<C-a>", KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('2'), ret);
                ret = _map.GetKeyMapping("<C-A>", KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('2'), ret);
            }

            [Test]
            public void AltAlphaIsCaseSensitive()
            {
                Assert.IsTrue(_map.MapWithNoRemap("<A-a>", "1", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithNoRemap("<A-A>", "2", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping("<A-a>", KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('1'), ret);
                ret = _map.GetKeyMapping("<A-A>", KeyRemapMode.Normal).Single();
                Assert.AreEqual(KeyInputUtil.CharToKeyInput('2'), ret);
            }

            /// <summary>
            /// When two mappnigs have the same prefix then they are ambiguous and require a
            /// tie breaker input
            /// </summary>
            [Test]
            public void Ambiguous()
            {
                Assert.IsTrue(_map.MapWithNoRemap("aa", "foo", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithNoRemap("aaa", "bar", KeyRemapMode.Normal));
                var ret = _map.GetKeyMappingResult("aa", KeyRemapMode.Normal);
                Assert.IsTrue(ret.IsNeedsMoreInput);
            }

            /// <summary>
            /// Resloving the ambiguity should cause both the original plus the next input to be 
            /// returned
            /// </summary>
            [Test]
            public void Ambiguous_ResolveShorter()
            {
                Assert.IsTrue(_map.MapWithNoRemap("aa", "foo", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithNoRemap("aaa", "bar", KeyRemapMode.Normal));
                AssertPartialMapping("aab", "foo", "b");
            }

            [Test]
            public void Ambiguous_ResolveLonger()
            {
                Assert.IsTrue(_map.MapWithNoRemap("aa", "foo", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithNoRemap("aaa", "bar", KeyRemapMode.Normal));
                var ret = _map.GetKeyMappingResult("aaa", KeyRemapMode.Normal);
                Assert.IsTrue(ret.IsMapped);
                Assert.AreEqual(KeyInputSetUtil.OfString("bar"), ret.AsMapped().Item);
            }

            /// <summary>
            /// In the ambiguous double case we will end up with the best possible mapping
            /// for the given input here.  The first mapping can be resolved but not the
            /// second
            /// </summary>
            [Test]
            public void Ambiguous_Double()
            {
                Map("aa", "one");
                Map("aaa", "two");
                Map("b", "three");
                Map("bb", "four");
                AssertPartialMapping("aab", "one", "b");
                AssertMapping("aaa", "two");
            }

            [Test]
            public void Ambiguous_DoubleResolve()
            {
                Map("aa", "one");
                Map("aaa", "two");
                Map("b", "three");
                Map("bb", "four");
                AssertPartialMapping("aabc", "one", "bc");
            }
        }

        [TestFixture]
        public sealed class MapWithRemapTest : KeyMapTest
        {
            private void Map(string lhs, string rhs)
            {
                Assert.IsTrue(_map.MapWithRemap(lhs, rhs, KeyRemapMode.Normal));
            }

            /// <summary>
            /// Simple mapping with no remap involved
            /// </summary>
            [Test]
            public void Simple()
            {
                Map("a", "b");
                AssertMapping("a", "b");
            }

            [Test]
            public void SimpleLong()
            {
                Map("a", "bcd");
                AssertPartialMapping("a", "b", "cd");
            }

            [Test]
            public void SimpleChain()
            {
                Map("a", "b");
                Map("b", "c");
                AssertMapping("a", "c");
                AssertMapping("b", "c");
            }

            [Test]
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
            [Test]
            public void MapWithRemapSameKey()
            {
                Map("j", "3j");
                AssertPartialMapping("j", "3", "j");
            }

            /// <summary>
            /// If the rhs begins with the lhs then it shouldn't produce an infinite recursive 
            /// mapping.  
            /// </summary>
            [Test]
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
            [Test]
            public void RightStartsWithLeftComplex()
            {
                Map("ab", "abcd");
                Map("b", "hit");
                AssertPartialMapping("ab", "a", "bcd");
            }

            [Test]
            public void HandleCommandKey()
            {
                Map("<D-k>", "gk");
                var ki = new KeyInput(VimKey.LowerK, KeyModifiers.Command, FSharpOption.Create('k'));
                var kiSet = KeyInputSet.NewOneKeyInput(ki);
                AssertPartialMapping(kiSet, "g", "k");
            }

            /// <summary>
            /// When processing a remap if the {rhs} doesn't map and has length greater
            /// than 0 then we shouldn't be considering the remainder here.  Once the {lhs} isn't 
            /// mappable at column 0 we are done.  It's up to the IVimBuffer to give us back the 
            /// remainder in the proper mode based on the processing of the first value
            /// </summary>
            [Test]
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
            [Test]
            public void RecursivePrefix()
            {
                Map("ab", "abcd");
                AssertPartialMapping("ab", "a", "bcd");
            }

            /// <summary>
            /// Mutually recursive mappings should cause a recursive result once we hit the 
            /// maxmapdept number of recursions (:help recursive_mapping)
            /// </summary>
            [Test]
            public void RecursiveMutually()
            {
                // Make the depth high in the unit tests to ensure that we aren't doing head recursion 
                // here
                _globalSettings.MaxMapDepth = 1000;
                Map("k", "j");
                Map("j", "k");
                Assert.IsTrue(_map.GetKeyMappingResult("k", KeyRemapMode.Normal).IsRecursive);
                Assert.IsTrue(_map.GetKeyMappingResult("j", KeyRemapMode.Normal).IsRecursive);
            }

            [Test]
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
            [Test]
            public void RestShouldBeRemappable()
            {
                Map("a", "could");
                AssertPartialMapping("a", "c", "ould");
            }

            /// <summary>
            /// If a remap operation resolves to a single value which itself is unmappable then 
            /// the mapping is complete
            /// </summary>
            [Test]
            public void SingleIsComplete()
            {
                Map("cat", "a");
                Map("a", "b");
                AssertMapping("cat", "b");
            }
        }

        [TestFixture]
        public sealed class Misc : KeyMapTest
        {
            private void MapWithRemap(string lhs, string rhs)
            {
                Assert.IsTrue(_map.MapWithRemap(lhs, rhs, KeyRemapMode.Normal));
            }

            [Test]
            public void GetKeyMapping1()
            {
                Assert.IsTrue(_map.MapWithRemap("a", "b", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithRemap("b", "a", KeyRemapMode.Normal));
                var ret = _map.GetKeyMapping(KeyInputSetUtil.OfChar('a'), KeyRemapMode.Normal);
                Assert.IsTrue(ret.IsRecursive);
            }

            [Test]
            public void GetKeyMappingResult1()
            {
                Assert.IsTrue(_map.MapWithRemap("a", "b", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithRemap("b", "a", KeyRemapMode.Normal));
                var ret = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.IsTrue(ret.IsRecursive);
            }

            /// <summary>
            /// If a particular value isn't mapped then the GetKeyMappingResult functions 
            /// should just be an identity function
            /// </summary>
            [Test]
            public void GetKeyMappingResult2()
            {
                AssertNoMapping("b");
                AssertNoMapping("a");
            }

            [Test]
            public void GetKeyMapppingResult3()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.IsTrue(res.IsMapped);
                Assert.AreEqual('b', res.AsMapped().Item.KeyInputs.Single().Char);
            }

            [Test]
            public void GetKeyMappingResult4()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "bc", KeyRemapMode.Normal));
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.IsTrue(res.IsMapped);
                var list = res.AsMapped().Item.KeyInputs.ToList();
                Assert.AreEqual(2, list.Count);
                Assert.AreEqual('b', list[0].Char);
                Assert.AreEqual('c', list[1].Char);
            }

            [Test]
            public void GetKeyMappingResult5()
            {
                Assert.IsTrue(_map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal));
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
                Assert.IsTrue(res.IsNeedsMoreInput);
            }

            /// <summary>
            /// If GetKeyMapping is called with a string that has no mapping then only the first
            /// key is considered unmappable.  The rest is still eligable for mapping 
            /// </summary>
            [Test]
            public void GetKeyMappingResult_RemainderIsMappable()
            {
                var result = _map.GetKeyMappingResult("dog", KeyRemapMode.Normal).AsPartiallyMapped();
                Assert.AreEqual(KeyInputSetUtil.OfString("d"), result.Item1);
                Assert.AreEqual(KeyInputSetUtil.OfString("og"), result.Item2);
            }

            /// <summary>
            /// Once the mapping is cleared it goes back to an identity mapping since there isn't anything
            /// </summary>
            [Test]
            public void Clear1()
            {
                MapWithRemap("a", "b");
                _map.Clear(KeyRemapMode.Normal);
                AssertNoMapping("a");
            }

            /// <summary>
            /// Make sure we only clear the specified mode 
            /// </summary>
            [Test, Description("Only clear the specified mode")]
            public void Clear2()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Insert));
                _map.Clear(KeyRemapMode.Normal);
                var res = _map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Insert);
                Assert.IsTrue(res.IsMapped);
                Assert.AreEqual('b', res.AsMapped().Item.KeyInputs.Single().Char);
            }

            [Test]
            public void ClearAll()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Insert));
                _map.ClearAll();
                AssertNoMapping("a", KeyRemapMode.Normal);
                AssertNoMapping("a", KeyRemapMode.Insert);
            }

            /// <summary>
            /// Unmapping a specific entry removes it completely
            /// </summary>
            [Test]
            public void Unmap1()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.IsTrue(_map.Unmap("a", KeyRemapMode.Normal));
                AssertNoMapping("a");
            }

            [Test]
            public void Unmap2()
            {
                Assert.IsTrue(_map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
                Assert.IsFalse(_map.Unmap("a", KeyRemapMode.Insert));
                Assert.IsTrue(_map.GetKeyMappingResult(KeyInputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).IsMapped);
            }

            /// <summary>
            /// Straight forward unmap of via the mapping instead of the key 
            /// </summary>
            [Test]
            public void UnmapByMapping_Simple()
            {
                Assert.IsTrue(_map.MapWithNoRemap("cat", "dog", KeyRemapMode.Insert));
                Assert.IsTrue(_map.UnmapByMapping("dog", KeyRemapMode.Insert));
                AssertNoMapping("cat");
            }

            /// <summary>
            /// Don't allow the unmapping by the key
            /// </summary>
            [Test]
            public void UnmapByMapping_Bad()
            {
                Assert.IsTrue(_map.MapWithNoRemap("cat", "dog", KeyRemapMode.Insert));
                Assert.IsFalse(_map.UnmapByMapping("cat", KeyRemapMode.Insert));
            }

            [Test]
            public void GetKeyMappingResultFromMultiple1()
            {
                _map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal);

                var input = "aa".Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
                var res = _map.GetKeyMapping(KeyInputSet.NewManyKeyInputs(input), KeyRemapMode.Normal);
                Assert.AreEqual('b', res.AsMapped().Item.KeyInputs.Single().Char);
            }

            [Test]
            public void GetKeyMappingResultFromMultiple2()
            {
                _map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal);

                var input = "a".Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
                var res = _map.GetKeyMapping(KeyInputSet.NewManyKeyInputs(input), KeyRemapMode.Normal);
                Assert.IsTrue(res.IsNeedsMoreInput);
            }

            [Test]
            public void Issue328()
            {
                Assert.IsTrue(_map.MapWithNoRemap("<S-SPACE>", "<ESC>", KeyRemapMode.Insert));
                var res = _map.GetKeyMapping(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Space, KeyModifiers.Shift), KeyRemapMode.Insert);
                Assert.AreEqual(KeyInputUtil.EscapeKey, res.Single());
            }
        }
    }
}
