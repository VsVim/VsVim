using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VimCoreTest
{
    class IEditorOperationsTest
    {
        // TODO:
        // MoveLineDown 
        //   - Doesn't crash second to last line
        //   - Maintains column

        //[Test, Description("Be wary the 0 length last line")]
        //public void MoveCaretDown5()
        //{
        //    CreateLines("foo", String.Empty);
        //    var tss = _view.TextSnapshot;
        //    var line = tss.GetLineFromLineNumber(0);
        //    _view.Caret.MoveTo(line.Start);
        //    _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
        //    _editorOpts.Setup(x => x.MoveLineDown(false)).Verifiable();
        //    _operations.MoveCaretDown(1);
        //    _editorOpts.Verify();
        //}

        //[Test, Description("Move caret down should maintain column")]
        //public void MoveCaretDown6()
        //{
        //    CreateLines("foo", "bar");
        //    var tss = _view.TextSnapshot;
        //    _view.Caret.MoveTo(tss.GetLineFromLineNumber(0).Start.Add(1));
        //    _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
        //    _operations.MoveCaretDown(1);
        //    Assert.AreEqual(tss.GetLineFromLineNumber(1).Start.Add(1), _view.Caret.Position.BufferPosition);
        //    _editorOpts.Verify();
        //}

        //[Test, Description("Move caret down should maintain column")]
        //public void MoveCaretDown7()
        //{
        //    CreateLines("foo", "bar");
        //    var tss = _view.TextSnapshot;
        //    _view.Caret.MoveTo(tss.GetLineFromLineNumber(0).End);
        //    _editorOpts.Setup(x => x.ResetSelection()).Verifiable();
        //    _operations.MoveCaretDown(1);
        //    Assert.AreEqual(tss.GetLineFromLineNumber(1).End, _view.Caret.Position.BufferPosition);
        //    _editorOpts.Verify();
        //}
    }
}
