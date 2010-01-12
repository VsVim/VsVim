using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class RegexSearchReplaceTest
    {
        private ITextBuffer _buffer;
        private RegexSearchReplace _search;

        private void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
            _search = new RegexSearchReplace();
        }

        [Test]
        public void FindNextMatch1()
        {
            Create("bar for");
            var data = new SearchData("for", SearchKind.ForwardWithWrap, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsTrue(found.IsSome());
            Assert.AreEqual(4, found.Value.Start.Position);
        }

        [Test]
        public void FindNextMatch2()
        {
            Create("foo bar");
            var data = new SearchData("won't be there", SearchKind.ForwardWithWrap, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsTrue(found.IsNone());
        }

        [Test]
        public void FindNextMatch3()
        {
            Create("foo bar");
            var data = new SearchData("oo", SearchKind.Backward, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, _buffer.CurrentSnapshot.GetLineFromLineNumber(0).End);
            Assert.IsTrue(found.HasValue());
            Assert.AreEqual(1, found.Value.Start);
            Assert.AreEqual("oo", found.Value.GetText());
        }

        [Test, Description("Search with a bad regex should just produce a bad result")]
        public void FindNextMatch4()
        {
            Create("foo bar(");
            var data = new SearchData("(", SearchKind.Forward, SearchReplaceFlags.None);
            var found = _search.FindNextMatch(data, new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsFalse(found.HasValue());
        }
    }
}
