using System;
using System.Collections.Generic;
using System.Linq;

namespace Vim.UnitTest
{
    public sealed class TestableStatusUtil : IStatusUtil
    {
        public string LastError { get; set; }
        public string LastStatus { get; set; }
        public string[] LastStatusLong { get; set; }
        public string LastWarning { get; set; }

        public void OnError(string value)
        {
            LastError = value;
        }

        public void OnStatus(string value)
        {
            LastStatus = value;
        }

        public void OnStatusLong(IEnumerable<string> value)
        {
            LastStatus = value.Aggregate((x, y) => x + Environment.NewLine + y);
            LastStatusLong = value.ToArray();
        }

        public void OnWarning(string value)
        {
            LastWarning = value;
        }
    }
}
