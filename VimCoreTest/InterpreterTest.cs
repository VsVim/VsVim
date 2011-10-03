using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Interpreter;
using Vim.UnitTest;
using System;
using System.Linq;
using Vim.Extensions;

namespace VimCore.UnitTest
{
    public sealed class InterpreterTest : VimTestBase
    {
        private VimBufferData _vimBufferData;
        private IVimTextBuffer _vimTextBuffer;
        private ITextBuffer _textBuffer;
        private Interpreter _interpreter;
        private TestableStatusUtil _statusUtil;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private IRegisterMap _registerMap;
        private IKeyMap _keyMap;

        private void Create(params string[] lines)
        {
            _statusUtil = new TestableStatusUtil();
            _vimBufferData = CreateVimBufferData(
                CreateTextView(lines),
                statusUtil: _statusUtil);
            _vimTextBuffer = _vimBufferData.VimTextBuffer;
            _localSettings = _vimBufferData.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _textBuffer = _vimBufferData.TextBuffer;
            _interpreter = new Interpreter(
                _vimBufferData,
                CommonOperationsFactory.GetCommonOperations(_vimBufferData),
                FoldManagerFactory.GetFoldManager(_vimBufferData.TextView),
                new FileSystem());
            _registerMap = Vim.RegisterMap;
            _keyMap = Vim.KeyMap;
        }

        /// <summary>
        /// Parse and run the specified command
        /// </summary>
        private void ParseAndRun(string command)
        {
            var parseResult = Parser.ParseLineCommand(command);
            Assert.IsTrue(parseResult.IsSucceeded);
            _interpreter.RunLineCommand(parseResult.AsSucceeded().Item);
        }

        private LineRange ParseLineRange(string lineRangeText)
        {
            var parseResult = Parser.ParseRange(lineRangeText);
            Assert.IsTrue(parseResult.IsSucceeded);
            Assert.AreEqual("", parseResult.AsSucceeded().Item.Item2);
            return parseResult.AsSucceeded().Item.Item1;
        }

        private SnapshotLineRange ParseAndGetLineRange(string lineRangeText)
        {
            var lineRange = ParseLineRange(lineRangeText);
            return _interpreter.GetLineRange(lineRange).Value;
        }

        /// <summary>
        /// Handle the case where the adjustment simply occurs on the current line 
        /// </summary>
        [Test]
        public void GetLine_AdjustmentOnCurrent()
        {
            Create("cat", "dog", "bear");
            var range = _interpreter.GetLine(LineSpecifier.NewAdjustmentOnCurrent(1));
            Assert.AreEqual(_textBuffer.GetLine(1).LineNumber, range.Value.LineNumber);
        }

        [Test]
        public void LineRange_FullFile()
        {
            Create("foo", "bar");
            var lineRange = ParseAndGetLineRange("%");
            var tss = _textBuffer.CurrentSnapshot;
            Assert.AreEqual(new SnapshotSpan(tss, 0, tss.Length), lineRange.ExtentIncludingLineBreak);
        }

        [Test]
        public void LineRange_CurrentLine()
        {
            Create("foo", "bar");
            var lineRange = ParseAndGetLineRange(".");
            Assert.AreEqual(_textBuffer.GetLineRange(0), lineRange);
        }

        [Test]
        public void LineRange_CurrentLineWithCurrentLine()
        {
            Create("foo", "bar");
            var lineRange = ParseAndGetLineRange(".,.");
            Assert.AreEqual(_textBuffer.GetLineRange(0), lineRange);
        }

