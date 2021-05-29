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
        const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static object[] emptyArray = Array.Empty<object>();

        /// <summary>
        /// Utility function to map tabs and windows into a format that we can use
        /// from the Mac VimHost
        /// </summary>
        public static ImmutableArray<Notebook> GetNotebooks()
        {
            var workbench = IdeApp.Workbench.RootWindow;
            var workbenchType = workbench.GetType();
            var tabControlProp = workbenchType.GetProperty("TabControl", instanceFlags);
            var tabControl = tabControlProp.GetValue(workbench);
            var container = tabControlProp.PropertyType.GetProperty("Container", instanceFlags);
            var cont = container.GetValue(tabControl, null);
            var notebooks = (IEnumerable<object>)container.PropertyType.GetMethod("GetNotebooks", instanceFlags).Invoke(cont, emptyArray);
            return notebooks.Select(ToNotebook).ToImmutableArray();
        }

        private static string GetTabFileName(object tab)
        {
            var tabType = tab.GetType();
            var fileName = (string)tabType.GetProperty("Tooltip", instanceFlags).GetValue(tab);
            return fileName;
        }

        private static Notebook ToNotebook(object container)
        {
            var notebookType = container.GetType();
            bool isActiveNotebook = IsActiveNotebook(container, notebookType);

            int currentTab = (int)notebookType.GetProperty("CurrentTabIndex", instanceFlags).GetValue(container);

            var tabs = (IEnumerable<object>)notebookType.GetProperty("Tabs", instanceFlags).GetValue(container);

            var files = tabs.Select(GetTabFileName).ToImmutableArray();

            return new Notebook(isActiveNotebook, currentTab, files);
        }

        private static bool IsActiveNotebook(object container, Type notebookType)
        {
            var tabStripControllerProperty = notebookType.GetProperty("TabStripController", instanceFlags);

            bool isActiveNotebook = false;
            Type tabStripType;

            if (tabStripControllerProperty != null)
            {
                var tabStripController = tabStripControllerProperty.GetValue(container);
                // VSMac 8.10+
                tabStripType = tabStripControllerProperty.PropertyType;
                isActiveNotebook = (bool)tabStripType.GetProperty("IsActiveNotebook").GetValue(tabStripController);
            }
            else
            {
                // VSMac 8.9 and earlier
                var childrenProperty = notebookType.GetProperty("Children", instanceFlags);
                var children = (object[])childrenProperty.GetValue(container);

                if (children.Length > 0)
                {
                    var tabStrip = children[0];
                    tabStripType = tabStrip.GetType();
                    isActiveNotebook = (bool)tabStripType.GetProperty("IsActiveNotebook").GetValue(tabStrip);
                }
            }
            return isActiveNotebook;
        }
    }
}
