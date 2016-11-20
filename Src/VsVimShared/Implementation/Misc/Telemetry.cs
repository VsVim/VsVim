using EnvDTE;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(ITelemetry))]
    internal sealed class Telemetry : ITelemetry
    {
        private readonly TelemetryClient _client;
        private readonly DTEEvents _dteEvents;

        [ImportingConstructor]
        internal Telemetry(SVsServiceProvider serviceProvider)
        {
            var dte = serviceProvider.GetService<SDTE, _DTE>();
            _client = CreateClient(dte);
            _dteEvents = dte.Events.DTEEvents;
            _dteEvents.OnBeginShutdown += OnBeginShutdown;
        }

        internal void WriteEvent(string eventName)
        {
            _client.TrackEvent(new EventTelemetry(eventName));
        }

        private static TelemetryClient CreateClient(_DTE dte)
        {
            var config = TelemetryConfiguration.CreateDefault();
            var client = new TelemetryClient(config);
            client.InstrumentationKey = "..";
            client.Context.User.Id = GetUserId();
            client.Context.Session.Id = Guid.NewGuid().ToString();
            client.Context.Properties.Add("Host", dte.Application.Edition);
            client.Context.Properties.Add("HostVersion", dte.Version);
            client.Context.Properties.Add("HostFullVersion", GetFullHostVersion());
            client.Context.Component.Version = VimConstants.VersionNumber;
            client.Context.Properties.Add("AppVersion", VimConstants.VersionNumber);
            client.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            return client;
        }

        private void OnBeginShutdown()
        {
            if (_client != null)
            {
                _client.Flush();
            }
        }

        private static string GetUserId()
        {
            var user = Environment.MachineName + "\\" + Environment.UserName;
            var bytes = Encoding.UTF8.GetBytes(user);
            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        private static string GetFullHostVersion()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var devenv = Path.Combine(baseDir, "msenv.dll");
                var version = FileVersionInfo.GetVersionInfo(devenv);
                return version.ProductVersion;
            }
            catch
            {
                // Ignore if we cannot get
                return "<error>";
            }
        }

        void ITelemetry.WriteEvent(string eventName)
        {
            WriteEvent(eventName);
        }
    }
}
