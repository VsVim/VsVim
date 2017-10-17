using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using System.Reflection;
using System.ComponentModel.Composition;
using Vim;

namespace Vim.UnitTest
{
    public sealed class VersioningTest
    {
        /// <summary>
        /// Get all type defined in the system
        /// </summary>
        private List<Type> GetAllTypes()
        {
            var list = new List<Type>();
            var seen = new HashSet<Type>();
            var toVisit = new Stack<Type>(typeof(TaggerUtil).Assembly.GetTypes());
            while (toVisit.Count > 0)
            {
                var current = toVisit.Pop();
                if (!seen.Add(current))
                {
                    continue;
                }

                list.Add(current);
                foreach (var cur in current.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    toVisit.Push(cur);
                }
            }

            return list;
        }
    }
}
