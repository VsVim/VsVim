using System;
using System.Linq;
using Xunit;
using System.Collections.Generic;

namespace Vim.UnitTest
{
    public abstract class GlobalSettingsTest 
    {
        private readonly GlobalSettings _globalSettingsRaw;
        private readonly IVimGlobalSettings _globalSettings;

        protected GlobalSettingsTest()
        {
            _globalSettingsRaw = new GlobalSettings();
            _globalSettings = _globalSettingsRaw;
        }

        public sealed class CustomSettingsTest : GlobalSettingsTest
        {
            internal sealed class CustomSettingSource : IVimCustomSettingSource
            {
                internal readonly string Name;
                internal string DefaultValue;
                internal string Value;

                internal CustomSettingSource(string name, string defaultValue = "")
                {
                    Name = name;
                    DefaultValue = defaultValue;
                }

                SettingValue IVimCustomSettingSource.GetDefaultSettingValue(string name)
                {
                    Assert.Equal(name, Name);
                    return SettingValue.NewString(DefaultValue);
                }

                SettingValue IVimCustomSettingSource.GetSettingValue(string name)
                {
                    Assert.Equal(name, Name);
                    return SettingValue.NewString(Value);
                }

                void IVimCustomSettingSource.SetSettingValue(string name, SettingValue settingValue)
                {
                    Assert.Equal(name, Name);
                    if (settingValue.IsString)
                    {
                        Value = ((SettingValue.String)settingValue).Item;
                    }
                }
            }

            private string GetStringValue(string name)
            {
                var setting = _globalSettings.GetSetting(name).Value;
                return ((SettingValue.String)setting.LiveSettingValue.Value).Item;
            }

            [Fact]
            public void SimpleGet()
            {
                var source = new CustomSettingSource("test");
                source.Value = "foo";
                _globalSettings.AddCustomSetting(source.Name, source.Name, source);
                Assert.Equal("foo", GetStringValue(source.Name));
            }

            [Fact]
            public void SimpleSet()
            {
                var source = new CustomSettingSource("test");
                source.Value = "foo";
                _globalSettings.AddCustomSetting(source.Name, source.Name, source);
                _globalSettings.TrySetValueFromString(source.Name, "bar");
                Assert.Equal("bar", GetStringValue(source.Name));
                Assert.Equal("bar", source.Value);
            }
        }

        public sealed class PathTest : GlobalSettingsTest
        {
            public void Expect(string text, params PathOption[] expected)
            {
                var list = _globalSettingsRaw.GetPathOptionList(text);
                Expect(list, expected);
            }

            public void Expect(IEnumerable<PathOption> value, params PathOption[] expected)
            {
                Assert.Equal(expected, value);
            }

            [Fact]
            public void PathDefault()
            {
                Expect(_globalSettings.PathList, PathOption.CurrentFile, PathOption.CurrentDirectory);
            }

            [Fact]
            public void Simple()
            {
                Expect("foo", PathOption.NewNamed("foo"));
            }

            [Fact]
            public void SimpleList()
            {
                Expect("foo,bar", PathOption.NewNamed("foo"), PathOption.NewNamed("bar"));
            }

            [Fact]
            public void SimpleListWithSpace()
            {
                Expect("foo bar", PathOption.NewNamed("foo"), PathOption.NewNamed("bar"));
            }

            [Fact]
            public void CurrentDirectoryOnly()
            {
                Expect(",,", PathOption.CurrentDirectory);
            }

            [Fact]
            public void CurrentFileOnly()
            {
                Expect(".", PathOption.CurrentFile);
            }

            [Fact]
            public void EscapedCommaInPath()
            {
                Expect(@"cat\,dog", PathOption.NewNamed(@"cat,dog"));
            }

            [Fact]
            public void EscapedSpaceInPath()
            {
                Expect(@"cat\ dog", PathOption.NewNamed(@"cat dog"));
            }
        }

        public sealed class BackspaceTest : GlobalSettingsTest
        {
            [Fact]
            public void Value0()
            {
                _globalSettings.Backspace = "start";
                _globalSettings.Backspace = "0";
                Assert.False(_globalSettings.IsBackspaceStart);
            }

            [Fact]
            public void Value0FromString()
            {
                _globalSettings.TrySetValueFromString("backspace", "0");
                Assert.False(_globalSettings.IsBackspaceStart);
            }

            [Fact]
            public void Value1()
            {
                _globalSettings.Backspace = "1";
                Assert.True(_globalSettings.IsBackspaceIndent && _globalSettings.IsBackspaceEol);
            }

            [Fact]
            public void Value1FromString()
            {
                _globalSettings.TrySetValueFromString("backspace", "1");
                Assert.True(_globalSettings.IsBackspaceIndent && _globalSettings.IsBackspaceEol);
            }

            [Fact]
            public void Value2()
            {
                _globalSettings.Backspace = "2";
                Assert.True(_globalSettings.IsBackspaceIndent && _globalSettings.IsBackspaceEol && _globalSettings.IsBackspaceStart);
            }

