using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Normal;
using VimCoreTest.Utils;
using Moq;
using Vim;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest
{
    [TestFixture]
    public class Normal_Operations
    {
        private IWpfTextView _view;
        private Mock<IVimBufferData> _bufferData;
        private Register _reg;

        private NormalModeData Create(int? count, params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _bufferData= new Mock<IVimBufferData>(MockBehavior.Strict);
            _bufferData.Setup(x => x.TextView).Returns(_view);
            _bufferData.Setup(x => x.TextBuffer).Returns(_view.TextBuffer);
            _bufferData.Setup(x => x.TextSnapshot).Returns(() => _view.TextSnapshot);
            _reg = new Register('c');
            var innerFunc = FSharpFuncUtil.Create( (KeyInput ki) => NormalModeResult._unique_Complete);
            var func = FSharpFuncUtil.Create((NormalModeData d) => innerFunc);
            return new NormalModeData(
                _bufferData.Object,
                _reg,
                count ?? 1,
                FSharpOption<IncrementalSearch>.None,
                func,
                false);
        }

        [Test]
        public void DeleteCharacterAtCursor1()
        {
            var data = Create(null, "foo", "bar");
            var res = Operations.DeleteCharacterAtCursor(data);
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", _reg.StringValue);
        }
    }
}
