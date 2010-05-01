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
        public void ColumnOrFirstPoint1()
        {
            var buffer = EditorUtil.CreateBuffer("foo","bar");
            var data = new MotionData(
                buffer.GetLine(0).Extent,
                true,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                FSharpOption<int>.None);
            Assert.AreEqual(0, data.ColumnOrFirstPoint.Position);
        }

        [Test]
        public void ColumnOrFirstPoint2()
        {
            var buffer = EditorUtil.CreateBuffer("foo","bar");
            var data = new MotionData(
                buffer.GetLine(0).Extent,
                false,
                MotionKind.Inclusive,
                OperationKind.LineWise,
                FSharpOption<int>.None);
            Assert.AreEqual(buffer.GetLine(0).End, data.ColumnOrFirstPoint);
        }
    }
}
