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
    public class IntegrationTests
    {
        private IVimBuffer m_buffer;
        private IWpfTextView m_view;

        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        public void CreateBuffer(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            m_view = tuple.Item1;
            var service = EditorUtil.FactoryService;
            m_buffer = service.vim.CreateBuffer(m_view);
        }

        [SetUp]
        public void Init()
        {
            CreateBuffer(s_lines);
        }

        #region Misc

        [Test]
        public void Sanity()
        {
            Assert.AreEqual(ModeKind.Normal, m_buffer.ModeKind);
        }

        [Test]
        public void TestChar_h_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 2));
            m_buffer.Process('h');
            Assert.AreEqual(1, m_view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Use a count command to move the cursor
        /// </summary>
        [Test]
        public void TestChar_h_2()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 2));
            m_buffer.Process('2');
            m_buffer.Process('h');
            Assert.AreEqual(0, m_view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_l_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 1));
            m_buffer.Process('l');
            Assert.AreEqual(2, m_view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_w_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 1));
            m_buffer.Process('w');
            Assert.AreEqual(8, m_view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// w with a count
        /// </summary>
        [Test]
        public void TestChar_w_2()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 1));
            m_buffer.Process('2');
            m_buffer.Process('w');
            Assert.AreEqual(20, m_view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_i_1()
        {
            m_buffer.Process('i');
            Assert.AreEqual(ModeKind.Insert, m_buffer.ModeKind);
        }

        [Test]
        public void TestChar_yy_1()
        {
            m_buffer.Process("yy");
            var tss = m_view.TextSnapshot;
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual(tss.Lines.ElementAt(0).GetTextIncludingLineBreak(), reg.StringValue);
        }

        /// <summary>
        /// Yank mulptiple lines
        /// </summary>
        [Test]
        public void TestChar_yy_2()
        {
            m_buffer.Process("2yy");
            var tss = m_view.TextSnapshot;
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
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
            var tss = m_view.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.Lines.Count() - 1);
            m_view.Caret.MoveTo(last.Start);
            m_buffer.Process("2yy");
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual(last.GetTextIncludingLineBreak(), reg.StringValue);
        }

        /// <summary>
        /// Yank with a word motion
        /// </summary>
        [Test]
        public void TestChar_yw_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("yw");
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("summary ", reg.StringValue);
        }

        /// <summary>
        /// Yank into a different regisetr
        /// </summary>
        [Test]
        public void TestChar_yw_2()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("\"cyw");
            var reg = m_buffer.GetRegister('c');
            Assert.AreEqual("summary ", reg.StringValue);
        }

        /// <summary>
        /// Yank with a double word motion
        /// </summary>
        [Test]
        public void TestChar_y2w_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("y2w");
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("summary description ", reg.StringValue);
        }

        /// <summary>
        /// The order count shouldn't matter
        /// </summary>
        [Test]
        public void TestChar_2yw_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("2yw");
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("summary description ", reg.StringValue);
        }

        [Test]
        public void TestChar_dd_1()
        {
            CreateBuffer(s_lines);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            var text = m_view.TextSnapshot.GetLineFromLineNumber(0).GetTextIncludingLineBreak();
            m_buffer.Process("dd");
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual(text, reg.StringValue);
        }

        /// <summary>
        /// Delete a particular word from the file
        /// </summary>
        [Test]
        public void TestChar_dw_1()
        {
            CreateBuffer(s_lines);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 8));
            m_buffer.Process("dw");
            var reg = m_buffer.GetRegister(RegisterName.Unnamed);
            Assert.AreEqual("description ", reg.StringValue);
        }

        /// <summary>
        /// Delete into a different regisetr
        /// </summary>
        [Test]
        public void TestChar_dw_2()
        {
            CreateBuffer(s_lines);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 8));
            m_buffer.Process("\"cdw");
            var reg = m_buffer.GetRegister('c');
            Assert.AreEqual("description ", reg.StringValue);
        }

        /// <summary>
        /// Paste text into the buffer
        /// </summary>
        [Test]
        public void TestChar_p_1()
        {
            CreateBuffer("how is", "it going");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            m_buffer.Process("p");
            Assert.AreEqual("hheyow is", m_view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_P_1()
        {
            CreateBuffer("how is", "it going");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            m_buffer.Process("P");
            Assert.AreEqual("heyhow is", m_view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_2P_1()
        {
            CreateBuffer("how is", "it going");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.GetRegister(RegisterName.Unnamed).UpdateValue("hey");
            m_buffer.Process("2P");
            Assert.AreEqual("heyheyhow is", m_view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_A_1()
        {
            CreateBuffer("how is", "foo");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("A");
            Assert.AreEqual(ModeKind.Insert, m_buffer.ModeKind);
            Assert.AreEqual(m_view.TextSnapshot.GetLineFromLineNumber(0).End, m_view.Caret.Position.BufferPosition);
        }

        [Test]
        public void TestChar_o_1()
        {
            CreateBuffer("how is", "foo");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("o");
            Assert.AreEqual(ModeKind.Insert, m_buffer.ModeKind);
            Assert.AreEqual(3, m_view.TextSnapshot.Lines.Count());
            var left = m_view.TextSnapshot.GetLineFromLineNumber(1).Start;
            var right = m_view.Caret.Position.BufferPosition;
            Assert.AreEqual(left, right);
            Assert.AreEqual(String.Empty, m_view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Use o at end of buffer")]
        public void TestChar_o_2()
        {
            CreateBuffer("foo", "bar");
            var line = m_view.TextSnapshot.Lines.Last();
            m_view.Caret.MoveTo(line.Start);
            m_buffer.Process("o");
        }

        [Test]
        public void TestChar_x_1()
        {
            CreateBuffer("how is");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("x");
            Assert.AreEqual(ModeKind.Normal, m_buffer.ModeKind);
            Assert.AreEqual("ow is", m_buffer.TextView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        /// <summary>
        /// Test out the n command 
        /// </summary>
        [Test]
        public void Next1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("/some", enter: true);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("n");
            var line = m_view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, m_view.Caret.Position.BufferPosition);
        }

        /// <summary>
        /// Next should not start from the current cursor position
        /// </summary>
        [Test]
        public void Next3()
        {
            m_buffer.Process("/s", enter: true);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process('n');
            Assert.AreNotEqual(0, m_view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Make sure that we provide status when there is no next search
        /// </summary>
        [Test]
        public void Next4()
        {
            m_buffer.Process("n");
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
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.Process("*");
            var line = m_view.TextSnapshot.GetLineFromLineNumber(3);
            Assert.AreEqual(line.Start + 4, m_view.Caret.Position.BufferPosition);
            Assert.AreEqual(0, m_view.Selection.GetSpan().Length);
        }

        /// <summary>
        /// Move to a non-word
        /// </summary>
        [Test]
        public void NextWordUnderCursor2()
        {
            CreateBuffer(s_lines2);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 7));
            m_buffer.Process("*");
            Assert.AreEqual(7, m_view.Caret.Position.BufferPosition.Position);
        }


        #endregion
    }
}
