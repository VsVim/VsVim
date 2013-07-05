using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim;
using Vim.UnitTest;
using VsVim.Implementation.Misc;
using Xunit;

namespace VsVim.UnitTest
{
    public abstract class ReportDesignerUtilTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly ReportDesignerUtil _reportDesignerUtilRaw;
        private readonly IReportDesignerUtil _reportDesignerUtil;
        private readonly Mock<IVsEditorAdaptersFactoryService> _vsEditorAdaptersFactoryService;

        protected ReportDesignerUtilTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _vsEditorAdaptersFactoryService = _factory.Create<IVsEditorAdaptersFactoryService>();
            _reportDesignerUtilRaw = new ReportDesignerUtil(_vsEditorAdaptersFactoryService.Object);
            _reportDesignerUtil = _reportDesignerUtilRaw;
        }

        public sealed class IsExpressionViewTest : ReportDesignerUtilTest
        {
            public ITextView Create(string contentTypeName, string baseType)
            {
                var contentType = GetOrCreateContentType(contentTypeName, baseType);
                return CreateTextView(contentType);
            }

            [Fact]
            public void CSharpContent()
            {
                var textView = Create("csharp", "code");
                Assert.False(_reportDesignerUtil.IsExpressionView(textView));
            }

            [Fact]
            public void RdlContent()
            {
                var textView = Create(ReportDesignerUtil.RdlContentTypeName, "code");
                var vsTextBuffer = _factory.Create<IVsTextBuffer>();
                var vsUserData = vsTextBuffer.As<IVsUserData>();
                object data = "test";
                var guid = ReportDesignerUtil.ReportContextGuid;
                vsUserData.Setup(x => x.GetData(ref guid, out data)).Returns(VSConstants.S_OK);
                _vsEditorAdaptersFactoryService.Setup(x => x.GetBufferAdapter(textView.TextBuffer)).Returns(vsTextBuffer.Object);
                Assert.True(_reportDesignerUtil.IsExpressionView(textView));
            }
        }

        public sealed class IsSpecialHandledTest : ReportDesignerUtilTest
        {
            private void VerifySpecial(VimKey vimKey, KeyModifiers keyModifiers = KeyModifiers.None)
            {
                var keyInput = KeyInputUtil.ApplyModifiersToVimKey(vimKey, keyModifiers);
                Assert.True(_reportDesignerUtil.IsSpecialHandled(keyInput));
            }

            [Fact]
            public void AlphaKeysUpper()
            {
                foreach (var c in TestConstants.LowerCaseLetters)
                {
                    var keyInput = KeyInputUtil.CharToKeyInput(c);
                    Assert.False(_reportDesignerUtil.IsSpecialHandled(keyInput));
                }
            }

            [Fact]
            public void AlphaKeysLower()
            {
                foreach (var c in TestConstants.UpperCaseLetters)
                {
                    var keyInput = KeyInputUtil.CharToKeyInput(c);
                    Assert.False(_reportDesignerUtil.IsSpecialHandled(keyInput));
                }
            }

            [Fact]
            public void BackKey()
            {
                VerifySpecial(VimKey.Back);
            }

            [Fact]
            public void DeleteKey()
            {
                VerifySpecial(VimKey.Delete);
            }

            [Fact]
            public void ArrowKeys()
            {
                VerifySpecial(VimKey.Left);
                VerifySpecial(VimKey.Left, KeyModifiers.Control);
                VerifySpecial(VimKey.Left, KeyModifiers.Shift);
                VerifySpecial(VimKey.Right);
                VerifySpecial(VimKey.Right, KeyModifiers.Control);
                VerifySpecial(VimKey.Right, KeyModifiers.Shift);
                VerifySpecial(VimKey.Up);
                VerifySpecial(VimKey.Up, KeyModifiers.Shift);
                VerifySpecial(VimKey.Down);
                VerifySpecial(VimKey.Down, KeyModifiers.Shift);
            }
        }
    }
}
