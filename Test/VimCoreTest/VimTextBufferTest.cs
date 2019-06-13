using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;
using System.Linq;
using Microsoft.FSharp.Core;

namespace Vim.UnitTest
{
    public abstract class VimTextBufferTest : VimTestBase
    {
        protected IVimTextBuffer _vimTextBuffer;
        protected ITextBuffer _textBuffer;
        protected IVimLocalSettings _localSettings;
        protected IVimGlobalSettings _globalSettings;
        protected LocalMark _localMarkA = LocalMark.NewLetter(Letter.A);

        protected virtual void Create(params string[] lines)
        {
            _vimTextBuffer = CreateVimTextBuffer(lines);
            _textBuffer = _vimTextBuffer.TextBuffer;
            _localSettings = _vimTextBuffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
        }

        public sealed class LastInsertExitPoint : VimTextBufferTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                var point = _textBuffer.GetPoint(1);
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(point);
                Assert.Equal(point, _vimTextBuffer.LastInsertExitPoint.Value);
            }

            /// <summary>
            /// The point should track edits
            /// </summary>
            [WpfFact]
            public void TracksEdits()
            {
                Create("cat", "dog");
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(_textBuffer.GetPoint(1));
                _textBuffer.Insert(0, "foo");
                Assert.Equal(1, _vimTextBuffer.LastInsertExitPoint.Value.Position);
            }

