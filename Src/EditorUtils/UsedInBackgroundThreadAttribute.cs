using System;

namespace EditorUtils
{
    /// <summary>
    /// Used to indicate a given operation occurs on background threads
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface)]
    public sealed class UsedInBackgroundThreadAttribute : Attribute
    {

    }
}
