using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Summary description for CommandModeTest
    /// </summary>
    [TestFixture]
    public sealed class CommandModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private MockVimHost _vimHost;

        public void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _vimHost = VimHost;
        }

        private void RunCommand(string command)
        {
            _vimBuffer.Process(':');
            _vimBuffer.Process(command, enter: true);
        }

        /// <summary>
        /// Copying a line to a given line should put it at that given line
        /// </summary>
        [Test]
        public void CopyTo_Line()
        {
            Create("cat", "dog", "bear");
            RunCommand("co 1");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual("dog", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Copying a line to a given line should put it at that given line
        /// </summary>
        [Test]
        public void CopyTo_Line2()
        {
            Create("cat", "dog", "bear");
            RunCommand("co 2");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Check the copy command via the 't' synonym
        /// </summary>
        [Test]
        public void CopyTo_ViaSynonym()
        {
            Create("cat", "dog", "bear");
            RunCommand("t 2");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Copying a line to a range should cause it to copy to the first line 
        /// in the range
        /// </summary>
        [Test]
        public void CopyTo_LineRange()
        {
            Create("cat", "dog", "bear");
            RunCommand("co 1,2");
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
            Assert.AreEqual("dog", _textView.GetLine(2).GetText());
        }

        [Test]
        public void SwitchTo()
        {
            Create("");
            _vimBuffer.Process(':');
            Assert.AreEqual(ModeKind.Command, _vimBuffer.ModeKind);
        }

        [Test]
        public void SwitchOut()
        {
            Create("");
            RunCommand("e foo");
            Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        [Test]
        public void SwitchOutFromBackspace()
        {
            Create("");
            _vimBuffer.Process(':');
            _vimBuffer.Process(VimKey.Back);
            Assert.AreEqual(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        [Test]
        public void JumpLine1()
        {
            Create("a", "b", "c", "d");
            RunCommand("0");
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
            RunCommand("1");
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Non-first line
        /// </summary>
        [Test]
        public void JumpLine2()
        {
            Create("a", "b", "c", "d");
            RunCommand("2");
            Assert.AreEqual(_textView.TextSnapshot.GetLineFromLineNumber(1).Start, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        public void JumpLineLastWithNoWhiteSpace()
        {
            Create("dog", "cat", "tree");
            RunCommand("$");
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.Start, _textView.GetCaretPoint());
        }

        [Test]
        public void JumpLineLastWithWhiteSpace()
        {
            Create("dog", "cat", "  tree");
            RunCommand("$");
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.Start.Add(2), _textView.GetCaretPoint());
        }

        [Test]
        [Description("Suppress errors shouldn't print anything")]
        public void Substitute1()
        {
            Create("cat", "dog");
            var sawError = false;
            _vimBuffer.ErrorMessage += delegate { sawError = true; };
            RunCommand("s/z/o/e");
            Assert.IsFalse(sawError);
        }

        [Test]
        [Description("Simple search and replace")]
        public void Substitute2()
        {
            Create("cat bat", "dag");
            RunCommand("s/a/o/g 2");
            Assert.AreEqual("cot bot", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Repeat of the last search with a new flag")]
        public void Substitute3()
        {
            Create("cat bat", "dag");
            _vimBuffer.VimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "o", SubstituteFlags.None));
            RunCommand("s g 2");
            Assert.AreEqual("cot bot", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Testing the print option")]
        public void Substitute4()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _vimBuffer.StatusMessage += (_, e) => { message = e; };
            RunCommand("s/a/b/p");
            Assert.AreEqual("cbt bat", message);
        }

        [Test]
        [Description("Testing the print option")]
        public void Substitute5()
        {
            Create("cat bat", "dag");
            List<string> list = null;
            _vimBuffer.StatusMessageLong += (_, e) => { list = e.ToList(); };
            RunCommand("s/a/b/pg");
            Assert.AreEqual(Resources.Common_SubstituteComplete(2, 1), list[0]);
            Assert.AreEqual("cbt bbt", list[1]);
        }

        [Test]
        [Description("Testing the print number option")]
        public void Substitute6()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _vimBuffer.StatusMessage += (_, e) => { message = e; };
            RunCommand("s/a/b/#");
            Assert.AreEqual("  1 cbt bat", message);
        }

        [Test]
        [Description("Testing the print list option")]
        public void Substitute7()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _vimBuffer.StatusMessage += (_, e) => { message = e; };
            RunCommand("s/a/b/l");
            Assert.AreEqual("cbt bat$", message);
        }

        /// <summary>
        /// Verify we handle escaped back slashes correctly
        /// </summary>
        [Test]
        public void Substitute_WithBackslashes()
        {
            Create(@"\\\\abc\\\\def");
            RunCommand(@"s/\\\{4\}/\\\\/g");
            Assert.AreEqual(@"\\abc\\def", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Convert a set of spaces into tabs with the '\t' replacement
        /// </summary>
        [Test]
        public void Substitute_TabsForSpaces()
        {
            Create("    ");
            RunCommand(@"s/  /\t");
            Assert.AreEqual("\t  ", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Convert spaces into new lines with the '\r' replacement
        /// </summary>
        [Test]
        public void Substitute_SpacesToNewLine()
        {
            Create("dog chases cat");
            RunCommand(@"s/ /\r/g");
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("chases", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
        }

        /// <summary>
        /// Using the search forward feature which hits a match.  Search should start after the range
        /// so the first match will be after it 
        /// </summary>
        [Test]
        public void Search_ForwardWithMatch()
        {
            Create("cat", "dog", "cat", "fish");
            RunCommand("1,2/cat");
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Using the search forward feature which doesn't hit a match in the specified path.  Should 
        /// raise a warning
        /// </summary>
        [Test]
        public void Search_ForwardWithNoMatchInPath()
        {
            Create("cat", "dog", "cat", "fish");
            var didHit = false;
            _vimBuffer.LocalSettings.GlobalSettings.WrapScan = false;
            _vimBuffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_SearchHitBottomWithout("cat"), message);
                    didHit = true;
                };
            RunCommand("1,3/cat");
            Assert.IsTrue(didHit);
        }

        /// <summary>
        /// No match in the buffer should raise a different message
        /// </summary>
        [Test]
        public void Search_ForwardWithNoMatchInBuffer()
        {
            Create("cat", "dog", "cat", "fish");
            var didHit = false;
            _vimBuffer.ErrorMessage +=
                (sender, message) =>
                {
                    Assert.AreEqual(Resources.Common_PatternNotFound("pig"), message);
                    didHit = true;
                };
            RunCommand("1,2/pig");
            Assert.IsTrue(didHit);
        }
    }
}
