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
            IsRenameActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Cancel()
        {
            try
            {
                var activeSessionPropertyInfo = _inlineRenameService.GetType().GetProperty("ActiveSession", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                var activeSession = activeSessionPropertyInfo.GetValue(_inlineRenameService, null);

                // Look up Microsoft.CodeAnalysis.Editor.Implementation.InlineRename.InlineRenameSession.Cancel()
                // and call it.
                var cancelMethodInfo = activeSession.GetType().GetMethod("Cancel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                cancelMethodInfo.Invoke(activeSession, null);
            }
            catch (Exception)
            {
                // Cancel failed
            }
        }

        internal static bool TryCreate(SVsServiceProvider vsServiceProvider, out IRoslynRenameUtil roslynRenameUtil)
        {
            var ret = TryCreateCore(vsServiceProvider, out RoslynRenameUtil util);
            roslynRenameUtil = util;
            return ret;
        }

        internal static bool TryCreateCore(SVsServiceProvider vsServiceProvider, out RoslynRenameUtil roslynRenameUtil)
        {
            Type getActiveSessionChangedEventArgsType(string versionNumber)
            {
                var typeName = "ActiveSessionChangedEventArgs";

                // This type moved between DLLS in newer versions of Visual Studio. Accept it in any of the locations.
                var all = new[]
                {
                    "Microsoft.CodeAnalysis.EditorFeatures",
                    "Microsoft.CodeAnalysis.EditorFeatures.Wpf",
                };

                foreach (var assemblyName in all)
                {
                    var name = $"Microsoft.CodeAnalysis.Editor.Implementation.InlineRename.InlineRenameService+{typeName}, {assemblyName}, Version={versionNumber}, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
                    try
                    {
                        var type = Type.GetType(name, throwOnError: false);
                        if (type != null)
                        {
                            return type;
                        }
                    }
                    catch (Exception)
                    {
                        // There are cases it will throw even though we specified not to throw.
                    }
                }

                throw new Exception($"Could not locate {typeName}");
            }
            try
            {
                var componentModel = vsServiceProvider.GetService<SComponentModel, IComponentModel>();
                var exportProvider = componentModel.DefaultExportProvider;
                var inlineRenameService = exportProvider.GetExportedValue<object>("Microsoft.CodeAnalysis.Editor.Implementation.InlineRename.InlineRenameService");
                var inlineRenameServiceType = inlineRenameService.GetType();
                var activeSessionPropertyInfo = inlineRenameServiceType.GetProperty("ActiveSession", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                roslynRenameUtil = new RoslynRenameUtil(inlineRenameService, activeSessionPropertyInfo);

                // Subscribe to the event
                var version = GetRoslynVersionNumber(inlineRenameService.GetType().Assembly);
                var activeSessionChangedEventInfo = inlineRenameServiceType.GetEvent("ActiveSessionChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var eventArgsTypeArgument = getActiveSessionChangedEventArgsType(version);
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

        internal static string GetRoslynVersionNumber(Assembly assembly)
        {
            return assembly.GetName().Version.ToString();
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

        void IRoslynRenameUtil.Cancel()
        {
            Cancel();
        }

        #endregion

    }
}
