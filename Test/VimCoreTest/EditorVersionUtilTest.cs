using Vim.EditorHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class EditorVersionUtilTest
    {
        [Fact]
        public void GetShortVersionStringAll()
        {
            foreach (var e in Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>())
            {
                var value = EditorVersionUtil.GetShortVersionString(e);
                Assert.NotNull(value);
            }
        }

        [Fact]
        public void GetVersionNumberAll()
        {
            Assert.Equal(11, EditorVersionUtil.GetMajorVersionNumber(EditorVersion.Vs2012));
            Assert.Equal(12, EditorVersionUtil.GetMajorVersionNumber(EditorVersion.Vs2013));
            Assert.Equal(14, EditorVersionUtil.GetMajorVersionNumber(EditorVersion.Vs2015));
            Assert.Equal(15, EditorVersionUtil.GetMajorVersionNumber(EditorVersion.Vs2017));
            Assert.Equal(16, EditorVersionUtil.GetMajorVersionNumber(EditorVersion.Vs2019));
            Assert.Equal(17, EditorVersionUtil.GetMajorVersionNumber(EditorVersion.Vs2022));
        }

        [Fact]
        public void MaxEditorVersionIsMax()
        {
            var max = EditorVersionUtil.GetMajorVersionNumber(EditorVersionUtil.MaxVersion);
            foreach (var e in Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>())
            {
                var number = EditorVersionUtil.GetMajorVersionNumber(e);
                Assert.True(number <= max);
            }
        }

        [Fact]
        public void Completeness()
        {
            foreach (var e in Enum.GetValues(typeof(EditorVersion)).Cast<EditorVersion>())
            {
                var majorVersion = EditorVersionUtil.GetMajorVersionNumber(e);
                var e2 = EditorVersionUtil.GetEditorVersion(majorVersion);
                Assert.Equal(e, e2);
            }
        }
    }
}
