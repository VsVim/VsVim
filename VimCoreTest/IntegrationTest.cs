using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class IntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        static readonly string[] DefaultLines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        private void CreateBuffer(params string[] lines)
        {
            var tuple = EditorUtil.CreateTextViewAndEditorOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.Vim.CreateBuffer(_textView);
        }

        [SetUp]
        public void Init()
        {
            CreateBuffer(DefaultLines);
        }

        #region Misc

        [Test]
        public void Sanity()
        {
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        [Test]
        public void TestChar_h_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 2));
            _buffer.Process('h');
            Assert.AreEqual(1, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Use a count command to move the cursor
        /// </summary>
        [Test]
        public void TestChar_h_2()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 2));
            _buffer.Process('2');
            _buffer.Process('h');
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_l_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _buffer.Process('l');
            Assert.AreEqual(2, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_w_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _buffer.Process('w');
            Assert.AreEqual(8, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// w with a count
        /// </summary>
        [Test]
        public void TestChar_w_2()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _buffer.Process('2');
            _buffer.Process('w');
            Assert.AreEqual(20, _textView.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_i_1()
        {
            _buffer.Process('i');
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

        [Test]
        public void TestChar_yy_1()
        {
            _buffer.Process("yy");
            var tss = _textView.TextSnapshot;
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual(tss.Lines.ElementAt(0).GetTextIncludingLineBreak(), reg.StringValue);
        }

        /// <summary>
        /// Yank mulptiple lines
        /// </summary>
        [Test]
        public void TestChar_yy_2()
        {
            _buffer.Process("2yy");
            var tss = _textView.TextSnapshot;
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            var text = span.GetText();
            Assert.AreEqual(text, reg.StringValue);
        }

        /// <summary>
        /// Yank off the end of the buffer
        /// </summary>
        [Test]
        public void TestChar_yy_3()
        {
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.Lines.Count() - 1);
            _textView.Caret.MoveTo(last.Start);
            _buffer.Process("2yy");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual(last.GetTextIncludingLineBreak(), reg.StringValue);
        }

        /// <summary>
        /// Yank with a word motion
        /// </summary>
        [Test]
        public void TestChar_yw_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("yw");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("summary ", reg.StringValue);
        }

        /// <summary>
        /// Yank into a different regisetr
        /// </summary>
        [Test]
        public void TestChar_yw_2()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("\"cyw");
            var reg = _buffer.GetRegister('c');
            Assert.AreEqual("summary ", reg.StringValue);
        }

        /// <summary>
        /// Yank with a double word motion
        /// </summary>
        [Test]
        public void TestChar_y2w_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("y2w");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("summary description ", reg.StringValue);
        }

        /// <summary>
        /// The order count shouldn't matter
        /// </summary>
        [Test]
        public void TestChar_2yw_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("2yw");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("summary description ", reg.StringValue);
        }

        [Test]
        public void TestChar_dd_1()
        {
            CreateBuffer(DefaultLines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            var text = _textView.TextSnapshot.GetLineFromLineNumber(0).GetTextIncludingLineBreak();
            _buffer.Process("dd");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual(text, reg.StringValue);
        }

        /// <summary>
        /// Delete a particular word from the file
        /// </summary>
        [Test]
        public void TestChar_dw_1()
        {
            CreateBuffer(DefaultLines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 8));
            _buffer.Process("dw");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("description ", reg.StringValue);
        }

        /// <summary>
        /// Delete into a different regisetr
        /// </summary>
        [Test]
        public void TestChar_dw_2()
        {
            CreateBuffer(DefaultLines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 8));
            _buffer.Process("\"cdw");
            var reg = _buffer.GetRegister('c');
            Assert.AreEqual("description ", reg.StringValue);
        }

        /// <summary>
        /// Paste text into the buffer
        /// </summary>
        [Test]
        public void TestChar_p_1()
        {
            CreateBuffer("how is", "it going");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _buffer.Process("p");
            Assert.AreEqual("hheyow is", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_P_1()
        {
            CreateBuffer("how is", "it going");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _buffer.Process("P");
            Assert.AreEqual("heyhow is", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_2P_1()
        {
            CreateBuffer("how is", "it going");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _buffer.Process("2P");
            Assert.AreEqual("heyheyhow is", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_A_1()
        {
            CreateBuffer("how is", "foo");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("A");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(_textView.TextSnapshot.GetLineFromLineNumber(0).End, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        public void TestChar_o_1()
        {
            CreateBuffer("how is", "foo");
            _buffer.LocalSettings.GlobalSettings.UseEditorIndent = false;
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("o");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual(3, _textView.TextSnapshot.Lines.Count());
            var left = _textView.TextSnapshot.GetLineFromLineNumber(1).Start;
            var right = _textView.Caret.Position.BufferPosition;
            Assert.AreEqual(left, right);
            Assert.AreEqual(String.Empty, _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Use o at end of buffer")]
        public void TestChar_o_2()
        {
            CreateBuffer("foo", "bar");
            var line = _textView.TextSnapshot.Lines.Last();
            _textView.Caret.MoveTo(line.Start);
            _buffer.Process("o");
        }

        [Test]
        public void TestChar_x_1()
        {
            CreateBuffer("how is");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("x");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            Assert.AreEqual("ow is", _buffer.TextView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        /// <summary>
        /// Test out the n command 
        /// </summary>
        [Test]
        public void Next1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("/some", enter: true);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("n");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, _textView.Caret.Position.BufferPosition);
        }

        /// <summary>
        /// Next should not start from the current cursor position
        /// </summary>
        [Test]
        public void Next3()
        {
            _buffer.Process("/s", enter: true);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process('n');
            Assert.AreNotEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Make sure that we provide status when there is no next search
        /// </summary>
        [Test]
        public void Next4()
        {
            _buffer.Process("n");
        }

        #endregion

        #region # and *

        static string[] s_lines2 = new string[]
            {
                "summary description for this line",
                "some other line",
                "for what is this",
                "for summary other"
            };

        [Test]
        public void NextWordUnderCursor1()
        {
            CreateBuffer(s_lines2);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("*");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(3);
            Assert.AreEqual(line.Start + 4, _textView.Caret.Position.BufferPosition);
            Assert.AreEqual(0, _textView.Selection.GetSpan().Length);
        }

        #endregion
    }
}
