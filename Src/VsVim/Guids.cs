// Guids.cs
// MUST match guids.h
using System;

namespace VsVim
{
    static class GuidList
    {
        public const string guidVsVimPkgString = "a284d12c-1e96-451b-a3b0-5486a1beb6ca";
        public const string guidVsVimCmdSetString = "c7509f48-7d69-4344-8221-01989a8c4be5";

        public static readonly Guid guidVsVimCmdSet = new Guid(guidVsVimCmdSetString);
    };
}