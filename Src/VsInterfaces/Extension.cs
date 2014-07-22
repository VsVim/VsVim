using EnvDTE;

namespace Vim.VisualStudio
{
    public static class Extension
    {
        public static VisualStudioVersion GetVisualStudioVersion(this _DTE dte)
        {
            var version = dte.Version;
            if (string.IsNullOrEmpty(dte.Version))
            {
                return VisualStudioVersion.Unknown;
            }

            var parts = version.Split('.');
            if (parts.Length == 0)
            {
                return VisualStudioVersion.Unknown;
            }

            switch (parts[0])
            {
                case "10":
                    return VisualStudioVersion.Vs2010;
                case "11":
                    return VisualStudioVersion.Vs2012;
                case "12":
                    return VisualStudioVersion.Vs2013;
                default:
                    return VisualStudioVersion.Unknown;
            }
        }
    }
}
