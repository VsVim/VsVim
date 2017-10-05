using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Classification;

namespace EditorUtils
{
    public static class Extensions
    {
        #region Dispatcher

        /// <summary>
        /// Run all outstanding events queued on the provided Dispatcher
        /// </summary>
        /// <param name="dispatcher"></param>
        public static void DoEvents(this Dispatcher dispatcher)
        {
            var frame = new DispatcherFrame();
            Action<DispatcherFrame> action = _ => { frame.Continue = false; };
            dispatcher.BeginInvoke(
                DispatcherPriority.SystemIdle,
                action,
                frame);
            Dispatcher.PushFrame(frame);
        }

        #endregion
    }
}
