
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
    }
}
