﻿using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Classification;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    internal sealed class CommandMargin : IWpfTextViewMargin
    {
        internal const string Name = "Vim Command Margin";

        private readonly CommandMarginControl _margin = new CommandMarginControl();
        private readonly CommandMarginController _controller;
        private bool _enabled;

        public CommandMargin(FrameworkElement parentVisualElement, IVimBuffer buffer, IEditorFormatMap editorFormatMap, IClassificationFormatMap classificationFormatMap, ICommonOperations commonOperations, IClipboardDevice clipboardDevice, bool isFirstCommandMargin)
        {
            _margin.CommandLineTextBox.Text = isFirstCommandMargin ? $"Welcome to VsVim Version {VimConstants.VersionNumber}" : string.Empty;
            _controller = new CommandMarginController(buffer, parentVisualElement, _margin, editorFormatMap, classificationFormatMap, commonOperations, clipboardDevice);
            _enabled = true;
        }

        private void UpdateEnabled(bool enabled)
        {
            if (_enabled == enabled)
            {
                return;
            }

            if (enabled)
            {
                _margin.Visibility = Visibility.Visible;
                _controller.Connect();
                _controller.Reset();
            }
            else
            {
                _margin.Visibility = Visibility.Collapsed;
                _controller.Disconnect();
            }
        }

        public FrameworkElement VisualElement
        {
            get { return _margin; }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { UpdateEnabled(value); }
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return marginName == Name ? this : null;
        }

        public double MarginSize
        {
            get { return 25; }
        }

        public void Dispose()
        {
            _controller.Disconnect();
        }
    }
}
