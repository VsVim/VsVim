#light
namespace Vim
open System
open System.Diagnostics

[<RequireQualifiedAccess>]
type VimTraceKind =
    | Info
    | Error
    | Debug

type VimTraceEventArgs (_message: string, _kind: VimTraceKind) =
    inherit System.EventArgs()

    member x.Message = _message
    member x.TraceKind = _kind
    override x.ToString() = _message

[<AbstractClass>]
type VimTrace() =
    static let _traceEvent = StandardEvent<VimTraceEventArgs>()

    static let _prefixInfo = "VsVim "
    static let _prefixError = "VsVim Error "
    static let _prefixDebug = "VsVim Debug "
    static let _traceSwitch = TraceSwitch("VsVim", "VsVim Trace")

    static member TraceSwitch = _traceSwitch

    [<Conditional("DEBUG")>]
    static member BreakInDebug () =
        if System.Diagnostics.Debugger.IsAttached then
            System.Diagnostics.Debugger.Break()

    [<Conditional("TRACE")>]
    static member TraceInfo(msg: string) = 
        let msg = _prefixInfo + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceInfo, msg)
        VimTrace.Raise msg VimTraceKind.Info

    [<Conditional("TRACE")>]
    static member TraceInfo(format: string, [<ParamArrayAttribute>] args: obj []) = 
        let msg = String.Format(format, args)
        let msg = _prefixInfo + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceInfo, msg)
        VimTrace.Raise msg VimTraceKind.Info

    [<Conditional("TRACE")>]
    static member TraceError(ex: Exception) = 
        let msg = ex.Message + Environment.NewLine + ex.StackTrace
        VimTrace.TraceError(msg)
        VimTrace.Raise msg VimTraceKind.Error
        VimTrace.BreakInDebug()

    [<Conditional("TRACE")>]
    static member TraceError(msg: string) = 
        let msg = _prefixError + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceError, msg)
        VimTrace.Raise msg VimTraceKind.Error

    [<Conditional("TRACE")>]
    static member TraceError(format: string, [<ParamArrayAttribute>] args: obj []) = 
        let msg = String.Format(format, args)
        let msg = _prefixError + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceError, msg)
        VimTrace.Raise msg VimTraceKind.Error

    [<Conditional("DEBUG")>]
    static member TraceDebug(msg: string) = 
        let msg = _prefixDebug + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceVerbose, msg)
        VimTrace.Raise msg VimTraceKind.Debug

    [<Conditional("DEBUG")>]
    static member TraceDebug(format: string, [<ParamArrayAttribute>] args: obj []) = 
        let msg = String.Format(format, args)
        let msg = _prefixDebug + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceVerbose, msg)
        VimTrace.Raise msg VimTraceKind.Debug

    static member private Raise msg kind = 
        let args = VimTraceEventArgs(msg, kind)
        _traceEvent.Trigger _traceSwitch args

    /// Raised when a tracing event occurs.
    [<CLIEvent>]
    static member Trace = _traceEvent.Publish

