using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Input;
using Vim.Extensions;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IMouseDevice))]
    [Export(typeof(IMouseProcessorProvider))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Mouse Device")]
    internal sealed class MouseDeviceImpl : IMouseDevice, IMouseProcessorProvider
    {
        private sealed class Handler : MouseProcessorBase
        {
            private bool _inDrag;
            private bool _leftDown;

            internal bool InDrag
            {
                get { return _inDrag; }
            }

            public override void PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
            {
                _leftDown = true;
                base.PreprocessMouseLeftButtonDown(e);
            }

            public override void PostprocessMouseLeftButtonUp(MouseButtonEventArgs e)
            {
                _leftDown = false;
                _inDrag = false;
                base.PostprocessMouseLeftButtonUp(e);
            }

            public override void PostprocessMouseMove(MouseEventArgs e)
            {
                if (_leftDown)
                {
                    _inDrag = true;
                }

                base.PostprocessMouseMove(e);
            }
        }

        private static readonly object s_key = new object();

        private readonly MouseDevice _mouseDevice = InputManager.Current.PrimaryMouseDevice;

        private bool TryGetHandler(ITextView textView, out Handler handler)
        {
            return textView.Properties.TryGetPropertySafe(s_key, out handler);
        }

        public bool IsLeftButtonPressed
        {
            get { return _mouseDevice.LeftButton == MouseButtonState.Pressed; }
        }

        public bool IsRightButtonPressed
        {
            get { return _mouseDevice.RightButton == MouseButtonState.Pressed; }
        }

        public bool InDragOperation(ITextView textView)
        {
            if (TryGetHandler(textView, out Handler handler))
            {
                return handler.InDrag;
            }

            return false;
        }

        public FSharpOption<VimPoint> GetPosition(ITextView textView)
        {
            if (textView is IWpfTextView wpfTextView)
            {
                var position = _mouseDevice.GetPosition(wpfTextView.VisualElement);
                return FSharpOption.Create(new VimPoint(x: position.X, y: position.Y));
            }

            return null;
        }

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            if (!TryGetHandler(wpfTextView, out Handler handler))
            {
                handler = new Handler();
                wpfTextView.Properties.AddProperty(s_key, handler);
            }

            return handler;
        }
    }
}
