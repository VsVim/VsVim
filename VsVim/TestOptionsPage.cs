using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf;
using System.Windows.Controls;
using System.ComponentModel.Composition;

namespace VsVim
{
    public class TestOptionsPage : IOptionsPage
    {
        public string Name
        {
            get { return "Test"; }
        }

        public System.Windows.FrameworkElement VisualElement
        {
            get { return new TextBox() { Text = "hello world" }; }
        }
    }

    [Export(typeof(IOptionsPageFactory))]
    public class TestOptionsPageFactory : IOptionsPageFactory
    {
        public IOptionsPage CreateOptionsPage()
        {
            return new TestOptionsPage();
        }
    }
}
