using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Vim.UnitTest.Utilities
{
    [Serializable]
    public sealed class WpfTestSharedData
    {
        public static readonly WpfTestSharedData Instance = new WpfTestSharedData();

        /// <summary>
        /// Holds the last 10 test cases executed: more recent test cases will occur later in the 
        /// list. Useful for debugging deadlocks that occur because state leak between runs. 
        /// </summary>
        private readonly List<string> _recentTestCases = new List<string>();

        public void ExecutingTest(ITestMethod testMethod)
        {
            var name = $"{testMethod.TestClass.Class.Name}::{testMethod.Method.Name}";
            lock (_recentTestCases)
            {
                _recentTestCases.Add(name);
            }
        }
    }
}
