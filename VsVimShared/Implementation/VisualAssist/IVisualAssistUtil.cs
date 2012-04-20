
using System;
namespace VsVim.Implementation.VisualAssist
{
    /// <summary>
    /// Interface for shared services related to Visual Assist
    /// </summary>
    internal interface IVisualAssistUtil
    {
        /// <summary>
        /// Is Visual Assist installed on Visual Studio
        /// </summary>
        bool IsInstalled { get; }

        /// <summary>
        /// Does the registry need to be updated to suppor VsVim in Visual Assist
        /// </summary>
        bool IsRegistryFixNeeed { get; }

        /// <summary>
        /// Raised when the registry check is completed
        /// </summary>
        event EventHandler RegistryFixCompleted;

        /// <summary>
        /// Fix the registry entry related to VsVim
        /// </summary>
        void FixRegistry();

        /// <summary>
        /// Ignore the registry entry related to VsVim
        /// </summary>
        void IgnoreRegistry();
    }
}
