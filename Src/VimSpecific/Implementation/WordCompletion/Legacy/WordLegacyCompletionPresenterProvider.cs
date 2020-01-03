#if !VS_SPECIFIC_MAC
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
#pragma warning disable 649

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion.Legacy
{
    /// <summary>
    /// This class is responsible for creating IIntellisensePresenter instances for presented 
    /// IWordCompletionSession values.
    ///
    /// There is nothing truly special about the presentation of IWordCompletionSession that would
    /// warrant a completely new display.  However the default key board manipulation of the list
    /// involves the use of the CTRL key (CTRL-P for up and CTRL-N for down).  And the default presenter
    /// for IIntellisenseSession instances will fade out the entire list when any CTRL key is 
    /// pressed.  
    /// 
    /// So without any changes the user hits CTRL-N to bring up the completion list which then 
    /// promptly fades out and is unreadable.  This is a hard coded behavior which cannot be reset
    /// by an option or even ugly reflection tricks.  The only way to prevent this behavior is
    /// to provide an alternate IIntellisensePresenter which doesn't respond to calls to fade 
    /// out (this is done by manipulating the Opacity setting).  
    /// 
    /// Rather than build an IIntellisensePresenter from scratch, this class will simply create 
    /// the default presenter, then wrap it in a type which forwards every call but Opacity back
    /// to the default presenter.
    /// </summary>
    [Name("Word Completion Presenter Provider")]
    [Order(Before = "Default Completion Presenter")]
    [Export(typeof(IIntellisensePresenterProvider))]
    [ContentType(VimConstants.AnyContentType)]
    internal sealed class WordLegacyCompletionPresenterProvider : IIntellisensePresenterProvider
    {
        #region WordCompletionPresenter

        /// <summary>
        /// This type wraps the real IPopupIntellisensePresenter and forwards every call but Opacity
        /// down to it.
        /// </summary>
        private sealed class WordCompletionPresenter : IIntellisenseCommandTarget, IPopupIntellisensePresenter, IMouseProcessor, IDisposable, IComponentConnector
        {
            private readonly IPopupIntellisensePresenter _popupIntellisensePresenter;
            private readonly IIntellisenseCommandTarget _intellisenseCommandTarget;
            private readonly IMouseProcessor _mouseProcessor;
            private readonly IDisposable _disposable;
            private readonly IComponentConnector _componentConnector;

            internal WordCompletionPresenter(IPopupIntellisensePresenter source)
            {
                _popupIntellisensePresenter = source;
                _intellisenseCommandTarget = source as IIntellisenseCommandTarget;
                _mouseProcessor = source as IMouseProcessor;
                _disposable = source as IDisposable;
                _componentConnector = source as IComponentConnector;
            }

            bool IIntellisenseCommandTarget.ExecuteKeyboardCommand(IntellisenseKeyboardCommand command)
            {
                return _intellisenseCommandTarget != null
                    ? _intellisenseCommandTarget.ExecuteKeyboardCommand(command)
                    : false;
            }

            /// <summary>
            /// The Opacity setting is explicitly ignored here.  This property is the mechanism by
            /// which the fade in / out behavior is implemented.  We don't respond to the set in order
            /// to prevent it
            /// </summary>
            double IPopupIntellisensePresenter.Opacity
            {
                get { return _popupIntellisensePresenter.Opacity; }
                set { }
            }

            PopupStyles IPopupIntellisensePresenter.PopupStyles
            {
                get { return _popupIntellisensePresenter.PopupStyles; }
            }

            event EventHandler<ValueChangedEventArgs<PopupStyles>> IPopupIntellisensePresenter.PopupStylesChanged
            {
                add { _popupIntellisensePresenter.PopupStylesChanged += value; }
                remove { _popupIntellisensePresenter.PopupStylesChanged -= value; }
            }

            ITrackingSpan IPopupIntellisensePresenter.PresentationSpan
            {
                get { return _popupIntellisensePresenter.PresentationSpan; }
            }

            event EventHandler IPopupIntellisensePresenter.PresentationSpanChanged
            {
                add { _popupIntellisensePresenter.PresentationSpanChanged += value; }
                remove { _popupIntellisensePresenter.PresentationSpanChanged -= value; }
            }

            string IPopupIntellisensePresenter.SpaceReservationManagerName
            {
                get { return _popupIntellisensePresenter.SpaceReservationManagerName; }
            }

            UIElement IPopupIntellisensePresenter.SurfaceElement
            {
                get { return _popupIntellisensePresenter.SurfaceElement; }
            }

            event EventHandler IPopupIntellisensePresenter.SurfaceElementChanged
            {
                add { _popupIntellisensePresenter.SurfaceElementChanged += value; }
                remove { _popupIntellisensePresenter.SurfaceElementChanged -= value; }
            }

            IIntellisenseSession IIntellisensePresenter.Session
            {
                get { return _popupIntellisensePresenter.Session; }
            }

            void IMouseProcessor.PostprocessDragEnter(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessDragEnter(e);
                }
            }

            void IMouseProcessor.PostprocessDragLeave(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessDragLeave(e);
                }
            }

            void IMouseProcessor.PostprocessDragOver(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessDragOver(e);
                }
            }

            void IMouseProcessor.PostprocessDrop(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessDrop(e);
                }
            }

            void IMouseProcessor.PostprocessGiveFeedback(GiveFeedbackEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessGiveFeedback(e);
                }
            }

            void IMouseProcessor.PostprocessMouseDown(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseDown(e);
                }
            }

            void IMouseProcessor.PostprocessMouseEnter(MouseEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseEnter(e);
                }
            }

            void IMouseProcessor.PostprocessMouseLeave(MouseEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseLeave(e);
                }
            }

            void IMouseProcessor.PostprocessMouseLeftButtonDown(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseLeftButtonDown(e);
                }
            }

            void IMouseProcessor.PostprocessMouseLeftButtonUp(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseLeftButtonUp(e);
                }
            }

            void IMouseProcessor.PostprocessMouseMove(MouseEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseMove(e);
                }
            }

            void IMouseProcessor.PostprocessMouseRightButtonDown(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseRightButtonDown(e);
                }
            }

            void IMouseProcessor.PostprocessMouseRightButtonUp(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseRightButtonUp(e);
                }
            }

            void IMouseProcessor.PostprocessMouseUp(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseUp(e);
                }
            }

            void IMouseProcessor.PostprocessMouseWheel(MouseWheelEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessMouseWheel(e);
                }
            }

            void IMouseProcessor.PostprocessQueryContinueDrag(QueryContinueDragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PostprocessQueryContinueDrag(e);
                }
            }

            void IMouseProcessor.PreprocessDragEnter(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessDragEnter(e);
                }
            }

            void IMouseProcessor.PreprocessDragLeave(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessDragLeave(e);
                }
            }

            void IMouseProcessor.PreprocessDragOver(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessDragOver(e);
                }
            }

            void IMouseProcessor.PreprocessDrop(DragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessDrop(e);
                }
            }

            void IMouseProcessor.PreprocessGiveFeedback(GiveFeedbackEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessGiveFeedback(e);
                }
            }

            void IMouseProcessor.PreprocessMouseDown(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseDown(e);
                }
            }

            void IMouseProcessor.PreprocessMouseEnter(MouseEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseEnter(e);
                }
            }

            void IMouseProcessor.PreprocessMouseLeave(MouseEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseLeave(e);
                }
            }

            void IMouseProcessor.PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseLeftButtonDown(e);
                }
            }

            void IMouseProcessor.PreprocessMouseLeftButtonUp(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseLeftButtonUp(e);
                }
            }

            void IMouseProcessor.PreprocessMouseMove(MouseEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseMove(e);
                }
            }

            void IMouseProcessor.PreprocessMouseRightButtonDown(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseRightButtonDown(e);
                }
            }

            void IMouseProcessor.PreprocessMouseRightButtonUp(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseRightButtonUp(e);
                }
            }

            void IMouseProcessor.PreprocessMouseUp(MouseButtonEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseUp(e);
                }
            }

            void IMouseProcessor.PreprocessMouseWheel(MouseWheelEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessMouseWheel(e);
                }
            }

            void IMouseProcessor.PreprocessQueryContinueDrag(QueryContinueDragEventArgs e)
            {
                if (_mouseProcessor != null)
                {
                    _mouseProcessor.PreprocessQueryContinueDrag(e);
                }
            }

            void IDisposable.Dispose()
            {
                if (_disposable != null)
                {
                    _disposable.Dispose();
                }
            }

            void IComponentConnector.Connect(int connectionId, object target)
            {
                if (_componentConnector != null)
                {
                    _componentConnector.Connect(connectionId, target);
                }
            }

            void IComponentConnector.InitializeComponent()
            {
                if (_componentConnector != null)
                {
                    _componentConnector.InitializeComponent();
                }
            }
        }

        #endregion

        /// <summary>
        /// This is the list of all IIntellisensePresenterProvider instances including ourself.  We need
        /// this list so we can create the real IIntellisensePresenter which we will then subsequently 
        /// wrap
        /// </summary>
        [ImportMany]
        internal List<IIntellisensePresenterProvider> Providers;

        IIntellisensePresenter IIntellisensePresenterProvider.TryCreateIntellisensePresenter(IIntellisenseSession session)
        {
            // If this is not associated with an IWordCompletionSession then we don't want to special case
            // this in any way
            if (!session.Properties.ContainsProperty(WordLegacyCompletionSessionFactory.WordCompletionSessionKey))
            {
                return null;
            }

            foreach (var provider in Providers)
            {
                if (provider == this)
                {
                    // Don't consider 'this' here.  It would lead to an infinite loop
                    continue;
                }

                try
                {
                    var presenter = provider.TryCreateIntellisensePresenter(session);

                    // We only need to wrap IPopupIntellisensePresenter values as they are the
                    // only ones which expose opacity
                    if (presenter is IPopupIntellisensePresenter popupPresenter)
                    {
                        return new WordCompletionPresenter(popupPresenter);
                    }

                    if (presenter != null)
                    {
                        return presenter;
                    }
                }
                catch (Exception)
                {
                    // Move onto the next provider
                }
            }

            return null;
        }
    }
}
#endif
