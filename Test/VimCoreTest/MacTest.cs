using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using Vim.EditorHost;
using Vim.UI.Wpf.Implementation.Misc;
using Vim.UnitTest;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Test that we don't break the Vim Mac implementation as much as possible until we can get
    /// full Mac testing support https://github.com/VsVim/VsVim/issues/2907
    /// </summary>
    public sealed class MacTest
    {
        [WpfFact]
        public void Composition()
        {
            var baseTypeFilter = VimTestBase.GetVimEditorHostTypeFilter(typeof(TestableVimHost));
            Func<Type, bool> typeFilter = (type) =>
            {
                if (type.GetCustomAttributes(typeof(ExportAttribute), inherit: false).Length > 0)
                {
                    if (!baseTypeFilter(type))
                    {
                        return false;
                    }

                    if (type.FullName.StartsWith("Vim.UI.Wpf"))
                    {
                        if (type != typeof(DisplayWindowBrokerFactoryService) &&
                            type != typeof(AlternateKeyUtil))
                        {
                            return false;
                        }
                    }
                }

                return true;
            };

            var factory = new VimEditorHostFactory(typeFilter);
            var host = factory.CreateVimEditorHost();
            var textView = host.CreateTextView("");
            host.Vim.AutoLoadVimRc = false;
            var vimBuffer = host.Vim.CreateVimBuffer(textView);
            vimBuffer.Process("ihello Mac");
            Assert.Equal("hello Mac", textView.TextBuffer.GetLineText(0));
        }
    }
}
