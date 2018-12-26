using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Type specialization encapsulating class
    /// Hard dependencies on static classes are not test-friendly
    /// So as a workaround we use a class to encapsulate it
    /// </summary>
    [Export(typeof(ISpecializedCacheProvider))]
    public class StaticMapSpecializationProvider : ISpecializedCacheProvider
    {
        [ImportingConstructor]
        public StaticMapSpecializationProvider()
        {
        }

        public IDictionary<string, T> Get<T>()
        {
            return TypeBoundPropertyMap<T>.Cache;
        }

        /// <summary>
        /// Enables Type specialization for cache map
        /// </summary>
        private static class TypeBoundPropertyMap<T>
        {
            internal static IDictionary<string, T> Cache { get; }

            static TypeBoundPropertyMap()
            {
                Cache = new Dictionary<string, T>();
            }
        }
    }
}