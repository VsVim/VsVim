using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.EditorHost
{
    public static class EditorVersionUtil
    {
        public static IEnumerable<EditorVersion> All => Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>().OrderBy(x => GetMajorVersionNumber(x));

        public static EditorVersion MaxVersion => All.OrderByDescending(x => GetMajorVersionNumber(x)).First();

        public static EditorVersion GetEditorVersion(int majorVersion)
        {
            switch (majorVersion)
            {
                case 11: return EditorVersion.Vs2012;
                case 12: return EditorVersion.Vs2013;
                case 14: return EditorVersion.Vs2015;
                case 15: return EditorVersion.Vs2017;
                case 16: return EditorVersion.Vs2019;
                case 17: return EditorVersion.Vs2022;
                default: throw new Exception($"Unexpected major version value {majorVersion}");
            }
        }

        public static int GetMajorVersionNumber(EditorVersion version)
        {
            switch (version)
            {
                case EditorVersion.Vs2012: return 11;
                case EditorVersion.Vs2013: return 12;
                case EditorVersion.Vs2015: return 14;
                case EditorVersion.Vs2017: return 15;
                case EditorVersion.Vs2019: return 16;
                case EditorVersion.Vs2022: return 17;
                default: throw new Exception($"Unexpected enum value {version}");
            }
        }

        public static string GetShortVersionString(EditorVersion version)
        {
            var number = GetMajorVersionNumber(version);
            return $"{number}.0";
        }
    }
}
