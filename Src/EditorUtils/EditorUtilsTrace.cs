using System.Diagnostics;

namespace EditorUtils
{
    public static class EditorUtilsTrace
    {
        static readonly TraceSwitch s_traceSwitch = new TraceSwitch("EditorUtils", "EditorUtils Trace") { Level = TraceLevel.Off };

        public static TraceSwitch TraceSwitch
        {
            get { return s_traceSwitch; }
        }

        [Conditional("TRACE")]
        public static void TraceInfo(string msg)
        {
            Trace.WriteLineIf(s_traceSwitch.TraceInfo, "EditorUtils: " + msg);
        }

        [Conditional("TRACE")]
        public static void TraceInfo(string msg, params object[] args)
        {
            Trace.WriteLineIf(s_traceSwitch.TraceInfo, "EditorUtils: " + string.Format(msg, args));
        }
    }
}
