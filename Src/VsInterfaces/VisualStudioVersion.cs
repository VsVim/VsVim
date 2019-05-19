
using System;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Known Visual Studio versions
    /// </summary>
    public enum VisualStudioVersion
    {
        Vs2012,
        Vs2013,
        Vs2015,
        Vs2017,
        Vs2019,
        Unknown
    }

    public static class VisualStudioVersionUtil
    {
        public const string HostIdentifier2012 = "VsVim 2012";
        public const string HostIdentifier2013 = "VsVim 2013";
        public const string HostIdentifier2015 = "VsVim 2015";
        public const string HostIdentifier2017 = "VsVim 2017";
        public const string HostIdentifier2019 = "VsVim 2019";

        public static string GetHostIdentifier(VisualStudioVersion version)
        {
            switch (version)
            {
                case VisualStudioVersion.Vs2012: return HostIdentifier2012;
                case VisualStudioVersion.Vs2013: return HostIdentifier2013;
                case VisualStudioVersion.Vs2015: return HostIdentifier2015;
                case VisualStudioVersion.Vs2017: return HostIdentifier2017;
                case VisualStudioVersion.Vs2019: return HostIdentifier2019;
                default: return Guid.NewGuid().ToString();
            }
        }
    }
}
