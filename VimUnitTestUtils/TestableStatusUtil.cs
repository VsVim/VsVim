using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UnitTest
{
    public sealed class TestableStatusUtil : IStatusUtil
    {
        public string LastError { get; set; }
        public string LastStatus { get; set; }
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
        }

        public void OnWarning(string value)
        {
            LastWarning = value;
        }
    }
}
