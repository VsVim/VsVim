
namespace EditorUtils
{
    internal static class Constants
    {
        /// <summary>
        /// The version of the assembly.  This must be changed every time a new version of the utility 
        /// library is published to NuGet
        /// </summary>
#if DEBUG
        internal const string AssemblyVersion = "99.0.0.0";
#else
        internal const string AssemblyVersion = "1.5.0.0";
#endif

        internal const string PublicKeyToken = "3d1514c4742e0252";

        /// <summary>
        /// Standard delay for asynchronous taggers
        /// </summary>
        internal const int DefaultAsyncDelay = 100;
    }
}
