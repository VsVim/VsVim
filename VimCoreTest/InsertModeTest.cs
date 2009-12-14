using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for InputMode
    /// </summary>
    [TestFixture]
    public class InsertModeTest
    {
        private IVimBufferData _data;
        private Vim.Modes.Insert.InsertMode _modeRaw;
        private IMode _mode;
        private ITextBuffer _buffer;
        private IWpfTextView _view;

        public void CreateBuffer(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _buffer = _view.TextBuffer;
            _data = Utils.MockObjectFactory.CreateVimBufferData(_view);
            _modeRaw = new Vim.Modes.Insert.InsertMode(_data);
            _mode = _modeRaw;
        }

        [SetUp]
        public void Init()
        {
            CreateBuffer("foo bar baz", "boy kick ball");
        }

        #region Misc

        [Test, Description("Must process escape")]
        public void CanProcess1()
        {
            Assert.IsTrue(_mode.CanProcess(Key.Escape));
        }

        [Test, Description("Do not processing anything other than Escape")]
        public void CanProcess2()
        {
            Assert.IsFalse(_mode.CanProcess(Key.Enter));
            Assert.IsFalse(_mode.CanProcess(Key.I));
        }

        [Test, Description("Process but and handle Escape, otherwise it will end up as a char in the buffer")]
        public void Process1()
        {
            var res = _mode.Process(Key.Escape);
            Assert.IsTrue(res.IsSwitchMode);
        }

        #endregion

        #region CTRL-D

        [Test]
        public void ShiftLeft1()
        {
            CreateBuffer("    foo");
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Too short of a shift width")]
        public void ShiftLeft2()
        {
            CreateBuffer(" foo");
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Another too short of a shift")]
        public void ShiftLeft3()
        {
            CreateBuffer("foo");
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ShiftLeft4()
        {
            CreateBuffer("foo", "     bar");
            _view.Caret.MoveTo(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            Assert.AreEqual(" bar", _buffer.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
        }

        #endregion

    }
}
