using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCore;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for InputMode
    /// </summary>
    [TestClass]
    public class InsertModeTest
    {
        private IVimBufferData _data;
        private VimCore.Modes.Insert.InsertMode _modeRaw;
        private IMode _mode;
        private ITextBuffer _buffer;
        private IWpfTextView _view;

        public void CreateBuffer(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _buffer = _view.TextBuffer;
            _data = Utils.MockFactory.CreateVimBufferData(_view);
            _modeRaw = new VimCore.Modes.Insert.InsertMode(_data);
            _mode = _modeRaw;
        }

        [TestInitialize]
        public void Init()
        {
            CreateBuffer("foo bar baz", "boy kick ball");
        }

        #region Misc

        [TestMethod, Description("Must process escape")]
        public void CanProcess1()
        {
            Assert.IsTrue(_mode.CanProcess(Key.Escape));
        }

        [TestMethod, Description("Do not processing anything other than Escape")]
        public void CanProcess2()
        {
            Assert.IsFalse(_mode.CanProcess(Key.Enter));
            Assert.IsFalse(_mode.CanProcess(Key.I));
        }

        [TestMethod, Description("Process but and handle Escape, otherwise it will end up as a char in the buffer")]
        public void Process1()
        {
            var res = _mode.Process(Key.Escape);
            Assert.IsTrue(res.IsSwitchMode);
        }

        #endregion

        #region CTRL-D

        [TestMethod]
        public void ShiftLeft1()
        {
            CreateBuffer("    foo");
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod, Description("Too short of a shift width")]
        public void ShiftLeft2()
        {
            CreateBuffer(" foo");
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod, Description("Another too short of a shift")]
        public void ShiftLeft3()
        {
            CreateBuffer("foo");
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            Assert.AreEqual("foo", _buffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [TestMethod]
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
