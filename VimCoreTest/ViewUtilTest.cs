using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace VimCore.Test
{
    /// <summary>
    /// Summary description for ViewUtilTest
    /// </summary>
    public class ViewUtilTest
    {
        private IWpfTextView _view;

        public void CreateView(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
        }

        public void Init()
        {
            CreateView(
                "foo bar baz", 
                "boy kick ball",
                "a big dog");
        }

    }
}
