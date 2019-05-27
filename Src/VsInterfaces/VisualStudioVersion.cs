
using System;
using Vim.VisualStudio.Specific;

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
        public static string GetHostIdentifier(VisualStudioVersion version)
        {
            switch (version)
            {
                case VisualStudioVersion.Vs2012: return HostIdentifiers.VisualStudio2012;
                case VisualStudioVersion.Vs2013: return HostIdentifiers.VisualStudio2013;
                case VisualStudioVersion.Vs2015: return HostIdentifiers.VisualStudio2015;
                case VisualStudioVersion.Vs2017: return HostIdentifiers.VisualStudio2017;
                case VisualStudioVersion.Vs2019: return HostIdentifiers.VisualStudio2019;
                default: return Guid.NewGuid().ToString();
            }
        }
    }
}
