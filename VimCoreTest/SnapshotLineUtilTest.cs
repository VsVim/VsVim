using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    [TestFixture]
    public class SnapshotLineUtilTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        ITextBuffer _buffer = null;
        ITextSnapshot _snapshot = null;

        public void Create(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
            _snapshot = null;
        }

    }
}
