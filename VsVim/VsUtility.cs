using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using EnvDTE;

namespace VsVim
{
    public static class VsUtility
    {
        public static TInterface GetService<TService, TInterface>(this Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp)
        {
            return (TInterface)GetService(sp, typeof(TService).GUID, typeof(TInterface).GUID);
        }

        public static object GetService(this Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp, Guid serviceGuid, Guid interfaceGuid)
        {
            var ppvObject = IntPtr.Zero;
            if (0 != sp.QueryService(ref serviceGuid, ref interfaceGuid, out ppvObject))
            {
                throw new InvalidOperationException();
            }

            try
            {
                return Marshal.GetObjectForIUnknown(ppvObject);
            }
            finally
            {
                if ( ppvObject != IntPtr.Zero )
                {
                    Marshal.Release(ppvObject);
                }
            }
        }

        public static IEnumerable<Project> GetProjects(this _DTE dte)
        {
            var list = dte.Solution.Projects;
            for (int i = 1; i <= list.Count; i++)
            {
                yield return list.Item(i);
            }
        }

        public static IEnumerable<ProjectItem> GetProjecItems(this Project project)
        {
            var items = project.ProjectItems;
            for (int i = 1; i <= items.Count; i++)
            {
                yield return items.Item(i);
            }
        }

        public static bool TryGetProjectItem(this Project project, string fileName, out ProjectItem item)
        {
            try
            {
                item = project.ProjectItems.Item(fileName);
                return true;
            }
            catch (ArgumentException)
            {
                item = null;
                return false;
            }
        }

        public static IEnumerable<ProjectItem> GetProjectItems(this _DTE dte, string fileName)
        {
            foreach (var cur in dte.GetProjects())
            {
                ProjectItem item;
                if ( cur.TryGetProjectItem(fileName, out item))
                {
                    yield return item;
                }
            }
        }


    }
}
