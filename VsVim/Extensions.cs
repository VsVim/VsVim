using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using Command = EnvDTE.Command;

namespace VsVim
{
    public static class Extensions
    {
        #region Command

        /// <summary>
        /// Get the binding strings for this Command.  Digs through the various ways a 
        /// binding string can be stored and returns a uniform result
        /// </summary>
        public static IEnumerable<string> GetBindings(this Command command)
        {
            if (null == command)
            {
                throw new ArgumentException("command");
            }

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

        /// <summary>
        /// Get the binding strings in the form of CommandKeyBinding instances
        /// </summary>
        public static IEnumerable<CommandKeyBinding> GetCommandKeyBindings(this Command command)
        {
            if (null == command)
            {
                throw new ArgumentNullException("command");
            }

            // Need a helper method here so that the argument checking is prompt
            return GetCommandKeyBindingsHelper(command);
        }

        private static IEnumerable<CommandKeyBinding> GetCommandKeyBindingsHelper(Command command)
        {
            foreach (var cur in command.GetBindings())
            {
                KeyBinding binding;
                if (KeyBinding.TryParse(cur, out binding))
                {
                    yield return new CommandKeyBinding(command.Name, binding);
                }
            }
        }

        public static IEnumerable<KeyBinding> GetKeyBindings(this Command command)
        {
            return GetCommandKeyBindings(command).Select(x => x.KeyBinding);
        }

        /// <summary>
        /// Does the Command have the provided KeyBinding as a valid binding
        /// </summary>
        public static bool HasKeyBinding(this Command command, KeyBinding binding)
        {
            return GetCommandKeyBindings(command).Any(x => x.KeyBinding == binding);
        }

        /// <summary>
        /// Remove all bindings on the provided Command value
        /// </summary>
        /// <param name="command"></param>
        public static void SafeResetBindings(this Command command)
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

        /// <summary>
        /// Safely reset the bindings on this Command to the provided KeyBinding value
        /// </summary>
        public static void SafeSetBindings(this Command command, KeyBinding binding)
        {
            try
            {
                command.Bindings = new object[] { binding.CommandString };
            }
            catch (COMException)
            {
                // Several implementations, Transact SQL in particular, return E_FAIL for this
                // operation.  Simply ignore the failure and continue
            }
        }

        #endregion

        #region Commands

        public static IEnumerable<Command> GetCommands(this Commands commands)
        {
            return commands.Cast<Command>();
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
                var monikerId = Constants.VsUserDataFileNameMoniker;
                var userData = (IVsUserData)lines;
                object data;
                if (VSConstants.S_OK != userData.GetData(ref monikerId, out data)
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

        public static Result<IVsEnumLineMarkers> GetLineMarkersEnum(this IVsTextLines lines, TextSpan span)
        {
            IVsEnumLineMarkers markers;
            var hresult = lines.EnumMarkers(span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex, 0, (uint)ENUMMARKERFLAGS.EM_ALLTYPES, out markers);
            return Result.CreateSuccessOrError(markers, hresult);
        }

        public static List<IVsTextLineMarker> GetLineMarkers(this IVsTextLines lines, TextSpan span)
        {
            var markers = GetLineMarkersEnum(lines, span);
            return markers.IsSuccess
                ? markers.Value.GetAll()
                : new List<IVsTextLineMarker>();
        }

        #endregion

        #region IVsTextView

        public static Result<IVsTextLines> GetTextLines(this IVsTextView textView)
        {
            IVsTextLines textLines;
            var hresult = textView.GetBuffer(out textLines);
            return Result.CreateSuccessOrError(textLines, hresult);
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

        public static Result<List<IVsWindowFrame>> GetDocumentWindowFrames(this IVsUIShell vsShell)
        {
            IEnumWindowFrames enumFrames;
            var hr = vsShell.GetDocumentWindowEnum(out enumFrames);
            return ErrorHandler.Failed(hr) ? Result.CreateError(hr) : enumFrames.GetContents();
        }

        public static Result<List<IVsWindowFrame>> GetDocumentWindowFrames(this IVsUIShell4 vsShell, __WindowFrameTypeFlags flags)
        {
            IEnumWindowFrames enumFrames;
            var hr = vsShell.GetWindowEnum((uint)flags, out enumFrames);
            return ErrorHandler.Failed(hr) ? Result.CreateError(hr) : enumFrames.GetContents();
        }

        #endregion

        #region IEnumWindowFrames

        public static Result<List<IVsWindowFrame>> GetContents(this IEnumWindowFrames enumFrames)
        {
            var list = new List<IVsWindowFrame>();
            var array = new IVsWindowFrame[16];
            while (true)
            {
                uint num;
                var hr = enumFrames.Next((uint)array.Length, array, out num);
                if (ErrorHandler.Failed(hr))
                {
                    return Result.CreateError(hr);
                }

                if (0 == num)
                {
                    return list;
                }

                for (var i = 0; i < num; i++)
                {
                    list.Add(array[i]);
                }
            }
        }

        #endregion

        #region IVsCodeWindow

        /// <summary>
        /// Is this window currently in a split mode?
        /// </summary>
        public static bool IsSplit(this IVsCodeWindow window)
        {
            IVsTextView primary;
            IVsTextView secondary;
            return TryGetPrimaryView(window, out primary) && TryGetSecondaryView(window, out secondary);
        }

        /// <summary>
        /// Get the primary view of the code window.  Is actually the one on bottom
        /// </summary>
        public static bool TryGetPrimaryView(this IVsCodeWindow codeWindow, out IVsTextView textView)
        {
            return ErrorHandler.Succeeded(codeWindow.GetPrimaryView(out textView)) && textView != null;
        }

        /// <summary>
        /// Get the secondary view of the code window.  Is actually the one on top
        /// </summary>
        public static bool TryGetSecondaryView(this IVsCodeWindow codeWindow, out IVsTextView textView)
        {
            return ErrorHandler.Succeeded(codeWindow.GetSecondaryView(out textView)) && textView != null;
        }

        #endregion

        #region IVsWindowFrame

        public static Result<IVsCodeWindow> GetCodeWindow(this IVsWindowFrame frame)
        {
            var iid = typeof(IVsCodeWindow).GUID;
            var ptr = IntPtr.Zero;
            try
            {
                ErrorHandler.ThrowOnFailure(frame.QueryViewInterface(ref iid, out ptr));
                return Result.CreateSuccess((IVsCodeWindow)Marshal.GetObjectForIUnknown(ptr));
            }
            catch (Exception e)
            {
                // Venus will throw when querying for the code window
                return Result.CreateError(e);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.Release(ptr);
                }
            }

        }

        public static bool TryGetCodeWindow(this IVsWindowFrame frame, out IVsCodeWindow codeWindow)
        {
            var result = GetCodeWindow(frame);
            codeWindow = result.IsSuccess ? result.Value : null;
            return result.IsSuccess;
        }

        #endregion

        #region IVsTextManager

        public static Tuple<bool, IWpfTextView> TryGetActiveTextView(this IVsTextManager vsTextManager, IVsEditorAdaptersFactoryService factoryService)
        {
            IVsTextView vsTextView;
            IWpfTextView textView = null;
            if (ErrorHandler.Succeeded(vsTextManager.GetActiveView(0, null, out vsTextView)) && vsTextView != null)
            {
                textView = factoryService.GetWpfTextView(vsTextView);
            }

            return Tuple.Create(textView != null, textView);
        }

        #endregion

        #region IVsEnumLineMarkers

        /// <summary>
        /// Don't be tempted to make this an IEnumerable because multiple calls would not
        /// produce multiple enumerations since the parameter would need to be reset
        /// </summary>
        public static List<IVsTextLineMarker> GetAll(this IVsEnumLineMarkers markers)
        {
            var list = new List<IVsTextLineMarker>();
            do
            {
                IVsTextLineMarker marker;
                var hresult = markers.Next(out marker);
                if (ErrorHandler.Succeeded(hresult) && marker != null)
                {
                    list.Add(marker);
                }
                else
                {
                    break;
                }

            } while (true);

            return list;
        }

        #endregion

        #region IVsTextLineMarker

        public static Result<TextSpan> GetCurrentSpan(this IVsTextLineMarker marker)
        {
            var array = new TextSpan[1];
            var hresult = marker.GetCurrentSpan(array);
            return Result.CreateSuccessOrError(array[0], hresult);
        }

        public static Result<SnapshotSpan> GetCurrentSpan(this IVsTextLineMarker marker, ITextSnapshot snapshot)
        {
            var span = GetCurrentSpan(marker);
            return span.IsError ? Result.CreateError(span.HResult) : span.Value.ToSnapshotSpan(snapshot);
        }

        public static Result<MARKERTYPE> GetMarkerType(this IVsTextLineMarker marker)
        {
            int type;
            var hresult = marker.GetType(out type);
            return Result.CreateSuccessOrError((MARKERTYPE)type, hresult);
        }

        #endregion

        #region IVsSnippetManager



        #endregion

        #region IVsMonitorSelection

        public static Result<bool> IsCmdUIContextActive(this IVsMonitorSelection selection, Guid cmdId)
        {
            uint cookie;
            var hresult = selection.GetCmdUIContextCookie(ref cmdId, out cookie);
            if (ErrorHandler.Failed(hresult))
            {
                return Result.CreateError(hresult);
            }

            int active;
            hresult = selection.IsCmdUIContextActive(cookie, out active);
            return Result.CreateSuccessOrError(active != 0, hresult);
        }

        #endregion

        #region IServiceProvider

        public static TInterface GetService<TService, TInterface>(this IServiceProvider sp)
        {
            return (TInterface)sp.GetService(typeof(TService));
        }

        #endregion

        #region IContentType

        /// <summary>
        /// Does this IContentType represent C++
        /// </summary>
        public static bool IsCPlusPlus(this IContentType ct)
        {
            return ct.IsOfType(Constants.CPlusPlusContentType);
        }

        /// <summary>
        /// Is this IContentType of any of the specified types
        /// </summary>
        public static bool IsOfAnyType(this IContentType contentType, IEnumerable<string> types)
        {
            foreach (var type in types)
            {
                if (contentType.IsOfType(type))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region ITaggerProvider

        /// <summary>
        /// Creating an ITagger for an ITaggerProvider can fail in a number of ways.  Wrap them
        /// all up here 
        /// </summary>
        public static Result<ITagger<T>> SafeCreateTagger<T>(this ITaggerProvider taggerProvider, ITextBuffer textbuffer)
            where T : ITag
        {
            try
            {
                var tagger = taggerProvider.CreateTagger<T>(textbuffer);
                if (tagger == null)
                {
                    return Result.Error;
                }

                return Result.CreateSuccess(tagger);
            }
            catch (Exception e)
            {
                return Result.CreateError(e);
            }
        }

        #endregion

        #region ITextView

        /// <summary>
        /// This will return the SnapshotSpan values from the EditBuffer which are actually visible
        /// on the screen.
        /// </summary>
        public static NormalizedSnapshotSpanCollection GetVisibleSnapshotSpans(this ITextView textView)
        {
            var bufferGraph = textView.BufferGraph;
            var visualSnapshot = textView.VisualSnapshot;
            var formattedSpan = textView.TextViewLines.GetFormattedSpan();
            if (formattedSpan.IsError)
            {
                return new NormalizedSnapshotSpanCollection();
            }

            var visualSpans = bufferGraph.MapUpToSnapshot(formattedSpan.Value, SpanTrackingMode.EdgeExclusive, visualSnapshot);
            if (visualSpans.Count != 1)
            {
                return visualSpans;
            }

            return bufferGraph.MapDownToSnapshot(visualSpans.Single(), SpanTrackingMode.EdgeExclusive, textView.TextSnapshot);
        }

        /// <summary>
        /// Get the set of all visible SnapshotSpan values and potentially some collapsed / invisible
        /// ones.  It's common for users to have collapsed / invisible regions on the ITextView and 
        /// hence simply getting a single SnapshotSpan for the set of visible text could return a huge
        /// span (potentially many, many thousands) of lines.
        /// 
        /// This method attempts to return the set of SnapshotSpan values which actually have visible
        /// text associated with them.
        /// </summary>
        public static NormalizedSnapshotSpanCollection GetLikelyVisibleSnapshotSpans(this ITextView textView)
        {
            // Mapping up and down is potentially expensive so we don't want to do it unless we have to.  Implement
            // a heuristic to check for large sections of invisible text and if it's not present then just return
            // the value which doesn't require allocations or deep calculations
            var result = textView.TextViewLines.GetFormattedSpan();
            if (result.IsError)
            {
                return new NormalizedSnapshotSpanCollection();
            }

            var formattedSpan = result.Value;
            var formattedLength = formattedSpan.Length;
            if (formattedLength / 2 <= textView.VisualSnapshot.Length)
            {
                return new NormalizedSnapshotSpanCollection(formattedSpan);
            }

            return GetVisibleSnapshotSpans(textView);
        }

        #endregion

        #region ITextViewLineCollection

        /// <summary>
        /// Get the SnapshotSpan for the ITextViewLineCollection.  This can throw when the ITextView is being
        /// laid out so we wrap the try / catch here
        /// </summary>
        public static Result<SnapshotSpan> GetFormattedSpan(this ITextViewLineCollection collection)
        {
            try
            {
                return collection.FormattedSpan;
            }
            catch (Exception ex)
            {
                return Result.CreateError(ex);
            }
        }

        #endregion

        #region ITextSnapshot

        public static Result<SnapshotSpan> ToSnapshotSpan(this TextSpan span, ITextSnapshot snapshot)
        {
            try
            {
                var start = snapshot.GetLineFromLineNumber(span.iStartLine).Start.Add(span.iStartIndex);
                var end = snapshot.GetLineFromLineNumber(span.iEndLine).Start.Add(span.iEndIndex + 1);
                return new SnapshotSpan(start, end);
            }
            catch (Exception ex)
            {
                return Result.CreateError(ex);
            }
        }

        #endregion

        #region SnapshotSpan

        public static TextSpan ToTextSpan(this SnapshotSpan span)
        {
            var start = SnapshotPointUtil.GetLineColumn(span.Start);
            var option = SnapshotSpanUtil.GetLastIncludedPoint(span);
            var end = option.IsSome()
                ? SnapshotPointUtil.GetLineColumn(option.Value)
                : start;
            return new TextSpan
            {
                iStartLine = start.Item1,
                iStartIndex = start.Item2,
                iEndLine = end.Item1,
                iEndIndex = end.Item2
            };
        }

        public static Result<SnapshotSpan> SafeTranslateTo(this SnapshotSpan span, ITextSnapshot snapshot, SpanTrackingMode mode)
        {
            try
            {
                return span.TranslateTo(snapshot, mode);
            }
            catch (Exception ex)
            {
                return Result.CreateError(ex);
            }
        }

        #endregion

        #region SnapshotLineRange

        public static TextSpan ToTextSpan(this SnapshotLineRange range)
        {
            return range.Extent.ToTextSpan();
        }

        public static TextSpan ToTextSpanIncludingLineBreak(this SnapshotLineRange range)
        {
            return range.ExtentIncludingLineBreak.ToTextSpan();
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

        public static IEnumerable<T> GetValues<T>(this IEnumerable<Result<T>> enumerable)
        {
            foreach (var cur in enumerable)
            {
                if (cur.IsSuccess)
                {
                    yield return cur.Value;
                }
            }
        }

        #endregion
    }
}
