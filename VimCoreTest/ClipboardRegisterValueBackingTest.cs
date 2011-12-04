using System;
using Moq;
using NUnit.Framework;
using Vim;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class ClipboardRegisterValueBackingTest
    {
        private Mock<IClipboardDevice> _clipboardDevice;
        private ClipboardRegisterValueBacking _valueBackingRaw;
        private IRegisterValueBacking _valueBacking;

        [SetUp]
        public void SetUp()
        {
            _clipboardDevice = new Mock<IClipboardDevice>(MockBehavior.Strict);
            _valueBackingRaw = new ClipboardRegisterValueBacking(_clipboardDevice.Object);
            _valueBacking = _valueBackingRaw;
        }

        /// <summary>
        /// If the value in the clipboard doesn't end with a new line then it should be a 
        /// character wise value
        /// </summary>
        [Test]
        public void GetValue_Normal()
        {
            _clipboardDevice.SetupGet(x => x.Text).Returns("cat");
            Assert.IsTrue(_valueBacking.RegisterValue.OperationKind.IsCharacterWise);
            Assert.AreEqual("cat", _valueBacking.RegisterValue.StringValue);
        }

        /// <summary>
        /// If the text ends with a newline then it needs to be treated as a linewise 
        /// operation
        /// </summary>
        [Test]
        public void GetValue_EndsWithNewLine()
        {
            _clipboardDevice.SetupGet(x => x.Text).Returns("cat" + Environment.NewLine);
            Assert.IsTrue(_valueBacking.RegisterValue.OperationKind.IsLineWise);
            Assert.AreEqual("cat" + Environment.NewLine, _valueBacking.RegisterValue.StringValue);
        }
    }
}
