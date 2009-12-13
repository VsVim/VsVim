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
using Microsoft.FSharp.Collections;
using Moq;

namespace VimCoreTest
{
    [TestFixture]
    public class VimBufferTests
    {
        Mock<IMode> _normalMode;
        Mock<IMode> _insertMode;
        Mock<IVimBufferData> _bufferData;
        VimBuffer _rawBuffer;
        IVimBuffer _buffer;

        [SetUp]
        public void Initialize()
        {
            _normalMode = new Mock<IMode>(MockBehavior.Strict);
            _normalMode.Setup(x => x.OnEnter());
            _insertMode = new Mock<IMode>(MockBehavior.Strict);
            var map = new FSharpMap<ModeKind, IMode>(new List<Tuple<ModeKind, IMode>>())
               .Add(ModeKind.Normal, _normalMode.Object)
               .Add(ModeKind.Insert, _insertMode.Object);
            _bufferData = new Mock<IVimBufferData>();
            _rawBuffer = new VimBuffer(_bufferData.Object, map);
            _buffer = _rawBuffer;
        }

        [Test]
        public void SwitchedMode1()
        {
            var ran = false;
            _normalMode.Setup(x => x.OnLeave());
            _buffer.SwitchedMode += (s, m) => { ran = true; };
            _buffer.SwitchMode(ModeKind.Normal);
            Assert.IsTrue(ran);
        }

        [Test]
        public void KeyInputProcessed1()
        {
            var ki = new KeyInput('f', Key.F);
            _normalMode.Setup(x => x.Process(ki)).Returns(ProcessResult.Processed);
            var ran = false;
            _buffer.KeyInputProcessed += (s, i) => { ran = true; };
            _buffer.ProcessInput(ki);
            Assert.IsTrue(ran);
        }

        [Test, Description("Close should call OnLeave for the active mode")]
        public void Close1()
        {
            var ran = false;
            var caret = new MockBlockCaret();
            _normalMode.Setup(x => x.OnLeave()).Callback(() => { ran = true; });
            _bufferData.Setup(x => x.BlockCaret).Returns(caret);
            _buffer.SwitchMode(ModeKind.Normal);
            _buffer.Close();
            Assert.IsTrue(ran);
        }

        [Test, Description("Close should destroy the block caret")]
        public void Close2()
        {
            var caret = new MockBlockCaret();
            _bufferData.Setup(x => x.BlockCaret).Returns(caret);
            _normalMode.Setup(x => x.OnLeave());
            _buffer.Close();
            Assert.AreEqual(1, caret.DestroyCount);
        }
    }
}
