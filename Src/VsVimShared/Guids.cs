using System;

namespace Vim.VisualStudio
{
    /// <summary>
    /// These values must match up with those defined in VsVim.vsct
    /// </summary>
    internal static class GuidList
    {
        internal const string VsVimPackageString = VsVimConstants.PackageGuidString;
        internal const string VsVimCommandSetString = "c7509f48-7d69-4344-8221-01989a8c4be5";

        internal static readonly Guid VsVimPackage = VsVimConstants.PackageGuid;
        internal static readonly Guid VsVimCommandSet = new Guid(VsVimCommandSetString);
    };
}