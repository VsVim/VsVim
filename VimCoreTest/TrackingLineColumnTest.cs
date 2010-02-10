using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;
using Vim;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    [TestFixture]
    public class TrackingLineColumnTest
    {
        private ITrackingLineColumn Create(
            ITextBuffer buffer,
            int line,
            int column,
            Action<TrackingLineColumn> onClose = null)
        {   
            onClose = onClose ?? ( _ => {} );
            var func = FSharpFuncUtil.Create<TrackingLineColumn,Unit>( item =>
                {
                    onClose(item);
                    return null;
                });
            var tlc = new TrackingLineColumn(buffer, column, func);
            tlc.Line = FSharpOption.Create(buffer.CurrentSnapshot.GetLineFromLineNumber(line));
            return tlc;
        }

        private static void AssertLineColumn(SnapshotPoint point, int lineNumber, int column)
        {
            var line = point.GetContainingLine();
            Assert.AreEqual(lineNumber, line.LineNumber, "Invalid line number");
            Assert.AreEqual(column, point.Position - line.Start.Position, "Invalid column");
        }
            
        [Test]
        public void SimpleEdit1()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar", "baz");
            var tlc = Create(buffer, 0, 1);
            buffer.Replace(new Span(0, 0), "foo");
            var point = tlc.Point;
            Assert.IsTrue(point.IsSome());
            AssertLineColumn(point.Value, 0, 1);
        }


    }
}
