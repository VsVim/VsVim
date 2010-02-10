using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    [TestFixture]
    public class JumpListTest
    {
        private Mock<ITrackingLineColumnService> _tlcService;
        private JumpList _jumpListRaw;
        private IJumpList _jumpList;

        [SetUp]
        public void SetUp()
        {
            Create();
        }

        private void Create(int limit = 100)
        {
            _tlcService = new Mock<ITrackingLineColumnService>(MockBehavior.Strict);
            _jumpListRaw = new JumpList(_tlcService.Object, limit);
            _jumpList = _jumpListRaw;
        }

        [Test]
        public void AllJumps1()
        {
            Assert.AreEqual(0, _jumpList.AllJumps.Count());
        }

        [Test]
        public void MoveNext1()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var point = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var tlc = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            _tlcService.Setup(x => x.CreateForPoint(point)).Returns(tlc.Object);
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 0));
            Assert.IsFalse(_jumpList.MoveNext());
        }

        [Test]
        public void MoveNext2()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            tlc1.SetupGet(x => x.Point).Returns(FSharpOption.Create(new SnapshotPoint(buffer.CurrentSnapshot, 1)));
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            _jumpList.Add(point2);
            Assert.IsTrue(_jumpList.MoveNext());
            Assert.IsTrue(_jumpList.Current.IsSome());
            Assert.AreEqual(1, _jumpList.Current.Value.Position);
            Assert.IsFalse(_jumpList.MoveNext());
        }

        [Test]
        public void MovePrevious1()
        {
            Assert.IsFalse(_jumpList.MovePrevious()); 
        }

        [Test]
        public void MovePrevious2()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            _tlcService.Setup(x => x.CreateForPoint(new SnapshotPoint(buffer.CurrentSnapshot,0))).Returns(tlc1.Object);
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 0));
            Assert.IsFalse(_jumpList.MovePrevious());
        }

        [Test]
        public void MovePrevious3()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            tlc1.SetupGet(x => x.Point).Returns(FSharpOption.Create(new SnapshotPoint(buffer.CurrentSnapshot, 1)));
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _jumpList.Add(point2);
            _jumpList.Add(point1);
            _jumpList.MoveNext();
            Assert.IsTrue(_jumpList.MovePrevious());
            Assert.IsTrue(_jumpList.Current.IsSome());
            Assert.AreEqual(1, _jumpList.Current.Value.Position);
        }

        [Test, Description("Make sure we call close on ITrackingLineColumn instances which fall off the end of the list")]
        public void Limit1()
        {
            Create(1);
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            tlc1.Setup(x => x.Close()).Verifiable();
            _jumpList.Add(point2);
            tlc1.Verify();
        }

        [Test, Description("Make sure we call close on ITrackingLineColumn instances which fall off the end of the list")]
        public void Limit2()
        {
            Create(1);
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc3 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            var point3 = new SnapshotPoint(buffer.CurrentSnapshot, 2);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _tlcService.Setup(x => x.CreateForPoint(point3)).Returns(tlc3.Object);
            _jumpList.Add(point1);
            tlc1.Setup(x => x.Close()).Verifiable();
            _jumpList.Add(point2);
            tlc2.Setup(x => x.Close()).Verifiable();
            _jumpList.Add(point3);
            tlc1.Verify();
        }


    }
}
