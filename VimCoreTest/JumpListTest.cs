using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Vim.UnitTest;
using Microsoft.VisualStudio.Text;
using Vim.Extensions;

namespace VimCore.UnitTest
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
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            _jumpList.Add(point2);
            Assert.IsFalse(_jumpList.MoveNext());
        }

        [Test]
        public void MoveNext3()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            _jumpList.Add(point2);
            _jumpList.MovePrevious();
            Assert.IsTrue(_jumpList.MoveNext());
            Assert.IsTrue(_jumpList.Current.IsNone());
        }

        [Test]
        public void MoveNext4()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            _jumpList.Add(point2);
            _jumpList.MovePrevious();
            _jumpList.MovePrevious();
            Assert.IsTrue(_jumpList.MoveNext());
            tlc2.SetupGet(x => x.Point).Returns(FSharpOption.Create(point2));
            Assert.IsTrue(_jumpList.Current.IsSome());
            Assert.AreEqual(point2, _jumpList.Current.Value);
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
            Assert.IsTrue(_jumpList.MovePrevious());
        }

        [Test]
        public void MovePrevious3()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _jumpList.Add(point1);
            Assert.IsTrue(_jumpList.MovePrevious());
        }

        [Test]
        public void MovePrevious4()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _jumpList.Add(point1);
            Assert.IsTrue(_jumpList.MovePrevious());
            Assert.IsFalse(_jumpList.MovePrevious());
        }

        [Test]
        public void MovePrevious5()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            _jumpList.Add(point2);
            Assert.IsTrue(_jumpList.MovePrevious());
            Assert.IsTrue(_jumpList.MovePrevious());
            tlc1.SetupGet(x => x.Point).Returns(FSharpOption.Create(point1));
            Assert.IsTrue(_jumpList.Current.IsSome());
            Assert.AreEqual(point1, _jumpList.Current.Value);
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

        [Test]
        public void Current1()
        {
            Assert.IsTrue(_jumpList.Current.IsNone());
        }

        [Test, Description("Current is None until we actually move")]
        public void Current2()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _jumpList.Add(point1);
            Assert.IsTrue(_jumpList.Current.IsNone());
        }

        [Test]
        public void Current3()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _jumpList.Add(point1);
            Assert.IsTrue(_jumpList.MovePrevious());
            tlc1.SetupGet(x => x.Point).Returns(FSharpOption.Create(point1)).Verifiable();
            Assert.IsTrue(_jumpList.Current.IsSome());
            Assert.AreEqual(point1, _jumpList.Current.Value);
            tlc1.Verify();
        }

        [Test, Description("Add should reset Current to empty")]
        public void Current4()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            _jumpList.MovePrevious();
            _jumpList.Add(point2);
            Assert.IsTrue(_jumpList.Current.IsNone());
        }

        [Test]
        public void Current5()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            var point1 = new SnapshotPoint(buffer.CurrentSnapshot, 0);
            var point2 = new SnapshotPoint(buffer.CurrentSnapshot, 1);
            _tlcService.Setup(x => x.CreateForPoint(point1)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.CreateForPoint(point2)).Returns(tlc2.Object);
            _jumpList.Add(point1);
            _jumpList.MovePrevious();
            _jumpList.Add(point2);
            Assert.IsTrue(_jumpList.MovePrevious());
            tlc2.SetupGet(x => x.Point).Returns(FSharpOption.Create(point2));
            Assert.IsTrue(_jumpList.Current.IsSome());
            Assert.AreEqual(point2, _jumpList.Current.Value);
        }
    }
}