        [Test]
        public void LineRange_LineNumberRange()
        {
            Create("a", "b", "c");
            var lineRange = ParseAndGetLineRange("1,2");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), lineRange);
        }

        [Test]
        public void LineRange_SingleLine1()
        {
            Create("foo", "bar");
            var lineRange = ParseAndGetLineRange("1");
            Assert.AreEqual(0, lineRange.StartLineNumber);
            Assert.AreEqual(1, lineRange.Count);
        }

        [Test]
        public void LineRange_MarkWithLineNumber()
        {
            Create("foo", "bar", "tree");
            _vimTextBuffer.SetLocalMark(LocalMark.OfChar('c').Value, 0, 1);
            var lineRange = ParseAndGetLineRange("'c,2");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), lineRange);
        }

        [Test]
        public void LineRange_MarkWithMark()
        {
            Create("foo", "bar");

            _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.C), 0, 0);
            _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.B), 1, 0);

            var lineRange = ParseAndGetLineRange("'c,'b");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), lineRange);
        }

        /// <summary>
        /// Combine a global mark with a line number
        /// </summary>
        [Test]
        public void LineRange_GlobalMarkAndLineNumber()
        {
            Create("foo bar", "bar", "baz");
            Vim.MarkMap.SetGlobalMark(Letter.A, _vimTextBuffer, 0, 2);
            var lineRange = ParseAndGetLineRange("'A,2");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 1), lineRange);
        }

        /// <summary>
        /// Can't get the range for a mark that doesn't exist
        /// </summary>
        [Test]
        public void LineRange_BadMark()
        {
            Create("foo bar", "baz");
            var lineRange = _interpreter.GetLineRange(ParseLineRange("'c,2"));
            Assert.IsTrue(lineRange.IsNone());
        }

        [Test]
        public void LineRange_LineNumberWithPlus()
        {
            Create("foo", "bar", "baz", "jaz");
            var lineRange = ParseAndGetLineRange("1+2");
            Assert.AreEqual(2, lineRange.StartLineNumber);
            Assert.AreEqual(1, lineRange.Count);
        }

        /// <summary>
        /// Make sure that we treat a plus with no trailing value as a + 1
        /// </summary>
        [Test]
        public void LineRange_LineNumberWithPlusAndNoValue()
        {
            Create("foo", "bar", "baz");
            var lineRange = ParseAndGetLineRange("1+");
            Assert.AreEqual(1, lineRange.StartLineNumber);
            Assert.AreEqual(1, lineRange.Count);
        }

        /// <summary>
        /// Test the + with a range
        /// </summary>
        [Test]
        public void LineRange_LineNumberWithPlusInRange()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            var lineRange = ParseAndGetLineRange("1+1,3");
            Assert.AreEqual(1, lineRange.StartLineNumber);
            Assert.AreEqual(2, lineRange.EndLineNumber);
        }

        [Test]
        public void LineRange_LineNumberWithMinus()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            var lineRange = ParseAndGetLineRange("1-1");
            Assert.AreEqual(_textBuffer.GetLineRange(0), lineRange);
        }

        [Test]
        public void LineRange_LineNumberWithMinusAndNoValue()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            var lineRange = ParseAndGetLineRange("2-");
            Assert.AreEqual(_textBuffer.GetLineRange(0), lineRange);
        }

        [Test]
        public void LineRange_LineNumberWithMinus2()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            var lineRange = ParseAndGetLineRange("5-3");
            Assert.AreEqual(_textBuffer.GetLineRange(1), lineRange);
        }

        [Test]
        public void LineRange_LineNumberWithMinusInRange()
        {
            Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
            var lineRange = ParseAndGetLineRange("1,5-2");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 2), lineRange);
        }

        [Test]
        public void LineRange_LastLine()
        {
            Create("cat", "tree", "dog");
            var lineRange = ParseAndGetLineRange("$");
            Assert.AreEqual(_textBuffer.GetLineRange(2), lineRange);
        }

        [Test]
        public void LineRange_LastLine_OneLineBuffer()
        {
            Create("cat");
            var lineRange = ParseAndGetLineRange("$");
            Assert.AreEqual(_textBuffer.GetLineRange(0), lineRange);
        }

        [Test]
        public void LineRange_CurrentToEnd()
        {
            Create("cat", "tree", "dog");
            var lineRange = ParseAndGetLineRange(".,$");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 2), lineRange);
        }

        [Test]
        public void LineRange_RightSideIncrementsLeft()
        {
            Create("cat", "dog", "bear", "frog", "tree");
            var lineRange = ParseAndGetLineRange(".,+2");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 2), lineRange);
        }

        [Test]
        public void LineRange_LeftSideIncrementsCurrent()
        {
            Create("cat", "dog", "bear", "frog", "tree");
            var lineRange = ParseAndGetLineRange(".,+2");
            Assert.AreEqual(_textBuffer.GetLineRange(0, 2), lineRange);
        }

        /// <summary>
        /// Make sure we can clear out key mappings with the "mapc" command
        /// </summary>
        [Test]
        public void MapKeys_Clear()
        {
            Action<string, KeyRemapMode[]> testMapClear =
                (command, toClearModes) =>
                {
                    foreach (var keyRemapMode in KeyRemapMode.All)
                    {
                        Assert.IsTrue(_keyMap.MapWithNoRemap("a", "b", keyRemapMode));
                    }

                    ParseAndRun(command);

                    foreach (var keyRemapMode in KeyRemapMode.All)
                    {
                        if (toClearModes.Contains(keyRemapMode))
                        {
                            Assert.IsFalse(_keyMap.GetKeyMappingsForMode(keyRemapMode).Any());
                        }
                        else
                        {
                            Assert.IsTrue(_keyMap.GetKeyMappingsForMode(keyRemapMode).Any());
                        }
                    }

                    _keyMap.ClearAll();
                };

            testMapClear("mapc", new [] {KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Command, KeyRemapMode.OperatorPending});
            testMapClear("nmapc", new [] {KeyRemapMode.Normal});
            testMapClear("vmapc", new [] {KeyRemapMode.Visual, KeyRemapMode.Select});
            testMapClear("xmapc", new [] {KeyRemapMode.Visual});
            testMapClear("smapc", new [] {KeyRemapMode.Select});
            testMapClear("omapc", new [] {KeyRemapMode.OperatorPending});
            testMapClear("mapc!", new [] {KeyRemapMode.Insert, KeyRemapMode.Command});
            testMapClear("imapc", new [] {KeyRemapMode.Insert});
            testMapClear("cmapc", new [] {KeyRemapMode.Command});
        }

        /// <summary>
        /// Test the ability to print out the key mappings
        /// </summary>
        [Test]
        public void MapKeys_Print()
        {
            Action<string, string> assertPrintMap =
                (input, output) =>
                {
                    Vim.KeyMap.MapWithNoRemap(input, input, KeyRemapMode.Normal);
                    ParseAndRun("nmap");
                    var expected = String.Format("n    {0} {0}", output);
                    Assert.AreEqual(expected, _statusUtil.LastStatus);
                    Vim.KeyMap.ClearAll();
                };

            Create("");

            assertPrintMap("a", "a");
            assertPrintMap("b", "b");
            assertPrintMap("A", "A");
            assertPrintMap("<S-a>", "A");
            assertPrintMap("<S-A>", "A");
            assertPrintMap("<c-a>", "<C-A>");
            assertPrintMap("<c-S-a>", "<C-A>");
            assertPrintMap("<Esc>", "<Esc>");
            assertPrintMap("<c-[>", "<Esc>");
            assertPrintMap("<c-@>", "<Nul>");
            assertPrintMap("<Tab>", "<Tab>");
            assertPrintMap("<c-i>", "<Tab>");
            assertPrintMap("<c-h>", "<C-H>");
            assertPrintMap("<BS>", "<BS>");
            assertPrintMap("<NL>", "<NL>");
            assertPrintMap("<c-j>", "<NL>");
            assertPrintMap("<c-l>", "<C-L>");
            assertPrintMap("<FF>", "<FF>");
            assertPrintMap("<c-m>", "<CR>");
            assertPrintMap("<CR>", "<CR>");
            assertPrintMap("<Return>", "<CR>");
            assertPrintMap("<Enter>", "<CR>");
        }

        /// <summary>
        /// Ensure the Put command is linewise even if the register is marked as characterwise
        /// </summary>
        [Test]
        public void Put_ShouldBeLinewise()
        {
            Create("foo", "bar");
            _registerMap.GetRegister(RegisterName.Unnamed).UpdateValue("hey", OperationKind.CharacterWise);
            ParseAndRun("put");
            Assert.AreEqual("foo", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("hey", _textBuffer.GetLine(1).GetText());
        }

        /// <summary>
        /// Ensure that when the ! is present that the appropriate option is passed along
        /// </summary>
        [Test]
        public void Put_BangShouldPutTextBefore()
        {
            Create("foo", "bar");
            _registerMap.GetRegister(RegisterName.Unnamed).UpdateValue("hey", OperationKind.CharacterWise);
            ParseAndRun("put!");
            Assert.AreEqual("hey", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("foo", _textBuffer.GetLine(1).GetText());
        }

        /// <summary>
        /// By default the retab command should affect the entire ITextBuffer and not include
        /// space strings
        /// </summary>
        [Test]
        public void Retab_Default()
        {
            Create("   cat", "\tdog");
            _localSettings.ExpandTab = true;
            _localSettings.TabStop = 2;
            ParseAndRun("retab");
            Assert.AreEqual("   cat", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("  dog", _textBuffer.GetLine(1).GetText());
        }

        /// <summary>
        /// The ! operator should force the command to include spaces
        /// </summary>
        [Test]
        public void Retab_WithBang()
        {
            Create("  cat", "  dog");
            _localSettings.ExpandTab = false;
            _localSettings.TabStop = 2;
            ParseAndRun("retab!");
            Assert.AreEqual("\tcat", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("\tdog", _textBuffer.GetLine(1).GetText());
        }

        /// <summary>
        /// Print out the modified settings
        /// </summary>
        [Test]
        public void Set_PrintModifiedSettings()
        {
            Create("");
            _localSettings.ExpandTab = true;
            ParseAndRun("set");
            Assert.AreEqual("expandtab", _statusUtil.LastStatus);
        }

        /// <summary>
        /// Test the assignment of a string value
        /// </summary>
        [Test]
        public void Set_Assign_StringValue_Global()
        {
            Create("");
            ParseAndRun(@"set sections=cat");
            Assert.AreEqual("cat", _globalSettings.Sections);
        }

        /// <summary>
        /// Test the assignment of a local string value
        /// </summary>
        [Test]
        public void Set_Assign_StringValue_Local()
        {
            Create("");
            ParseAndRun(@"set nrformats=alpha");
            Assert.AreEqual("alpha", _localSettings.NumberFormats);
        }

        /// <summary>
        /// Make sure we can use set for a numbered value setting
        /// </summary>
        [Test]
        public void Set_Assign_NumberValue()
        {
            Create("");
            ParseAndRun(@"set ts=42");
            Assert.AreEqual(42, _localSettings.TabStop);
        }

        /// <summary>
        /// Toggle a toggle option off
        /// </summary>
        [Test]
        public void Set_Toggle_Off()
        {
            Create("");
            _localSettings.ExpandTab = true;
            ParseAndRun(@"set noet");
            Assert.IsFalse(_localSettings.ExpandTab);
        }

        /// <summary>
        /// Invert a toggle setting to on
        /// </summary
        [Test]
        public void Set_Toggle_InvertOn()
        {
            Create("");
            _localSettings.ExpandTab = false;
            ParseAndRun(@"set et!");
            Assert.IsTrue(_localSettings.ExpandTab);
        }

        /// <summary>
        /// Invert a toggle setting to off
        /// </summary
        [Test]
        public void Set_Toggle_InvertOff()
        {
            Create("");
            _localSettings.ExpandTab = true;
            ParseAndRun(@"set et!");
            Assert.IsFalse(_localSettings.ExpandTab);
        }

        /// <summary>
        /// When an empty string is provided for the pattern string then the pattern from the last
        /// substitute
        /// </summary>
        [Test]
        public void Substitute_EmptySearchUsesLastSearch()
        {
            Create("cat tree");
            Vim.VimData.LastSubstituteData = FSharpOption.Create(new SubstituteData(
                "cat",
                "rat",
                SubstituteFlags.None));
            ParseAndRun("s//dog/");
            Assert.AreEqual("dog tree", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure that back slashes are properly handled in the replace 
        /// </summary>
        [Test]
        public void Substitute_Backslashes()
        {
            Create("cat");
            ParseAndRun(@"s/a/\\\\");
            Assert.AreEqual(@"c\\t", _textBuffer.GetLine(0).GetText());
        }

        [Test]
        public void TabNext_NoCount()
        {
            Create("");
            ParseAndRun("tabn");
            Assert.AreEqual(Path.Forward, VimHost.GoToNextTabData.Item1);
            Assert.AreEqual(1, VimHost.GoToNextTabData.Item2);
        }

        /// <summary>
        /// :tabn with a count
        /// </summary>
        [Test]
        public void TabNext_WithCount()
        {
            Create("");
            ParseAndRun("tabn 3");
            Assert.AreEqual(Path.Forward, VimHost.GoToNextTabData.Item1);
            Assert.AreEqual(3, VimHost.GoToNextTabData.Item2);
        }

        [Test]
        public void TabPrevious_NoCount()
        {
            Create("");
            ParseAndRun("tabp");
            Assert.AreEqual(Path.Backward, VimHost.GoToNextTabData.Item1);
            Assert.AreEqual(1, VimHost.GoToNextTabData.Item2);
        }

        [Test]
        public void TabPrevious_NoCount_AltName()
        {
            Create("");
            ParseAndRun("tabN");
            Assert.AreEqual(Path.Backward, VimHost.GoToNextTabData.Item1);
            Assert.AreEqual(1, VimHost.GoToNextTabData.Item2);
        }

        /// <summary>
        /// :tabp with a count
        /// </summary>
        [Test]
        public void TabPrevious_WithCount()
        {
            Create("");
            ParseAndRun("tabp 3");
            Assert.AreEqual(Path.Backward, VimHost.GoToNextTabData.Item1);
            Assert.AreEqual(3, VimHost.GoToNextTabData.Item2);
        }
    }
}
