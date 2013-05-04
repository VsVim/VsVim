using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Operations;
using Xunit;
using Moq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.UnitTest
{
    public class VimHostTests
    {
        public class RunCommandTests
        {
            [Fact]
            public void ItUsesWorkingDirectoryFromVimData()
            {
                const string cwd = @"C:\Windows";
                var vimHost = new Mock<VimHost>(Mock.Of<ITextBufferFactoryService>(), 
                                          Mock.Of<ITextEditorFactoryService>(),
                                          Mock.Of<ITextDocumentFactoryService>(),
                                          Mock.Of<IEditorOperationsFactoryService>()){CallBase = true}.Object;
                var vimData = Mock.Of<IVimData>(x => x.CurrentDirectory == cwd);

                vimHost.RunCommand("pwd", "", vimData);

                // Not sure if we can do anything besides verify that it used the getter
                Mock.Get(vimData).VerifyGet(x => x.CurrentDirectory);
            }
        }
    }
}
