using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class MotionDataTest
    {
        [Test]
        public void OperationSpan1()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "  bar");
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(buffer.GetPoint(0), buffer.GetLine(1).Start.Add(2)),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise);
            Assert.AreEqual("foo", data.OperationSpan.GetText());
        }

        [Test]
        public void OperationSpan2()
        {
            var buffer = EditorUtil.CreateBuffer("foo", "  bar");
            var data = VimUtil.CreateMotionData(
                new SnapshotSpan(buffer.GetLine(1).Start.Add(1), 1),
                true,
                MotionKind.Exclusive,
                OperationKind.CharacterWise);
            Assert.AreEqual(new SnapshotSpan(buffer.GetLine(1).Start.Add(1), 1), data.OperationSpan);
        }
    }
}
