using System;

namespace VsVim
{
    /// <summary>
    /// These values must match up with those defined in VsVim.vsct
    /// </summary>
    internal static class GuidList
    {
        internal const string VsVimPackageString = "a284d12c-1e96-451b-a3b0-5486a1beb6ca";
        internal const string VsVimCommandSetString = "c7509f48-7d69-4344-8221-01989a8c4be5";

        internal static readonly Guid VsVimPackage = new Guid(VsVimPackageString);
        internal static readonly Guid VsVimCommandSet = new Guid(VsVimCommandSetString);
    };
}