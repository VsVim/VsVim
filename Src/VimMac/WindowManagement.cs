using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using MonoDevelop.Ide;

namespace Vim.Mac
{
    /// <summary>
    /// Represents the left or right group of tabs
    /// </summary>
    internal class Notebook
    {
        public Notebook(bool isActive, int activeTab, ImmutableArray<string> fileNames)
        {
            IsActive = isActive;
            ActiveTab = activeTab;
            FileNames = fileNames;
        }

        public bool IsActive { get; }
        public int ActiveTab { get; }
        public ImmutableArray<string> FileNames { get; }
    }

    internal static class WindowManagement
    {
        private const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static object[] emptyArray = Array.Empty<object>();
        private static object shellNotebook;
        private static PropertyInfo activeNotebookProperty;

        static WindowManagement()
        {
            // CocoaWorkbenchController
            var shell = GetPropertyValue(IdeApp.Workbench, "Shell");

            // IShellNotebook IShell.TabControl
            shellNotebook = GetPropertyValueFromInterface(shell, "IShell", "TabControl");

            // SdiDragNotebook : DockNotebookController
            var dockNotebookControllerType = shellNotebook.GetType().BaseType;

            // public static IShellNotebook ActiveNotebook
            activeNotebookProperty = dockNotebookControllerType.GetProperty("ActiveNotebook", staticFlags);
        }

        /// <summary>
        /// Utility function to map tabs and windows into a format that we can use
        /// from the Mac VimHost
        /// </summary>
        public static ImmutableArray<Notebook> GetNotebooks()
        {
            var activeNotebook = activeNotebookProperty.GetValue(shellNotebook);

            // DockNotebookContainer Container { get; set; }
            var container = GetPropertyValue(shellNotebook, "Container");

            // public IEnumerable<IShellNotebook> GetNotebooks()
            var notebookController = GetPropertyValue(container, "NotebookController");
            var getNotebooksMethod = container.GetType().GetMethod("GetNotebooks");
            var notebooks = (IEnumerable<object>)getNotebooksMethod.Invoke(container, null);

            return notebooks.Select(ToNotebook).ToImmutableArray();

            Notebook ToNotebook(object container)
            {
                var tabs = ((IEnumerable<object>)GetPropertyValue(container, "Tabs")).ToArray();
                var files = tabs.Select(GetTabFileName).ToImmutableArray();
                var activeTab = 0;

                for(int index = 0; index < tabs.Length; index++)
                {
                    var isActive = (bool)GetPropertyValue(tabs[index], "Active");
                    if (isActive)
                    {
                        activeTab = index;
                        break;
                    }
                }

                return new Notebook(container == activeNotebook, activeTab, files);
            }

            string GetTabFileName(object tab)
            {
                return (string)GetPropertyValue(tab, "Tooltip");
            }
        }

        private static object GetPropertyValue(object o, string propertyName)
        {
            var objType = o.GetType();
            var prop = objType.GetProperty(propertyName, instanceFlags);
            var value = prop.GetValue(o);
            return value;
        }

        private static object GetPropertyValueFromInterface(object o, string interfaceName, string propertyName)
        {
            var objType = o.GetType();
            var interfaceType = objType.GetInterface(interfaceName);
            var prop = interfaceType.GetProperty(propertyName, instanceFlags);
            var value = prop.GetValue(o);
            return value;
        }
    }
}
