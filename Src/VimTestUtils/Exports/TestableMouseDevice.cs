﻿using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using System.Windows;
using Vim;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IMouseDevice))]
    public sealed class TestableMouseDevice : IMouseDevice
    {
        public bool IsLeftButtonPressed { get; set; }
        public bool InDragOperationImpl { get; set; }

        bool IMouseDevice.IsLeftButtonPressed
        {
            get { return IsLeftButtonPressed; }
        }

        public FSharpOption<VimPoint> GetPosition(ITextView textView)
        {
            return null;
        }

        public bool InDragOperation(ITextView textView)
        {
            return InDragOperationImpl;
        }
    }
}
