using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VimCore;
using Microsoft.FSharp.Core;
using CommandUtil = VimCore.Modes.Command.Util;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;
using VimCore.Modes.Common;

namespace VimCoreTest
{
    [TestFixture]
    public class Command_UtilTest
    {

        private bool Join(ITextView view, SnapshotSpan? range, int? count, bool removeSpaces)
        {
            return CommandUtil.Join(
                view,
                range.HasValue ? FSharpOption<SnapshotSpan>.Some(range.Value) : FSharpOption<SnapshotSpan>.None,
                removeSpaces ? JoinKind.RemoveEmptySpaces : JoinKind.KeepEmptySpaces,
                count.HasValue ? FSharpOption<int>.Some(count.Value) : FSharpOption<int>.None);
        }

        [Test]
        public void Join1()
        {
            var view = EditorUtil.CreateView("foo", "bar");
            Assert.IsTrue(Join(view, null, null, true));
            Assert.AreEqual("foo bar", view.TextSnapshot.GetText());
        }

        [Test]
        public void Join2()
        {
            var view = EditorUtil.CreateView("foo", "bar", "baz", "jazz");
            var tss = view.TextSnapshot;
            Assert.IsTrue(Join(
                view,
                new SnapshotSpan(tss.GetLineFromLineNumber(0).Start, tss.GetLineFromLineNumber(1).End),
                1,
                true));
            tss = view.TextSnapshot;
            Assert.AreEqual("foo", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("bar baz", tss.GetLineFromLineNumber(1).GetText());
        }
    }
}
