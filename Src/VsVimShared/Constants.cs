using System;

namespace VsVim
{
    // TODO: Rename to VsVimConstants to avoid ambiguity issues
    public static class Constants
    {
        /// <summary>
        /// Content Type name and display name for C++ projects
        /// </summary>
        public const string CPlusPlusContentType = "C/C++";

        /// <summary>
        /// Content Type name and display name for C# projects
        /// </summary>
        public const string CSharpContentType = "CSharp";

        /// <summary>
        /// Name of the main Key Processor
        /// </summary>
        public const string VsKeyProcessorName = "VsVim";

        /// <summary>
        /// Name of the fallback Key Processor
        /// </summary>
        public const string FallbackKeyProcessorName = "VsVimFallback";

        /// <summary>
        /// Name of the standard ICommandTarget implementation
        /// </summary>
        public const string StandardCommandTargetName = "Standard Command Target";

        /// <summary>
        /// Name of the main Visual Studio KeyProcessor implementation
        /// </summary>
        public const string VisualStudioKeyProcessorName = "VisualStudioKeyProcessor";

        /// <summary>
        /// This text view role was added in VS 2013.  Adding the constant here so we can refer to 
        /// it within our code as we compile against the VS 2010 binaries
        /// </summary>
        public const string TextViewRoleEmbeddedPeekTextView = "EMBEDDED_PEEK_TEXT_VIEW";

        public const string ToastMarginFormatDefinitionName = "vsvim_margin";

        /// <summary>
        /// The GUID of our package 
        /// </summary>
        public const string PackageGuidString = "a284d12c-1e96-451b-a3b0-5486a1beb6ca";

        public static readonly Guid PackageGuid = new Guid("a284d12c-1e96-451b-a3b0-5486a1beb6ca");

        public static readonly Guid VsUserDataFileNameMoniker = new Guid(0x978a8e17, 0x4df8, 0x432a, 150, 0x23, 0xd5, 0x30, 0xa2, 100, 0x52, 0xbc);
    }
}
