using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using Microsoft.FSharp.Core;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Utilities;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.ObjectModel;

namespace VsVim
{
    public static class Extensions
    {
        #region FSharpOption<T>

        public static bool IsSome<T>(this FSharpOption<T> opt)
        {
            return FSharpOption<T>.get_IsSome(opt);
        }

        #endregion

        #region Command

        public static IEnumerable<string> GetCommandStrings(this EnvDTE.Command command)
        {
            if (null == command)
            {
                throw new ArgumentException("command");
            }

            var name = command.Name;
            var bindings = command.Bindings as object[];
            if (bindings != null)
            {
                return bindings
                    .Where(x => x is string)
                    .Cast<string>()
                    .Where(x => !String.IsNullOrEmpty(x));
            }

            var singleBinding = command.Bindings as string;
            if (singleBinding != null)
            {
                return Enumerable.Repeat(singleBinding, 1);
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<CommandKeyBinding> GetCommandKeyBindings(this EnvDTE.Command command)
        {
            if (null == command)
            {
                throw new ArgumentNullException("command");
            }

            foreach ( var cur in command.GetCommandStrings())
            {
                KeyBinding binding;
                if ( KeyBinding.TryParse(cur, out binding))
                {
                    yield return new CommandKeyBinding(command.Name, binding);
                }
            }
        }

        public static IEnumerable<KeyBinding> GetKeyBindings(this EnvDTE.Command command)
        {
            return GetCommandKeyBindings(command).Select(x => x.KeyBinding);
        }

        public static bool HasKeyBinding(this EnvDTE.Command command, KeyBinding binding)
        {
            return GetCommandKeyBindings(command).Any(x => x.KeyBinding == binding);
        }

        public static void SafeResetBindings(this EnvDTE.Command command)
        {
            try
            {
                command.Bindings = new object[] { };
            }
            catch (COMException)
            {
                // Several implementations, Transact SQL in particular, return E_FAIL for this
                // operation.  Simply ignore the failure and continue
            }
        }

        public static void SafeSetBindings(this EnvDTE.Command command, KeyBinding binding)
        {
            try
            {
                command.Bindings = new object[] { binding.CommandString };
            }
            catch (COMException)
            {

            }
        }

        #endregion

        #region Commands

        public static IEnumerable<EnvDTE.Command> GetCommands(this Commands commands)
        {
            return commands.Cast<EnvDTE.Command>();
        }

        #endregion

        #region PropertyCollection

        public static void AddTypedProperty<T>(this PropertyCollection col, T value)
        {
            col.AddProperty(typeof(T), value);
        }

        public static FSharpOption<T> TryGetTypedProperty<T>(this PropertyCollection col)
        {
            T value;
            if (col.TryGetProperty(typeof(T), out value))
            {
                return FSharpOption<T>.Some(value);
            }

            return FSharpOption<T>.None;
        }

        public static bool RemoveTypedProperty<T>(this PropertyCollection col)
        {
            return col.RemoveProperty(typeof(T));
        }

        #endregion

        #region IVsTextLines

        /// <summary>
        /// Get the file name of the presented view.  If the name cannot be discovered an empty string will be returned
        /// </summary>
        public static string GetFileName(this IVsTextLines lines)
        {
            try
            {
                // GUID_VsBufferMoniker
                var monikerId = Constants.VsUserData_FileNameMoniker;
                var userData = (IVsUserData)lines;
                object data = null;
                if (Microsoft.VisualStudio.VSConstants.S_OK != userData.GetData(ref monikerId, out data)
                    || String.IsNullOrEmpty(data as string))
                {
                    return String.Empty;
                }

                return (string)data;
            }
            catch (InvalidCastException)
            {
                return String.Empty;
            }
        }

        #endregion

        #region IVsUIShell

        private sealed class ModelessUtil : IDisposable
        {
            private readonly IVsUIShell _vsShell;
            public ModelessUtil(IVsUIShell vsShell)
            {
                _vsShell = vsShell;
                vsShell.EnableModeless(0);
            }
            public void Dispose()
            {
                _vsShell.EnableModeless(-1); 
            }
        }

        public static IDisposable EnableModelessDialog(this IVsUIShell vsShell)
        {
            return new ModelessUtil(vsShell);
        }

        #endregion

        #region IServiceProvider

        public static TInterface GetService<TService, TInterface>(this IServiceProvider sp)
        {
            return (TInterface)sp.GetService(typeof(TService));
        }

        #endregion

        #region _DTE

        public static IEnumerable<Project> GetProjects(this _DTE dte)
        {
            var list = dte.Solution.Projects;
            for (int i = 1; i <= list.Count; i++)
            {
                yield return list.Item(i);
            }
        }

        public static IEnumerable<ProjectItem> GetProjectItems(this _DTE dte, string fileName)
        {
            foreach (var cur in dte.GetProjects())
            {
                ProjectItem item;
                if (cur.TryGetProjectItem(fileName, out item))
                {
                    yield return item;
                }
            }
        }

        #endregion 

        #region Project

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

        #endregion

        #region ObservableCollection<T>

        public static void AddRange<T>(this ObservableCollection<T> col, IEnumerable<T> enumerable)
        {
            foreach (var cur in enumerable)
            {
                col.Add(cur);
            }
        }

        #endregion

        #region IEnumerable<T>

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> del)
        {
            foreach (var cur in enumerable)
            {
                del(cur);
            }
        }

        #endregion
    }
}
