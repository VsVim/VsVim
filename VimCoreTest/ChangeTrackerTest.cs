using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using VimCore.Test.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class ChangeTrackerTest
    {
        private MockFactory _factory;
        private ChangeTracker _trackerRaw;
        private IChangeTracker _tracker;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<ITextChangeTrackerFactory> _textChangeTrackerFactory;
        private MockVimBuffer _buffer;

        private void CreateForText(params string[] lines)
        {
            _textBuffer = Utils.EditorUtil.CreateBuffer(lines);
            _textView = Mock.MockObjectFactory.CreateTextView(_textBuffer);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);

            _buffer = new MockVimBuffer();
            _buffer.TextViewImpl = _textView.Object;
            _buffer.TextBufferImpl = _textBuffer;

            _factory = new MockFactory(MockBehavior.Loose);
            _factory.DefaultValue = DefaultValue.Mock;
            _textChangeTrackerFactory = _factory.Create<ITextChangeTrackerFactory>();
            _buffer.NormalModeImpl = _factory.Create<INormalMode>().Object;
            _buffer.VisualBlockModeImpl = _factory.Create<IVisualMode>().Object;
            _buffer.VisualCharacterModeImpl = _factory.Create<IVisualMode>().Object;
            _buffer.VisualLineModeImpl = _factory.Create<IVisualMode>().Object;
            _trackerRaw = new ChangeTracker(_textChangeTrackerFactory.Object);
            _tracker = _trackerRaw;
            _trackerRaw.OnVimBufferCreated(_buffer);
        }


    }
}
