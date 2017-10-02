using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using System.Reflection;
using System.ComponentModel.Composition;

namespace EditorUtils.UnitTest
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
            var toVisit = new Stack<Type>(typeof(EditorUtilsFactory).Assembly.GetTypes());
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

        /// <summary>
        /// Make sure that there are no Export values in the system.  EditorUtils does not use MEF to 
        /// provide parts to consumers
        /// </summary>
        [Fact]
        public void EnsureNoExports()
        {
            var assembly = typeof(EditorUtilsFactory).Assembly;
            foreach (var cur in GetAllTypes())
            {
                var all = cur
                    .GetCustomAttributes(typeof(ExportAttribute), false)
                    .Cast<ExportAttribute>();
                Assert.Equal(0, all.Count());
            }
        }

        /// <summary>
        /// Make sure there are no Import values in the system.  EditorUtils does not use MEF hence 
        /// there can be no [ImportingConstructors]
        /// </summary>
        [Fact]
        public void EnsureNoImportingConstructors()
        {
            var assembly = typeof(EditorUtilsFactory).Assembly;
            foreach (var cur in GetAllTypes())
            {
                foreach (var constructorInfo in cur.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var all = constructorInfo.GetCustomAttributes(typeof(ImportingConstructorAttribute), false);
                    Assert.Equal(0, all.Count());
                }
            }
        }
    }
}
