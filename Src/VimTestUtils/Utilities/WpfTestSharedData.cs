using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Xunit.Abstractions;

namespace Vim.UnitTest.Utilities
{
    [Serializable]
    public sealed class WpfTestSharedData
    {
        internal static readonly WpfTestSharedData Instance = new WpfTestSharedData();

        /// <summary>
        /// Holds the last 10 test cases executed: more recent test cases will occur later in the 
        /// list. Useful for debugging deadlocks that occur because state leak between runs. 
        /// </summary>
        private readonly List<string> _recentTestCases = new List<string>();

        /// <summary>
        /// The <see cref="Semaphore"/> used to ensure that only a single
        /// <see cref="WpfFactAttribute"/>-attributed test runs at once. This requirement must be made because,
        /// currently, <see cref="WpfTestCase"/>'s logic sets various static state before a method runs. If two tests
        /// run interleaved on the same scheduler (i.e. if one yields with an await) then all bets are off.
        /// </summary>
        public Semaphore TestSerializationGate = new Semaphore(initialCount: 1, maximumCount: 1);

        private WpfTestSharedData()
        {
        }

        public void ExecutingTest(ITestMethod testMethod)
        {
            var name = $"{testMethod.TestClass.Class.Name}::{testMethod.Method.Name}";
            lock (_recentTestCases)
            {
                _recentTestCases.Add(name);
            }
        }

        public void ExecutingTest(MethodInfo testMethod)
        {
            var name = $"{testMethod.DeclaringType.Name}::{testMethod.Name}";
            lock (_recentTestCases)
            {
                _recentTestCases.Add(name);
            }
        }
    }
}
