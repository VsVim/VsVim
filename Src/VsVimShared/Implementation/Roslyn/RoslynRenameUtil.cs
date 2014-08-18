using Microsoft.VisualStudio.ComponentModelHost;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace Vim.VisualStudio.Implementation.Roslyn
{
    internal sealed class RoslynRenameUtil : IRoslynRenameUtil
    {
        private readonly object _inlineRenameService;
        private readonly PropertyInfo _activeSessionPropertyInfo;

        internal bool IsRenameActive
        {
            get
            {
                try
                {
                    return _activeSessionPropertyInfo.GetValue(_inlineRenameService, null) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal event EventHandler IsRenameActiveChanged;

        private RoslynRenameUtil(object inlineRenameService, PropertyInfo activeSessionPropertyInfo)
        {
            _inlineRenameService = inlineRenameService;
            _activeSessionPropertyInfo = activeSessionPropertyInfo;
        }

        private void OnActiveSessionChanged(object sender, EventArgs e)
        {
            var handlers = IsRenameActiveChanged;
            if (handlers != null)
            {
                handlers(this, EventArgs.Empty);
            }
        }

        internal static bool TryCreate(SVsServiceProvider vsServiceProvider, out IRoslynRenameUtil roslynRenameUtil)
        {
            RoslynRenameUtil util;
            bool ret = TryCreateCore(vsServiceProvider, out util);
            roslynRenameUtil = util;
            return ret;
        }

        internal static bool TryCreateCore(SVsServiceProvider vsServiceProvider, out RoslynRenameUtil roslynRenameUtil)
        {
            try
            {
                var componentModel = vsServiceProvider.GetService<SComponentModel, IComponentModel>();
                var exportProvider = componentModel.DefaultExportProvider;
                var inlineRenameService = exportProvider.GetExportedValue<object>("Microsoft.CodeAnalysis.Editor.Implementation.InlineRename.InlineRenameService");
                var inlineRenameServiceType = inlineRenameService.GetType();
                var activeSessionPropertyInfo = inlineRenameServiceType.GetProperty("ActiveSession", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                roslynRenameUtil = new RoslynRenameUtil(inlineRenameService, activeSessionPropertyInfo);

                // Subscribe to the event
                var activeSessionChangedEventInfo = inlineRenameServiceType.GetEvent("ActiveSessionChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var eventArgsTypeArgument = Type.GetType("Microsoft.CodeAnalysis.Editor.Implementation.InlineRename.InlineRenameService+ActiveSessionChangedEventArgs, Microsoft.CodeAnalysis.EditorFeatures, Version=0.7.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                var openType = typeof(EventHandler<>);
                var delegateType = openType.MakeGenericType(eventArgsTypeArgument);
                var methodInfo = roslynRenameUtil.GetType().GetMethod("OnActiveSessionChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                var delegateInstance = Delegate.CreateDelegate(delegateType, roslynRenameUtil, methodInfo);

                var addMethodInfo = activeSessionChangedEventInfo.GetAddMethod(nonPublic: true);
                addMethodInfo.Invoke(inlineRenameService, new[] { delegateInstance });

                return true;
            }
            catch (Exception)
            {
                // If type load fails that is not a problem.  It is expected to happen in cases where
                // Roslyn is not available
                roslynRenameUtil = null;
                return false;
            }
        }

        #region IRoslynRenameUtil

        bool IRoslynRenameUtil.IsRenameActive
        {
            get { return IsRenameActive; }
        }

        event EventHandler IRoslynRenameUtil.IsRenameActiveChanged
        {
            add { IsRenameActiveChanged += value; }
            remove { IsRenameActiveChanged -= value; }
        }

        #endregion

    }
}
