using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows.Threading;
using Vim.EditorHost;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;
using Vim.UnitTest;
using Vim.VisualStudio.UnitTest.Mock;
using Xunit;
using System.Threading;
using EnvDTE;
using Thread = System.Threading.Thread;

namespace Vim.VisualStudio.UnitTest
{
    /// <summary>
    /// At least a cursory attempt at getting memory leak detection into a unit test.  By 
    /// no means a thorough example because I can't accurately simulate Visual Studio 
    /// integration without starting Visual Studio.  But this should at least help me catch
    /// a portion of them. 
    /// </summary>
    public sealed class MemoryLeakTest : VsVimTestBase
    {
        #region Exports

        #endregion

        private readonly TestableSynchronizationContext _synchronizationContext;

        public MemoryLeakTest()
        {
            _synchronizationContext = new TestableSynchronizationContext();
        }

        public override void Dispose()
        {
            try
            {
                _synchronizationContext.RunAll();
            }
            finally
            {
                _synchronizationContext.Dispose();
            }
            base.Dispose();
        }

        private void RunGarbageCollector()
        {
            for (var i = 0; i < 15; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
                _synchronizationContext.RunAll(); 
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);
                GC.Collect();
            }
        }

        private void ClearHistory(ITextBuffer textBuffer)
        {
            if (VimEditorHost.BasicUndoHistoryRegistry.TryGetBasicUndoHistory(textBuffer, out IBasicUndoHistory basicUndoHistory))
            {
                basicUndoHistory.Clear();
            }
        }

        private new IVimBuffer CreateVimBuffer(string[] roles = null)
        {
            var factory = VimEditorHost.CompositionContainer.GetExport<ITextEditorFactoryService>().Value;
            ITextView textView;
            if (roles is null)
            {
                textView = factory.CreateTextView();
            }
            else
            {
                var bufferFactory = VimEditorHost.CompositionContainer.GetExport<ITextBufferFactoryService>().Value;
                var textViewRoles = factory.CreateTextViewRoleSet(roles);
                textView = factory.CreateTextView(bufferFactory.CreateTextBuffer(), textViewRoles);
            }

            // Verify we actually created the IVimBuffer instance 
            var vimBuffer = VimEditorHost.Vim.GetOrCreateVimBuffer(textView);
            Assert.NotNull(vimBuffer);

            // Do one round of DoEvents since several services queue up actions to 
            // take immediately after the IVimBuffer is created
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.CurrentDispatcher.DoEvents();
            }

            // Force the buffer into normal mode if the WPF 'Loaded' event
            // hasn't fired.
            if (vimBuffer.ModeKind == ModeKind.Uninitialized)
            {
                vimBuffer.SwitchMode(vimBuffer.VimBufferData.VimTextBuffer.ModeKind, ModeArgument.None);
            }

