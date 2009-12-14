using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Microsoft.FSharp.Core;
using CommandUtil = Vim.Modes.Command.Util;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;
using Vim.Modes.Common;

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

        [Test]
        public void Put1()
        {
            var view = EditorUtil.CreateView("foo");
            var host = new FakeVimHost();
            CommandUtil.Put(host, view, "bar", view.TextSnapshot.GetLineFromLineNumber(0), false);
            Assert.AreEqual("bar", view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void Put2()
        {
            var view = EditorUtil.CreateView("bar", "baz");
            var host = new FakeVimHost();
            CommandUtil.Put(host, view, " here", view.TextSnapshot.GetLineFromLineNumber(0), true);
            var tss = view.TextSnapshot;
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(" here", tss.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual(tss.GetLineFromLineNumber(1).Start.Add(1).Position, view.Caret.Position.BufferPosition.Position);
        }
    }
}
