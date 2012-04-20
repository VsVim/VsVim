using System;

namespace VsVim
{
    internal static class Constants
    {
        /// <summary>
        /// Content Type name and display name for C++ projects
        /// </summary>
        internal const string CPlusPlusContentType = "C/C++";

        /// <summary>
        /// Content Type name and display name for C# projects
        /// </summary>
        internal const string CSharpContentType = "CSharp";

        /// <summary>
        /// Name of the main Key Processor
        /// </summary>
        internal const string VsKeyProcessorName = "VsVim";

        /// <summary>
        /// Name of the main Visual Studio KeyProcessor implementation
        /// </summary>
        internal const string VisualStudioKeyProcessorName = "VisualStudioKeyProcessor";

        internal static Guid VsUserDataFileNameMoniker = new Guid(0x978a8e17, 0x4df8, 0x432a, 150, 0x23, 0xd5, 0x30, 0xa2, 100, 0x52, 0xbc);
    }
}