            [Fact]
            public void Value2FromString()
            {
                _globalSettings.TrySetValueFromString("backspace", "2");
                Assert.True(_globalSettings.IsBackspaceIndent && _globalSettings.IsBackspaceEol && _globalSettings.IsBackspaceStart);
            }

            [Fact]
            public void StartIndent()
            {
                _globalSettings.Backspace = "start,indent";
                Assert.True(_globalSettings.IsBackspaceIndent && !_globalSettings.IsBackspaceEol && _globalSettings.IsBackspaceStart);
            }

            [Fact]
            public void StartIndentEol()
            {
                _globalSettings.Backspace = "start,indent,eol";
                Assert.True(_globalSettings.IsBackspaceIndent && _globalSettings.IsBackspaceEol && _globalSettings.IsBackspaceStart);
            }
        }

        public sealed class MiscTest : GlobalSettingsTest
        {
            [Fact]
            public void Sanity1()
            {
                var all = _globalSettings.AllSettings;
                Assert.True(all.Any(x => x.Name == GlobalSettingNames.IgnoreCaseName));
                Assert.True(all.Any(x => x.Name == GlobalSettingNames.ScrollOffsetName));
            }

            [Fact]
            public void IsVirtualEditOneMore1()
            {
                _globalSettings.VirtualEdit = String.Empty;
                Assert.False(_globalSettings.IsVirtualEditOneMore);
            }

            [Fact]
            public void IsVirtualEditOneMore2()
            {
                _globalSettings.VirtualEdit = "onemore";
                Assert.True(_globalSettings.IsVirtualEditOneMore);
            }

            [Fact]
            public void IsVirtualEditOneMore3()
            {
                _globalSettings.VirtualEdit = "onemore,blah";
                Assert.True(_globalSettings.IsVirtualEditOneMore);
            }

            /// <summary>
            /// Ensure the IsBackspaceStart properly parsers start from the option
            /// </summary>
            [Fact]
            public void IsBackspaceStart()
            {
                Assert.False(_globalSettings.IsBackspaceStart);
                _globalSettings.Backspace = "eol,start";
                Assert.True(_globalSettings.IsBackspaceStart);
            }

            /// <summary>
            /// Ensure the IsBackspaceEol properly parsers eol from the option
            /// </summary>
            [Fact]
            public void IsBackspaceEol()
            {
                Assert.False(_globalSettings.IsBackspaceEol);
                _globalSettings.Backspace = "eol,Eol";
                Assert.True(_globalSettings.IsBackspaceEol);
            }

            /// <summary>
            /// Ensure the IsBackspaceIndent properly parsers start from the option
            /// </summary>
            [Fact]
            public void IsBackspaceIndent()
            {
                Assert.False(_globalSettings.IsBackspaceIndent);
                _globalSettings.Backspace = "indent,start";
                Assert.True(_globalSettings.IsBackspaceIndent);
            }

            /// <summary>
            /// Setting a setting should raise the event even if the values are the same.  This is 
            /// depended on by the :noh feature
            /// </summary>
            [Fact]
            public void SetShouldRaise()
            {
                var seen = false;
                _globalSettings.HighlightSearch = true;
                _globalSettings.SettingChanged += delegate { seen = true; };
                _globalSettings.HighlightSearch = true;
                Assert.True(seen);
            }

            [Fact]
            public void Clipboard_SetUnnamed()
            {
                _globalSettings.Clipboard = "unnamed";
                Assert.Equal(ClipboardOptions.Unnamed, _globalSettings.ClipboardOptions);
                Assert.Equal("unnamed", _globalSettings.Clipboard);
            }

            [Fact]
            public void Clipboard_Multiple()
            {
                _globalSettings.Clipboard = "unnamed,autoselect";
                Assert.Equal(ClipboardOptions.Unnamed | ClipboardOptions.AutoSelect, _globalSettings.ClipboardOptions);
                Assert.Equal("unnamed,autoselect", _globalSettings.Clipboard);
            }

            /// <summary>
            /// Make sure the get / set logic for parsing out the options is complete
            /// </summary>
            [Fact]
            public void SelectModeOptions_Simple()
            {
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard | SelectModeOptions.Mouse;
                Assert.Equal("mouse,key", _globalSettings.SelectMode);
                _globalSettings.SelectModeOptions = SelectModeOptions.Keyboard;
                Assert.Equal("key", _globalSettings.SelectMode);
            }

            [Fact]
            public void SelectModeOptions_Default()
            {
                Assert.Equal("", _globalSettings.SelectMode);
                Assert.Equal(SelectModeOptions.None, _globalSettings.SelectModeOptions);
                var setting = _globalSettings.GetSetting(GlobalSettingNames.SelectModeName).Value;
                Assert.Equal("", setting.DefaultValue.AsString().Item);
            }
        }
    }
}