            return vimBuffer;
        }

        /// <summary>
        /// Make sure that we respect the host policy on whether or not an IVimBuffer should be created for a given
        /// ITextView
        ///
        /// This test is here because it's one of the few places where we load every component in every assembly into
        /// our MEF container.  This gives us the best chance of catching a random new component which accidentally
        /// introduces a new IVimBuffer against the host policy
        /// </summary>
        [WpfFact]
        public void RespectHostCreationPolicy()
        {
            var container = VimEditorHost.CompositionContainer;
            var vsVimHost = container.GetExportedValue<VsVimHost>();
            vsVimHost.DisableVimBufferCreation = true;
            try
            {
                var factory = container.GetExportedValue<ITextEditorFactoryService>();
                var textView = factory.CreateTextView();
                var vim = container.GetExportedValue<IVim>();
                Assert.False(vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer));
            }
            finally
            {
                vsVimHost.DisableVimBufferCreation = false;
            }
        }

        /// <summary>
        /// Run a sanity check which just tests the ability for an ITextView to be created
        /// and closed without leaking memory that doesn't involve the creation of an 
        /// IVimBuffer
        /// 
        /// TODO: This actually creates an IVimBuffer instance.  Right now IVim will essentially
        /// create an IVimBuffer for every ITextView created hence one is created here.  Need
        /// to fix this so we have a base case to judge the memory leak tests by
        /// </summary>
        [WpfFact]
        public void TextViewOnly()
        {
            var container = VimEditorHost.CompositionContainer;
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();
            var weakReference = new WeakReference(textView);
            textView.Close();
            textView = null;

            RunGarbageCollector();
            Assert.Null(weakReference.Target);
        }

        /// <summary>
        /// Run a sanity check which just tests the ability for an ITextViewHost to be created
        /// and closed without leaking memory that doesn't involve the creation of an
        /// IVimBuffer
        /// </summary>
        [WpfFact]
        public void TextViewHostOnly()
        {
            var container = VimEditorHost.CompositionContainer;
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();
            var textViewHost = factory.CreateTextViewHost(textView, setFocus: true);
            var weakReference = new WeakReference(textViewHost);
            textViewHost.Close();
            textView = null;
            textViewHost = null;

            RunGarbageCollector();
            Assert.Null(weakReference.Target);
        }

        [WpfFact]
        public void VimWpfDoesntHoldBuffer()
        {
            var container = VimEditorHost.CompositionContainer;
            var factory = container.GetExport<ITextEditorFactoryService>().Value;
            var textView = factory.CreateTextView();

            // Verify we actually created the IVimBuffer instance 
            var vim = container.GetExport<IVim>().Value;
            var vimBuffer = vim.GetOrCreateVimBuffer(textView);
            Assert.NotNull(vimBuffer);

            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(textView);

            // Clean up 
            ClearHistory(textView.TextBuffer);
            textView.Close();
            textView = null;
            Assert.True(vimBuffer.IsClosed);
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

        [WpfFact]
        public void VsVimDoesntHoldBuffer()
        {
            var vimBuffer = CreateVimBuffer();
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

        [WpfFact]
        public void SetGlobalMarkAndClose()
        {
            var vimBuffer = CreateVimBuffer();
            vimBuffer.MarkMap.SetMark(Mark.OfChar('a').Value, vimBuffer.VimBufferData, 0, 0);
            vimBuffer.MarkMap.SetMark(Mark.OfChar('A').Value, vimBuffer.VimBufferData, 0, 0);
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

        /// <summary>
        /// Change tracking is currently IVimBuffer specific.  Want to make sure it's
        /// not indirectly holding onto an IVimBuffer reference
        /// </summary>
        [WpfFact]
        public void ChangeTrackerDoesntHoldTheBuffer()
        {
            var vimBuffer = CreateVimBuffer();
            vimBuffer.TextBuffer.SetText("hello world");
            vimBuffer.Process("dw");
            var weakVimBuffer = new WeakReference(vimBuffer);
            var weakTextView = new WeakReference(vimBuffer.TextView);

            // Clean up 
            ClearHistory(vimBuffer.TextBuffer);
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakVimBuffer.Target);
            Assert.Null(weakTextView.Target);
        }

        /// <summary>
        /// Make sure the caching which comes with searching doesn't hold onto the buffer
        /// </summary>
        [WpfFact]
        public void SearchCacheDoesntHoldTheBuffer()
        {
            var vimBuffer = CreateVimBuffer();
            vimBuffer.TextBuffer.SetText("hello world");
            vimBuffer.Process("/world", enter: true);

            // This will kick off five search items on the thread pool, each of which
            // has a strong reference. Need to wait until they have all completed.
            var count = 0;
            while (count < 5)
            {
                while (_synchronizationContext.PostedCallbackCount > 0)
                {
                    _synchronizationContext.RunOne();
                    count++;
                }

                Thread.Yield();
            }

            var weakTextBuffer = new WeakReference(vimBuffer.TextBuffer);

            // Clean up 
            vimBuffer.TextView.Close();
            vimBuffer = null;

            RunGarbageCollector();
            Assert.Null(weakTextBuffer.Target);
        }
    }
}
