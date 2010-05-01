using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VimCore.Test.Utils;
using Microsoft.VisualStudio.Text;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using VimCore.Test.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class TextViewUtilTest
    {
        [Test]
        public void MoveCaretToVirtualPoint1()
        {
            var buffer = EditorUtil.CreateBuffer("foo","bar");
            var caret = MockObjectFactory.CreateCaret();
            var textView = MockObjectFactory.CreateTextView(buffer:buffer, caret:caret.Object);
            var point = new VirtualSnapshotPoint(buffer.GetLine(0), 2); 

            caret.Setup(x => x.MoveTo(point)).Returns(new CaretPosition()).Verifiable();
            caret.Setup(x => x.EnsureVisible()).Verifiable();
            TextViewUtil.MoveCaretToVirtualPoint(textView.Object, point);
            caret.Verify();
        }

    }
}
