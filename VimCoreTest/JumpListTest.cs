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
            var tlc = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            _tlcService.Setup(x => x.Create(buffer, 0, 0)).Returns(tlc.Object);
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 0));
            Assert.IsFalse(_jumpList.MoveNext());
        }

        [Test]
        public void MoveNext2()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            tlc1.SetupGet(x => x.Point).Returns(FSharpOption.Create(new SnapshotPoint(buffer.CurrentSnapshot, 1)));
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            _tlcService.Setup(x => x.Create(buffer, 0, 0)).Returns(tlc1.Object);
            _tlcService.Setup(x => x.Create(buffer, 0, 1)).Returns(tlc2.Object);
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 0));
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 1));
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
            _tlcService.Setup(x => x.Create(buffer, 0, 0)).Returns(tlc1.Object);
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 0));
            Assert.IsFalse(_jumpList.MovePrevious());
        }

        [Test]
        public void MovePrevious3()
        {
            var buffer = EditorUtil.CreateBuffer("foo bar");
            var tlc1 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            tlc1.SetupGet(x => x.Point).Returns(FSharpOption.Create(new SnapshotPoint(buffer.CurrentSnapshot, 1)));
            var tlc2 = new Mock<ITrackingLineColumn>(MockBehavior.Strict);
            _tlcService.Setup(x => x.Create(buffer, 0, 1)).Returns(tlc2.Object);
            _tlcService.Setup(x => x.Create(buffer, 0, 0)).Returns(tlc1.Object);
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 1));
            _jumpList.Add(new SnapshotPoint(buffer.CurrentSnapshot, 0));
            _jumpList.MoveNext();
            Assert.IsTrue(_jumpList.MovePrevious());
            Assert.IsTrue(_jumpList.Current.IsSome());
            Assert.AreEqual(1, _jumpList.Current.Value.Position);
        }

    }
}
