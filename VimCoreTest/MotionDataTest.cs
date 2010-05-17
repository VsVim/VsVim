using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VimCore.Test.Utils;
using Vim;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;

namespace VimCore.Test
{
    [TestFixture]
    public class MotionDataTest
    {
        [Test]
        public void OperationSpan1()
        {
            var buffer = EditorUtil.CreateBuffer("foo","  bar");
            var data = new MotionData(
                new SnapshotSpan(buffer.GetPoint(0), buffer.GetLine(1).Start.Add(2)),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise,
                FSharpOption<int>.None);
            Assert.AreEqual("foo", data.OperationSpan.GetText());
        }
    }
}
