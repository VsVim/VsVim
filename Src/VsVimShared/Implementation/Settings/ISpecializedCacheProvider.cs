using System.Collections.Generic;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Interface to abstract "Type specialization" code
    /// Since in .net the only way to do it now is to use static generics
    /// Which is not test-friendly if we embed it directly into the class
    /// This allows to have cleaner code without repeating methods for each type
    /// While also providing fast access as it doesn't involve reflection
    /// </summary>
    public interface ISpecializedCacheProvider
    {
        IDictionary<string, T> Get<T>();
    }
}