            /// <summary>
            /// A delete of the line that contains that LastInsertExitPoint should cause it to be 
            /// cleared
            /// </summary>
            [WpfFact]
            public void DeleteShouldClear()
            {
                Create("cat", "dog", "fish");
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(_textBuffer.GetPoint(1));
                _textBuffer.Delete(_textBuffer.GetLine(0).ExtentIncludingLineBreak.Span);
                Assert.True(_vimTextBuffer.LastInsertExitPoint.IsNone());
            }
        }

        public sealed class LocalMarkTest : VimTextBufferTest
        {
            /// <summary>
            /// Requesting a LocalMark which isn't set should produce an empty option
            /// </summary>
            [WpfFact]
            public void GetLocalMark_NotSet()
            {
                Create("");
                Assert.True(_vimTextBuffer.GetLocalMark(_localMarkA).IsNone());
            }

            /// <summary>
            /// Sanity check to ensure we can get and set a local mark 
            /// </summary>
            [WpfFact]
            public void SetLocalMark_FirstLine()
            {
                Create("hello world");
                Assert.True(_vimTextBuffer.SetLocalMark(_localMarkA, 0, 1));
                Assert.Equal(1, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
            }

            /// <summary>
            /// Sanity check to ensure we can get and set a local mark 
            /// </summary>
            [WpfFact]
            public void SetLocalMark_SecondLine()
            {
                Create("hello", "world");
                Assert.True(_vimTextBuffer.SetLocalMark(_localMarkA, 1, 1));
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(1).Position, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
            }

            /// <summary>
            /// Attempting to set a read only mark should return false and not update the mark
            /// </summary>
            [WpfFact]
            public void SetLocalMark_ReadOnlyMark()
            {
                Create("hello", "world");
                var visualSpan = VisualSpan.NewCharacter(new CharacterSpan(_textBuffer.GetPoint(0), 1, 2));
                _vimTextBuffer.LastVisualSelection = FSharpOption.Create(VisualSelection.CreateForward(visualSpan));
                Assert.False(_vimTextBuffer.SetLocalMark(LocalMark.LastSelectionStart, 0, 4));
                Assert.Equal(0, _vimTextBuffer.GetLocalMark(LocalMark.LastSelectionStart).Value.Position.Position);
            }

            [WpfFact]
            public void RemoveLocalMark_NotFound()
            {
                Create("dog");
                Assert.False(_vimTextBuffer.RemoveLocalMark(LocalMark.NewLetter(Letter.A)));
            }

            [WpfFact]
            public void RemoveLocalMark_Found()
            {
                Create("dog");
                _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.A), 0, 0);
                Assert.True(_vimTextBuffer.RemoveLocalMark(LocalMark.NewLetter(Letter.A)));
            }
        }

        public sealed class ClearTest : VimTextBufferTest
        {
            [WpfFact]
            public void ShouldRemoveLocalMarks()
            {
                Create("cat");
                var marks = Letter.All.Select(LocalMark.NewLetter);
                foreach (var mark in marks)
                {
                    _vimTextBuffer.SetLocalMark(mark, 0, 0);
                    Assert.True(_vimTextBuffer.GetLocalMark(mark).IsSome());
                }

                _vimTextBuffer.Clear();

                foreach (var mark in marks)
                {
                    Assert.False(_vimTextBuffer.GetLocalMark(mark).IsSome());
                }
            }

            [WpfFact]
            public void ShouldClearFields()
            {
                Create("cat");
                _vimTextBuffer.LastEditPoint = FSharpOption.Create(_textBuffer.GetPoint(0));
                _vimTextBuffer.LastInsertExitPoint = FSharpOption.Create(_textBuffer.GetPoint(0));
                _vimTextBuffer.Clear();
                Assert.True(_vimTextBuffer.LastEditPoint.IsNone());
                Assert.True(_vimTextBuffer.LastInsertExitPoint.IsNone());
            }
        }

        public sealed class ModeLineTest : VimTextBufferTest
        {
            private IVimWindowSettings _windowSettings;

            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _windowSettings = new WindowSettings(_globalSettings);
            }

            [WpfFact]
            public void FirstForm()
            {
                var modeLine = " vim:ts=8:";
                Create(modeLine);
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsNone());
                Assert.Equal(8, _localSettings.TabStop);
            }

            [WpfFact]
            public void SecondForm()
            {
                var modeLine = "/* vim: set ts=8 : */";
                Create(modeLine);
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsNone());
                Assert.Equal(8, _localSettings.TabStop);
            }

            [WpfFact]
            public void TrailingColonOptional()
            {
                var modeLine = " vim:ts=8";
                Create(modeLine);
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.Equal(8, _localSettings.TabStop);
            }

            [WpfFact]
            public void AllowWhitespace()
            {
                var modeLine = " vim: ts=8 :";
                Create(modeLine);
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.Equal(8, _localSettings.TabStop);
            }

            [WpfFact]
            public void AllowMultipleSettings()
            {
                var modeLine = " vim:ts=8 sw=8:";
                Create(modeLine);
                _localSettings.TabStop = 4;
                _localSettings.ShiftWidth = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.Equal(8, _localSettings.TabStop);
                Assert.Equal(8, _localSettings.ShiftWidth);
            }

            [WpfFact]
            public void AllowQuotedColon()
            {
                var modeLine = @" vim:comments=\:*,\://,\:#,\:;:ts=8:";
                Create(modeLine);
                _localSettings.TabStop = 4;
                _localSettings.Comments = "";
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.Equal(8, _localSettings.TabStop);
                Assert.Equal(":*,://,:#,:;", _localSettings.Comments);
            }

            [WpfFact]
            public void BackslashOnlyQuotesColon()
            {
                var modeLine = @" vim:comments=\\:\x:ts=8:";
                Create(modeLine);
                _localSettings.TabStop = 4;
                _localSettings.Comments = "";
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.Equal(8, _localSettings.TabStop);
                Assert.Equal(@"\:\x", _localSettings.Comments);
            }

            [WpfFact]
            public void IgnoreEmpty()
            {
                var modeLine = " vim::";
                Create(modeLine);
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsNone());
            }

            [WpfFact]
            public void IgnoreInvalid()
            {
                var modeLine = " vim:answer=42:";
                Create(modeLine);
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsNone());
            }

            [WpfFact]
            public void FailGlobal()
            {
                var modeLine = " vim:virtualedit=all:";
                Create(modeLine);
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsSome());
            }

            [WpfFact]
            public void FailMalformedSetting()
            {
                var modeLine = " vim:*foo*:";
                Create(modeLine);
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsSome());
            }

            [WpfFact]
            public void FailMalformedValue()
            {
                var modeLine = " vim:ts=invalid:";
                Create(modeLine);
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsSome());
            }

            [WpfFact]
            public void FailOnFirstError()
            {
                var modeLine = " vim:*foo*:ts=8:*bar*:";
                Create(modeLine);
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.True(result.Item2.IsSome());
                Assert.Equal("*foo*", result.Item2.Value);
                Assert.Equal(4, _localSettings.TabStop);
            }

            [WpfFact]
            public void ObeysModeLineSetting()
            {
                var modeLine = " vim:ts=8:";
                Create(modeLine);
                _globalSettings.ModeLine = false;
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.False(result.Item1.IsSome());
                Assert.Equal(4, _localSettings.TabStop);
            }

            [WpfFact]
            public void ObeysModeLinesSetting()
            {
                var modeLine = " vim:ts=8:";
                Create(modeLine);
                _globalSettings.ModeLines = 0;
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.False(result.Item1.IsSome());
                Assert.Equal(4, _localSettings.TabStop);
            }

            [WpfFact]
            public void MiddleOfLongFile()
            {
                var modeLine = " vim:ts=8:";
                Create(Enumerable.Repeat("", 100).Concat(new[] { modeLine }).Concat(Enumerable.Repeat("", 100)).ToArray());
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.False(result.Item1.IsSome());
                Assert.Equal(4, _localSettings.TabStop);
            }

            [WpfFact]
            public void EndOfLongFile()
            {
                var modeLine = " vim:ts=8:";
                Create(Enumerable.Repeat("", 100).Concat(new[] { modeLine }).ToArray());
                _localSettings.TabStop = 4;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.Equal(8, _localSettings.TabStop);
            }

            [WpfFact]
            public void HelpFile()
            {
                var modeLine = " vim:tw=78:ts=8:noet:ft=help:norl:";
                Create(modeLine);
                _localSettings.TextWidth = 132;
                _localSettings.TabStop = 4;
                _localSettings.ExpandTab = true;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.Equal(modeLine, result.Item1.Value);
                Assert.True(result.Item2.IsNone());
                Assert.Equal(78, _localSettings.TextWidth);
                Assert.Equal(8, _localSettings.TabStop);
                Assert.False(_localSettings.ExpandTab);
            }

            [WpfFact]
            public void WindowSetting()
            {
                var modeLine = " vim:nowrap:";
                Create(modeLine);
                _windowSettings.Wrap = true;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.False(_windowSettings.Wrap);
            }

            [WpfFact]
            public void SecondWindow()
            {
                var modeLine = " vim:ts=8:nowrap:";
                Create(modeLine);
                _localSettings.TabStop = 4;
                _windowSettings.Wrap = true;
                var result = _vimTextBuffer.CheckModeLine(_windowSettings);
                Assert.True(result.Item1.IsSome());
                Assert.Equal(8, _localSettings.TabStop);
                Assert.False(_windowSettings.Wrap);

                // Subsequent local setting changes in the first window.
                _localSettings.TabStop = 2;
                _windowSettings.Wrap = true;

                // Simulate second window.
                var otherWindowSettings = new WindowSettings(_globalSettings) as IVimWindowSettings;
                otherWindowSettings.Wrap = true;
                var otherResult = _vimTextBuffer.CheckModeLine(otherWindowSettings);
                Assert.True(otherResult.Item1.IsSome());
                Assert.Equal(2, _localSettings.TabStop); // shared local setting is not affected
                Assert.True(_windowSettings.Wrap); // first window's setting is not affected
                Assert.False(otherWindowSettings.Wrap); // second window's setting is set
            }
        }
    }
}
