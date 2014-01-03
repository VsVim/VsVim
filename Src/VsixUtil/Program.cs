using Microsoft.VisualStudio.ExtensionManager;
using Microsoft.VisualStudio.Settings;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace VsixUtil
{
    enum Version
    {
        Vs2010,
        Vs2012,
        Vs2013,
    }

    class Program
    {
        private static string GetVersionNumber(Version version)
        {
            switch (version)
            {
                case Version.Vs2010:
                    return "10";
                case Version.Vs2012:
                    return "11";
                case Version.Vs2013:
                    return "12";
                default:
                    throw new Exception("Bad Version");
            }
        }

        /// <summary>
        /// Load the Microsoft.VisualStudio.ExtensionManager.Implementation assembly for the specified version
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        private static Assembly LoadImplementationAssembly(Version version)
        {
            var format = "Microsoft.VisualStudio.ExtensionManager.Implementation, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            var strongName = string.Format(format, GetVersionNumber(version));
            return Assembly.Load(strongName);
        }

        private static Assembly LoadSettingsAssembly(Version version)
        {
            var format = "Microsoft.VisualStudio.Settings{0}, Version={1}.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            string suffix = "";
            switch (version)
            {
                case Version.Vs2010:
                    suffix = "";
                    break;
                case Version.Vs2012:
                    suffix = ".11.0";
                    break;
                case Version.Vs2013:
                    suffix = ".12.0";
                    break;
                default:
                    throw new Exception("Bad Version");
            }

            var strongName = string.Format(format, suffix, GetVersionNumber(version));
            return Assembly.Load(strongName);
        }

        private static string GetApplicationPath(Version version)
        {
            var path = string.Format("SOFTWARE\\Microsoft\\VisualStudio\\{0}.0\\Setup\\VS", GetVersionNumber(version));
            using (var registryKey = Registry.LocalMachine.OpenSubKey(path))
            {
                return (string)(registryKey.GetValue("EnvironmentPath"));
            }
        }

        private static dynamic CreateExtensionManager(Version version, string rootSuffix)
        {
            var settingsAssembly = LoadSettingsAssembly(version);
            var extensionAssembly = LoadImplementationAssembly(version);
            var applicationPath = GetApplicationPath(version);

            var externalSettingsManagerType = settingsAssembly.GetType("Microsoft.VisualStudio.Settings.ExternalSettingsManager");
            var settingsManager = externalSettingsManagerType
                .GetMethods()
                .Where(x => x.Name == "CreateForApplication")
                .Where(x =>
                    {
                        var parameters = x.GetParameters();
                        return
                            parameters.Length == 2 &&
                            parameters[0].ParameterType == typeof(string) &&
                            parameters[1].ParameterType == typeof(string);
                    })
                .FirstOrDefault()
                .Invoke(null, new[] { applicationPath, rootSuffix });

            var extensionManagerServiceType = extensionAssembly.GetType("Microsoft.VisualStudio.ExtensionManager.ExtensionManagerService");
            var constructor = extensionManagerServiceType
                .GetConstructors()
                .Where(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType.Name.Contains("SettingsManager"))
                .FirstOrDefault();

            var obj = constructor.Invoke(new[] { settingsManager });
            return obj;
        }

        static void Main(string[] args)
        {
            var extensionManager = CreateExtensionManager(Version.Vs2013, "");

            var testId = "VsVim.Microsoft.e214908b-0458-4ae2-a583-4310f29687c3";
            dynamic installedExtension = extensionManager.GetInstalledExtension(testId);
        }
    }
}
