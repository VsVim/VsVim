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

        public event EventHandler<StringEventArgs> ErrorRaised;
        public event EventHandler<StringEventArgs> WarningRaised;
        public event EventHandler<StringEventArgs> StatusUpdated;

        public void OnError(string value)
        {
            LastError = value;

            OnErrorRaised(LastError);
        }

        public void OnStatus(string value)
        {
            LastStatus = value;

            OnStatusUpdated(LastStatus);
        }

        public void OnStatusLong(IEnumerable<string> value)
        {
            LastStatus = value.Any() ? value.Aggregate((x, y) => x + Environment.NewLine + y) : "";
            LastStatusLong = value.ToArray();

            OnStatusUpdated(LastStatus);
        }

        public void OnWarning(string value)
        {
            LastWarning = value;

            OnWarningRaised(LastWarning);
        }

        private void OnErrorRaised(string error)
        {
            ErrorRaised?.Invoke(this, new StringEventArgs(error));
        }

        private void OnWarningRaised(string warning)
        {
            WarningRaised?.Invoke(this, new StringEventArgs(warning));
        }

        private void OnStatusUpdated(string status)
        {
            StatusUpdated?.Invoke(this, new StringEventArgs(status));
        }
    }
}
