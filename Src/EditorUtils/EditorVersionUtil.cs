using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorUtils
{
    public static class EditorVersionUtil
    {
        public static IEnumerable<EditorVersion> All => Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>().OrderBy(x => GetMajorVersionNumber(x));

        public static EditorVersion MaxVersion => All.OrderByDescending(x => GetMajorVersionNumber(x)).First();

        public static EditorVersion GetEditorVersion(int majorVersion)
        {
            switch (majorVersion)
            {
                case 10: return EditorVersion.Vs2010;
                case 11: return EditorVersion.Vs2012;
                case 12: return EditorVersion.Vs2013;
                case 14: return EditorVersion.Vs2015;
                case 15: return EditorVersion.Vs2017;
                default: throw new Exception(string.Format("Unexpected major version value {0}", majorVersion));
            }
        }

        public static int GetMajorVersionNumber(EditorVersion version)
        {
            switch (version)
            {
                case EditorVersion.Vs2010: return 10;
                case EditorVersion.Vs2012: return 11;
                case EditorVersion.Vs2013: return 12;
                case EditorVersion.Vs2015: return 14;
                case EditorVersion.Vs2017: return 15;
                default: throw new Exception(string.Format("Unexpected enum value {0}", version));
            }
        }

        public static string GetShortVersionString(EditorVersion version)
        {
            var number = GetMajorVersionNumber(version);
            return string.Format("{0}.0", number);
        }
    }
}
