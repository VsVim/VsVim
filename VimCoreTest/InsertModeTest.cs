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
using Vim.Modes;
using Moq;

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
        private Mock<ICommonOperations> _operations;

        public void CreateBuffer(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _buffer = _view.TextBuffer;
            _data = Utils.MockObjectFactory.CreateVimBufferData(_view);
            _operations = new Mock<ICommonOperations>(MockBehavior.Strict);
            _modeRaw = new Vim.Modes.Insert.InsertMode(Tuple.Create<IVimBufferData,ICommonOperations>(_data,_operations.Object));
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
            _operations
                .Setup(x => x.ShiftLeft(_view.TextSnapshot.GetLineFromLineNumber(0).Extent, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable(); ;
            var res = _mode.Process(new KeyInput(Char.MinValue, Key.D, ModifierKeys.Control));
            Assert.IsTrue(res.IsProcessed);
            _operations.Verify();
        }

        #endregion

    }
}
