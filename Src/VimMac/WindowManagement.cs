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
        static BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

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

		private static Notebook ToNotebook(object obj)
        {
            var notebookType = obj.GetType();
            var childrenProperty = notebookType.GetProperty("Children", instanceFlags);
            var children = (object[])childrenProperty.GetValue(obj);
            bool isActiveNotebook = false;
            int currentTab = 0;

            if (children.Length > 0)
            {
                var tabstrip = children[0];
                var tabstripType = tabstrip.GetType();
                isActiveNotebook = (bool)tabstripType.GetProperty("IsActiveNotebook").GetValue(tabstrip);
            }

            currentTab = (int)notebookType.GetProperty("CurrentTabIndex", instanceFlags).GetValue(obj);

            var tabs = (IEnumerable<object>)notebookType.GetProperty("Tabs", instanceFlags).GetValue(obj);

            var files = tabs.Select(GetTabFileName).ToImmutableArray();

            return new Notebook(isActiveNotebook, currentTab, files);
        }

    }
}
