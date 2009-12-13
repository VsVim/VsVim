using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VimCore;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Moq;

namespace VimCoreTest
{
    [TestFixture]
    public class IntegrationTests
    {
        private IVimBuffer m_buffer;
        private IWpfTextView m_view;
        private FakeVimHost m_host;

        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        public void CreateBuffer(params string[] lines)
        {
            m_view = Utils.EditorUtil.CreateView(lines);
            m_host = new FakeVimHost();
            m_buffer = Factory.CreateVimBuffer(m_host, m_view, "test",VimCoreTest.Utils.MockObjectFactory.CreateBlockCaret().Object);
        }

        [SetUp]
        public void Init()
        {
            CreateBuffer(s_lines) ;
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
            m_buffer.ProcessKey(Key.H);
            Assert.AreEqual(1, m_view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Use a count command to move the cursor
        /// </summary>
        [Test]
        public void TestChar_h_2()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 2));
            m_buffer.ProcessKey(Key.D2);
            m_buffer.ProcessKey(Key.H);
            Assert.AreEqual(0, m_view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_l_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 1));
            m_buffer.ProcessKey(Key.L);
            Assert.AreEqual(2, m_view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_w_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 1));
            m_buffer.ProcessKey(Key.W);
            Assert.AreEqual(8, m_view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// w with a count
        /// </summary>
        [Test]
        public void TestChar_w_2()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 1));
            m_buffer.ProcessKey(Key.D2);
            m_buffer.ProcessKey(Key.W);
            Assert.AreEqual(20, m_view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void TestChar_i_1()
        {
            m_buffer.ProcessKey(Key.I);
            Assert.AreEqual(ModeKind.Insert, m_buffer.ModeKind);
        }

        [Test]
        public void TestChar_yy_1()
        {
            m_buffer.ProcessInputAsString("yy");
            var tss = m_view.TextSnapshot;
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
            Assert.AreEqual(tss.Lines.ElementAt(0).GetTextIncludingLineBreak(), reg.StringValue);
        }

        /// <summary>
        /// Yank mulptiple lines
        /// </summary>
        [Test]
        public void TestChar_yy_2()
        {
            m_buffer.ProcessInputAsString("2yy");
            var tss = m_view.TextSnapshot;
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
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
            m_buffer.ProcessInputAsString("2yy");
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
            Assert.AreEqual(last.GetTextIncludingLineBreak(), reg.StringValue);
        }

        /// <summary>
        /// Yank with a word motion
        /// </summary>
        [Test]
        public void TestChar_yw_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("yw");
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
            Assert.AreEqual("summary ", reg.StringValue);
        }

        /// <summary>
        /// Yank into a different regisetr
        /// </summary>
        [Test]
        public void TestChar_yw_2()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("\"cyw");
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
            m_buffer.ProcessInputAsString("y2w");
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
            Assert.AreEqual("summary description ", reg.StringValue);
        }

        /// <summary>
        /// The order count shouldn't matter
        /// </summary>
        [Test]
        public void TestChar_2yw_1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("2yw");
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
            Assert.AreEqual("summary description ", reg.StringValue);
        }

        [Test]
        public void TestChar_dd_1()
        {
            CreateBuffer(s_lines);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            var text = m_view.TextSnapshot.GetLineFromLineNumber(0).GetTextIncludingLineBreak();
            m_buffer.ProcessInputAsString("dd");
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
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
            m_buffer.ProcessInputAsString("dw");
            var reg = m_buffer.GetRegister(RegisterUtil.DefaultName);
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
            m_buffer.ProcessInputAsString("\"cdw");
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
            m_buffer.GetRegister(RegisterUtil.DefaultName).UpdateValue("hey");
            m_buffer.ProcessInputAsString("p");
            Assert.AreEqual("hheyow is", m_view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_P_1()
        {
            CreateBuffer("how is", "it going");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.GetRegister(RegisterUtil.DefaultName).UpdateValue("hey");
            m_buffer.ProcessInputAsString("P");
            Assert.AreEqual("heyhow is", m_view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_2P_1()
        {
            CreateBuffer("how is", "it going");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.GetRegister(RegisterUtil.DefaultName).UpdateValue("hey");
            m_buffer.ProcessInputAsString("2P");
            Assert.AreEqual("heyheyhow is", m_view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_A_1()
        {
            CreateBuffer("how is", "foo");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("A");
            Assert.AreEqual(ModeKind.Insert, m_buffer.ModeKind);
            Assert.AreEqual(m_view.TextSnapshot.GetLineFromLineNumber(0).End, m_view.Caret.Position.BufferPosition);
        }

        [Test]
        public void TestChar_o_1()
        {
            CreateBuffer("how is", "foo");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("o");
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
            m_buffer.ProcessInputAsString("o");
        }

        [Test, Description("Make sure o will indent if the previous line was indented")]
        public void TestChar_o_3()
        {
            CreateBuffer("  foo");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("o");
            var point = m_view.Caret.Position.VirtualBufferPosition;
            Assert.IsTrue(point.IsInVirtualSpace);
            Assert.AreEqual(2, point.VirtualSpaces);
        }
 
        [Test]
        public void TestChar_x_1()
        {
            CreateBuffer("how is");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("x");
            Assert.AreEqual(ModeKind.Normal, m_buffer.ModeKind);
            Assert.AreEqual("ow is", m_buffer.TextView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void TestChar_Search_1()
        {
            CreateBuffer("how is", "foo");
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("/");
            Assert.AreEqual("/", m_host.Status);
            m_buffer.ProcessInputAsString("is");
            Assert.AreEqual("/is", m_host.Status);
            Assert.AreEqual(4, m_view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Search in normal mode
        /// </summary>
        [Test]
        public void NormalMode1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("/des");
            Assert.AreEqual(8, m_view.Caret.Position.BufferPosition.Position);

            var selection = m_view.Selection;
            Assert.AreEqual(8, selection.Start.Position);

            var span = new SnapshotSpan(selection.Start.Position, selection.End.Position);
            Assert.AreEqual(3, span.Length);
        }

        /// <summary>
        /// Search must cross lines down
        /// </summary>
        [Test]
        public void NormalMode2()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("/some");
            var line = m_view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, m_view.Caret.Position.BufferPosition);

            var selection = m_view.Selection;
            Assert.AreEqual(line.Start, selection.Start.Position);
            var span = new SnapshotSpan(selection.Start.Position, selection.End.Position);
            Assert.AreEqual(4, span.Length);
        }

        /// <summary>
        /// Search must wrap
        /// </summary>
        [Test]
        public void NormalMode3()
        {
            m_view.Caret.MoveTo(m_view.TextSnapshot.GetLineFromLineNumber(2).Start);
            m_buffer.ProcessInputAsString("/summary");
            Assert.AreEqual(0, m_view.Caret.Position.BufferPosition);

            var selection = m_view.Selection;
            Assert.AreEqual(0, selection.Start.Position);
            Assert.AreEqual(7, selection.GetSpan().Length);
        }

        /// <summary>
        /// Escape should reset cursor and selection
        /// </summary>
        [Test]
        public void NormalModeEscape1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("/for");
            Assert.IsTrue(m_view.Caret.Position.BufferPosition.Position != 0);
            m_buffer.ProcessKey(Key.Escape);
            Assert.AreEqual(0, m_view.Caret.Position.BufferPosition.Position);
            Assert.AreEqual(0, m_view.Selection.GetSpan().Length);
        }

        /// <summary>
        /// Enter should clear the selection and set the cursor
        /// </summary>
        [Test]
        public void NormalModeEnter1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("/some");
            var line = m_view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, m_view.Caret.Position.BufferPosition);

            m_buffer.ProcessKey(Key.Enter);
            Assert.AreEqual(line.Start, m_view.Caret.Position.BufferPosition);
            Assert.AreEqual(0, m_view.Selection.GetSpan().Length);
        }

        /// <summary>
        /// Test out the n command 
        /// </summary>
        [Test]
        public void Next1()
        {
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("/some");
            m_buffer.ProcessKey(Key.Enter);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessInputAsString("n");
            var line = m_view.TextSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(line.Start, m_view.Caret.Position.BufferPosition);
        }



        /// <summary>
        /// Next should not start from the current cursor position
        /// </summary>
        [Test]
        public void Next3()
        {
            m_buffer.ProcessInputAsString("/s");
            m_buffer.ProcessKey(Key.Enter);
            m_view.Caret.MoveTo(new SnapshotPoint(m_view.TextSnapshot, 0));
            m_buffer.ProcessKey(Key.N);
            Assert.AreNotEqual(0, m_view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Make sure that we provide status when there is no next search
        /// </summary>
        [Test]
        public void Next4()
        {
            m_buffer.ProcessInputAsString("n");
            Assert.IsFalse(String.IsNullOrEmpty(m_host.Status));
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
            m_buffer.ProcessInputAsString("*");
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
            m_buffer.ProcessInputAsString("*");
            Assert.AreEqual(7, m_view.Caret.Position.BufferPosition.Position);
            Assert.IsFalse(string.IsNullOrEmpty(m_host.Status));
        }


        #endregion
    }
}
