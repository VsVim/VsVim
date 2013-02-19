using System;
using Moq;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class ClipboardRegisterValueBackingTest
    {
        private readonly Mock<IClipboardDevice> _clipboardDevice;
        private readonly ClipboardRegisterValueBacking _valueBackingRaw;
        private readonly IRegisterValueBacking _valueBacking;

        public ClipboardRegisterValueBackingTest()
        {
            _clipboardDevice = new Mock<IClipboardDevice>(MockBehavior.Strict);
            _valueBackingRaw = new ClipboardRegisterValueBacking(_clipboardDevice.Object);
            _valueBacking = _valueBackingRaw;
        }

        /// <summary>
        /// If the value in the clipboard doesn't end with a new line then it should be a 
        /// character wise value
        /// </summary>
        [Fact]
        public void GetValue_Normal()
        {
            _clipboardDevice.SetupGet(x => x.Text).Returns("cat");
            Assert.True(_valueBacking.RegisterValue.OperationKind.IsCharacterWise);
            Assert.Equal("cat", _valueBacking.RegisterValue.StringValue);
        }

        /// <summary>
        /// If the text ends with a newline then it needs to be treated as a linewise 
        /// operation
        /// </summary>
        [Fact]
        public void GetValue_EndsWithNewLine()
        {
            _clipboardDevice.SetupGet(x => x.Text).Returns("cat" + Environment.NewLine);
            Assert.True(_valueBacking.RegisterValue.OperationKind.IsLineWise);
            Assert.Equal("cat" + Environment.NewLine, _valueBacking.RegisterValue.StringValue);
        }
    }
}
