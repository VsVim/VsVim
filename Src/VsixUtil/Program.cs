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

    static class Extensions
    {
        internal static LateBound AsLateBound(this object obj)
        {
            return new LateBound(obj);
        }
    }

    sealed class LateBound
    {
        private readonly object _value;
        private readonly Type _type;

        internal object Value
        {
            get { return _value; }
        }

        internal bool IsNull
        {
            get { return _value == null; }
        }

        internal LateBound(object value)
        {
            _value = value;
            _type = value != null ? value.GetType() : null;
        }

        internal LateBound GetProperty(string name)
        {
            return _type
                .GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_value, null)
                .AsLateBound();
        }

        internal T Cast<T>()
        {
            return (T)_value;
        }

        internal LateBound CallMethod(string name, params object[] arguments)
        {
            return CallMethodCore(_type, name, _value, arguments);
        }

        internal static LateBound CallStaticMethod(Type type, string name, params object[] arguments)
        {
            return CallMethodCore(type, name, null, arguments);
        }

        private static LateBound CallMethodCore(Type type, string name, object thisArgument, params object[] arguments)
        {
            try
            {
                var methodInfo = type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(x => x.Name != null && x.Name == name)
                    .Where(x => x.GetParameters().Length == arguments.Length)
                    .FirstOrDefault();

                if (methodInfo == null)
                {
                    var message = String.Format("Could not find method {0}::{1}", type.Name, name);
                    throw new Exception(message);
                }

                return methodInfo
                    .Invoke(thisArgument, arguments)
                    .AsLateBound();
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        public override string ToString()
        {
            return _value != null ? _value.ToString() : "<null>";
        }
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

        private static bool IsVersionInstalled(Version version)
        {
            try
            {
                return GetApplicationPath(version) != null;
            }
            catch
            {
                return false;
            }
        }

        private static Type GetExtensionManagerServiceType(Version version)
        {
            var assembly = LoadImplementationAssembly(version);
            return assembly.GetType("Microsoft.VisualStudio.ExtensionManager.ExtensionManagerService");
        }

        private static LateBound CreateExtensionManager(Version version, string rootSuffix)
        {
            var settingsAssembly = LoadSettingsAssembly(version);
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

            var extensionManagerServiceType = GetExtensionManagerServiceType(version);
            var obj = extensionManagerServiceType
                .GetConstructors()
                .Where(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType.Name.Contains("SettingsManager"))
                .FirstOrDefault()
                .Invoke(new[] { settingsManager });
            return obj.AsLateBound();
        }

        private static LateBound CreateInstallableExtension(string extensionPath, Version version)
        {
            var type = GetExtensionManagerServiceType(version);
            return LateBound.CallStaticMethod(type, "CreateInstallableExtension", extensionPath);
        }

        private static void InstallExtension(string extensionPath, string rootSuffix, Version version)
        {
            var extensionManager = CreateExtensionManager(version, rootSuffix);
            var installableExtension = CreateInstallableExtension(extensionPath, version);
            var identifier = installableExtension.GetProperty("Header").GetProperty("Identifier").Cast<string>();

            try
            {
                var installedExtension = extensionManager.CallMethod("GetInstalledExtension", identifier);
                try
                {
                    Console.Write("Uninstalling ... ");
                    extensionManager.CallMethod("Uninstall", installedExtension.Value);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            catch
            {
                // Extension isn't installed
            }

            try
            {
                Console.Write("Installing ... ");
                extensionManager.CallMethod("Install", installableExtension.Value, false);

                var installedExtension = extensionManager.CallMethod("GetInstalledExtension", identifier);
                extensionManager.CallMethod("Enable", installedExtension.Value);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("vsixutil extensionPath [rootSuffix]");
                return;
            }

            var extensionPath = args[0];
            var rootSuffix = args.Length == 2 ? args[1] : "";
            foreach (var version in Enum.GetValues(typeof(Version)).Cast<Version>().Where(x => IsVersionInstalled(x)))
            {
                Console.WriteLine("Visual Studio {0}.0", GetVersionNumber(version));
                InstallExtension(extensionPath, rootSuffix, version);
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("vsixutil extensionPath [rootSuffix]");
        }
    }
}
