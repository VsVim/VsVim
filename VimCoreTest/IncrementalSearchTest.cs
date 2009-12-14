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
    /// <summary>
    /// Summary description for IncrementalSearchTest
    /// </summary>
    [TestFixture]
    public class IncrementalSearchTest
    {
        private ITextBuffer _buffer;

        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up",
                "for instance,",
                "where will things to make"
            };

        public void CreateBuffer(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
        }

        [SetUp]
        public void Init()
        {
            CreateBuffer(s_lines);
        }

        [Test]
        public void Search1()
        {
            var search = new IncrementalSearch("for");
            var found = search.FindNextMatch(new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsFalse(found.IsNone());
            Assert.AreEqual(20, found.Value.Start);
        }

        [Test]
        public void Search2()
        {
            var search = new IncrementalSearch("won'tbethere");
            var found = search.FindNextMatch(new SnapshotPoint(_buffer.CurrentSnapshot, 0));
            Assert.IsTrue(found.IsNone());
        }

        [Test]
        public void Search3()
        {
            var search = new IncrementalSearch("or", SearchKind.BackwardWithWrap);
            var found = search.FindNextMatch(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End);
            Assert.IsTrue(found.HasValue());
            Assert.AreEqual(21, found.Value.Start);
        }

        [Test, Description("Search with a bad regex should just produce a bad result")]
        public void Search4()
        {
            var search = new IncrementalSearch("(");
            var found = search.FindNextMatch(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End);
            Assert.IsFalse(found.HasValue());
        }

        [Test]
        public void Previous1()
        {
            CreateBuffer("bar bar");
            var search = new IncrementalSearch("bar", SearchKind.Backward);
            var found = search.FindNextMatch(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).End);
            Assert.IsTrue(found.HasValue());
            Assert.AreEqual(4, found.Value.Start.Position);
        }

        [Test, Description("In the middle of the word should match the word in a previous searh")]
        public void Previous2()
        {
            CreateBuffer("bar bar");
            var search = new IncrementalSearch("bar", SearchKind.Backward);
            var point = _buffer.CurrentSnapshot.GetLineFromLineNumber(0).End;
            var found = search.FindNextMatch(point.Subtract(1));
            Assert.IsTrue(found.HasValue());
            Assert.AreEqual(4, found.Value.Start.Position);
        }

        [Test, Description("Bad regex should not cause a crash")]
        public void Constructor1()
        {
            var search = new IncrementalSearch("(");
            Assert.IsNotNull(search);
        }

    }
}
