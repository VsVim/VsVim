using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    public class IntegrationTest : VimTestBase
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
            _textView = CreateTextView(lines);
            _buffer = Vim.CreateVimBuffer(_textView);
        }

        public IntegrationTest()
        {
            CreateBuffer(DefaultLines);
        }

        #region Misc

        [Fact]
        public void Sanity()
        {
            Assert.Equal(ModeKind.Normal, _buffer.ModeKind);
        }

        [Fact]
        public void TestChar_h_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 2));
            _buffer.Process('h');
            Assert.Equal(1, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Use a count command to move the cursor
        /// </summary>
        [Fact]
        public void TestChar_h_2()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 2));
            _buffer.Process('2');
            _buffer.Process('h');
            Assert.Equal(0, _textView.Caret.Position.BufferPosition.Position);
        }

        [Fact]
        public void TestChar_l_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _buffer.Process('l');
            Assert.Equal(2, _textView.Caret.Position.BufferPosition.Position);
        }

        [Fact]
        public void TestChar_w_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _buffer.Process('w');
            Assert.Equal(8, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// w with a count
        /// </summary>
        [Fact]
        public void TestChar_w_2()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 1));
            _buffer.Process('2');
            _buffer.Process('w');
            Assert.Equal(20, _textView.Caret.Position.BufferPosition.Position);
        }

        [Fact]
        public void TestChar_i_1()
        {
            _buffer.Process('i');
            Assert.Equal(ModeKind.Insert, _buffer.ModeKind);
        }

        [Fact]
        public void TestChar_yy_1()
        {
            _buffer.Process("yy");
            var tss = _textView.TextSnapshot;
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.Equal(tss.Lines.ElementAt(0).GetTextIncludingLineBreak(), reg.StringValue);
        }

        /// <summary>
        /// Yank mulptiple lines
        /// </summary>
        [Fact]
        public void TestChar_yy_2()
        {
            _buffer.Process("2yy");
            var tss = _textView.TextSnapshot;
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            var text = span.GetText();
            Assert.Equal(text, reg.StringValue);
        }

        /// <summary>
        /// Yank off the end of the buffer
        /// </summary>
        [Fact]
        public void TestChar_yy_3()
        {
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.Lines.Count() - 1);
            _textView.Caret.MoveTo(last.Start);
            _buffer.Process("2yy");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.Equal(last.GetTextIncludingLineBreak() + Environment.NewLine, reg.StringValue);
        }

        /// <summary>
        /// Yank with a word motion
        /// </summary>
        [Fact]
        public void TestChar_yw_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("yw");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.Equal("summary ", reg.StringValue);
        }

        /// <summary>
        /// Yank into a different register
        /// </summary>
        [Fact]
        public void TestChar_yw_2()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("\"cyw");
            var reg = _buffer.GetRegister('c');
            Assert.Equal("summary ", reg.StringValue);
        }

        /// <summary>
        /// Yank with a double word motion
        /// </summary>
        [Fact]
        public void TestChar_y2w_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("y2w");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.Equal("summary description ", reg.StringValue);
        }

        /// <summary>
        /// The order count shouldn't matter
        /// </summary>
        [Fact]
        public void TestChar_2yw_1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("2yw");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.Equal("summary description ", reg.StringValue);
        }

        [Fact]
        public void TestChar_dd_1()
        {
            CreateBuffer(DefaultLines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            var text = _textView.TextSnapshot.GetLineFromLineNumber(0).GetTextIncludingLineBreak();
            _buffer.Process("dd");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.Equal(text, reg.StringValue);
        }

        /// <summary>
        /// Delete a particular word from the file
        /// </summary>
        [Fact]
        public void TestChar_dw_1()
        {
            CreateBuffer(DefaultLines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 8));
            _buffer.Process("dw");
            var reg = _buffer.GetRegister(RegisterName.Unnamed);
            Assert.Equal("description ", reg.StringValue);
        }

        /// <summary>
        /// Delete into a different regisetr
        /// </summary>
        [Fact]
        public void TestChar_dw_2()
        {
            CreateBuffer(DefaultLines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 8));
            _buffer.Process("\"cdw");
            var reg = _buffer.GetRegister('c');
            Assert.Equal("description ", reg.StringValue);
        }

        /// <summary>
        /// Paste text into the buffer
        /// </summary>
        [Fact]
        public void TestChar_p_1()
        {
            CreateBuffer("how is", "it going");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _buffer.Process("p");
            Assert.Equal("hheyow is", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Fact]
        public void TestChar_P_1()
        {
            CreateBuffer("how is", "it going");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _buffer.Process("P");
            Assert.Equal("heyhow is", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Fact]
        public void TestChar_2P_1()
        {
            CreateBuffer("how is", "it going");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            _buffer.Process("2P");
            Assert.Equal("heyheyhow is", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Fact]
        public void TestChar_A_1()
        {
            CreateBuffer("how is", "foo");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("A");
            Assert.Equal(ModeKind.Insert, _buffer.ModeKind);
            Assert.Equal(_textView.TextSnapshot.GetLineFromLineNumber(0).End, _textView.Caret.Position.BufferPosition);
        }

        [Fact]
        public void TestChar_o_1()
        {
            CreateBuffer("how is", "foo");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("o");
            Assert.Equal(ModeKind.Insert, _buffer.ModeKind);
            Assert.Equal(3, _textView.TextSnapshot.Lines.Count());
            var left = _textView.TextSnapshot.GetLineFromLineNumber(1).Start;
            var right = _textView.Caret.Position.BufferPosition;
            Assert.Equal(left, right);
            Assert.Equal(String.Empty, _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        /// <summary>
        /// Use o at end of buffer
        /// </summary>
        [Fact]
        public void TestChar_o_2()
        {
            CreateBuffer("foo", "bar");
            var line = _textView.TextSnapshot.Lines.Last();
            _textView.Caret.MoveTo(line.Start);
            _buffer.Process("o");
        }

        [Fact]
        public void TestChar_x_1()
        {
            CreateBuffer("how is");
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("x");
            Assert.Equal(ModeKind.Normal, _buffer.ModeKind);
            Assert.Equal("ow is", _buffer.TextView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        /// <summary>
        /// Test out the n command 
        /// </summary>
        [Fact]
        public void Next1()
        {
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("/some", enter: true);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("n");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(1);
            Assert.Equal(line.Start, _textView.Caret.Position.BufferPosition);
        }

        /// <summary>
        /// Next should not start from the current cursor position
        /// </summary>
        [Fact]
        public void Next3()
        {
            _buffer.Process("/s", enter: true);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process('n');
            Assert.NotEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Make sure that we provide status when there is no next search
        /// </summary>
        [Fact]
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

        [Fact]
        public void NextWordUnderCursor1()
        {
            CreateBuffer(s_lines2);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _buffer.Process("*");
            var line = _textView.TextSnapshot.GetLineFromLineNumber(3);
            Assert.Equal(line.Start + 4, _textView.Caret.Position.BufferPosition);
            Assert.Equal(0, _textView.Selection.GetSpan().Length);
        }

        #endregion
    }
}
