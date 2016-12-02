using System;
using System.Linq;
using System.Threading;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;
using Xunit.Extensions;

namespace Vim.UnitTest
{
    public abstract class VisualModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private IVimBufferData _vimBufferData;
        private IVimTextBuffer _vimTextBuffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IRegisterMap _registerMap;
        private IVimGlobalSettings _globalSettings;
        private TestableSynchronizationContext _context;

        internal Register TestRegister
        {
            get { return _vimBuffer.RegisterMap.GetRegister('c'); }
        }

        protected virtual void Create(params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _vimBufferData = _vimBuffer.VimBufferData;
            _vimTextBuffer = _vimBuffer.VimTextBuffer;
            _registerMap = _vimBuffer.RegisterMap;
            _globalSettings = _vimBuffer.LocalSettings.GlobalSettings;
            Assert.True(_context.IsEmpty);

            // Need to make sure it's focused so macro recording will work
            ((MockVimHost)_vimBuffer.Vim.VimHost).FocusedTextView = _textView;
        }

        protected virtual void Create(int tabStop, params string[] lines)
        {
            Create();
            UpdateTabStop(_vimBuffer, tabStop);
            _textView.SetText(lines);
            _textView.MoveCaretTo(0);
        }

        protected void EnterMode(SnapshotSpan span)
        {
            var characterSpan = new CharacterSpan(span);
            var visualSelection = VisualSelection.NewCharacter(characterSpan, SearchPath.Forward);
            visualSelection.SelectAndMoveCaret(_textView);
            Assert.False(_context.IsEmpty);
            _context.RunAll();
            Assert.True(_context.IsEmpty);
        }

        protected void EnterMode(ModeKind kind, SnapshotSpan span)
        {
            EnterMode(span);
            _vimBuffer.SwitchMode(kind, ModeArgument.None);
        }

        /// <summary>
        /// Switches mode, then sets the visual selection. The order is reversed from EnterMode(ModeKind, SnapshotSpan).
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="span"></param>
        protected void SwitchEnterMode(ModeKind kind, SnapshotSpan span)
        {
            _vimBuffer.SwitchMode(kind, ModeArgument.None);
            var characterSpan = new CharacterSpan(span);
            var visualSelection = VisualSelection.NewCharacter(characterSpan, SearchPath.Forward);
            visualSelection.SelectAndMoveCaret(_textView);
            // skipping check: context.IsEmpty == false
            _context.RunAll();
            Assert.True(_context.IsEmpty);
        }

        protected void EnterBlock(BlockSpan blockSpan)
        {
            var visualSpan = VisualSpan.NewBlock(blockSpan);
            var visualSelection = VisualSelection.CreateForward(visualSpan);
            visualSelection.SelectAndMoveCaret(_textView);
            Assert.False(_context.IsEmpty);
            _context.RunAll();
            Assert.True(_context.IsEmpty);
            _vimBuffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.None);
        }

        /// <summary>
        /// Standard block selection tests
        /// </summary>
        public abstract class BlockSelectionTest : VisualModeIntegrationTest
        {
            private int _tabStop;

            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _tabStop = _vimBuffer.LocalSettings.TabStop;
            }

            public sealed class TabTest : BlockSelectionTest
            {
                protected override void Create(params string[] lines)
                {
                    base.Create();
                    UpdateTabStop(_vimBuffer, 4);
                    _vimBuffer.TextView.SetText(lines);
                    _textView.MoveCaretTo(0);
                }

                [Fact]
                public void CaretInTab()
                {
                    Create("cat", "\tdog");
                    _vimBuffer.ProcessNotation("<C-Q>j");
                    Assert.Equal(
                        new[]
                        {
                            _textBuffer.GetLineSpan(0, 3),
                            _textBuffer.GetLineSpan(1, 1)
                        },
                        _textView.Selection.SelectedSpans);
                }

                [Fact]
                public void CaretInTabAnchorNonZero()
                {
                    Create("cat", "\tdog");
                    _vimBuffer.ProcessNotation("ll<C-Q>j");

                    Assert.Equal(
                        new[]
                        {
                            _textBuffer.GetLineSpan(0, 3),
                            _textBuffer.GetLineSpan(1, 1)
                        },
                        _textView.Selection.SelectedSpans);
                }

                /// <summary>
                /// The caret is past the tab.  Hence the selection for the first line should
                /// be correct.
                /// </summary>
                [Fact]
                public void CaretPastTab()
                {
                    Create("kitty", "\tdog");
                    _vimBuffer.ProcessNotation("ll<C-Q>jl");

                    // In a strict vim interpretation both '\t' and 'd' would be selected in the 
                    // second line.  The Visual Studio editor won't have this selection and instead
                    // will not select the tab since it's only partially selected.  Hence only the
                    // 'd' will end up selected
                    Assert.Equal(
                        new[]
                        {
                            _textBuffer.GetLineSpan(0, 2, 3),
                            _textBuffer.GetLineSpan(1, 1, 1)
                        },
                        _textView.Selection.SelectedSpans);
                }

                /// <summary>
                /// This is an anti fact
                /// 
                /// The WPF editor can't place the caret in the middle of a tab.  It can't
                /// for example put it on the 2 of the 4th space a tab occupies.  
                /// </summary>
                [Fact]
                public void MiddleOfTab()
                {
                    Create("cat", "d\tog");
                    _vimBuffer.LocalSettings.TabStop = 4;
                    _vimBuffer.ProcessNotation("ll<C-q>jl");
                    var textView = _vimBuffer.TextView;
                    Assert.Equal('t', textView.Selection.Start.Position.GetChar());
                    Assert.Equal('g', textView.Selection.End.Position.GetChar());
                }
            }

            public sealed class MiscTest : BlockSelectionTest
            {
                /// <summary>
                /// Make sure the CTRL-Q command causes the block selection to start out as a single width
                /// column
                /// </summary>
                [Fact]
                public void InitialState()
                {
                    Create("hello world");
                    _vimBuffer.ProcessNotation("<C-Q>");
                    Assert.Equal(ModeKind.VisualBlock, _vimBuffer.ModeKind);
                    var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), tabStop: _tabStop, spaces: 1, height: 1);
                    Assert.Equal(blockSpan, _vimBuffer.GetSelectionBlockSpan());
                }

                /// <summary>
                /// Make sure the CTRL-Q command causes the block selection to start out as a single width 
                /// column from places other than the start of the document
                /// </summary>
                [Fact]
                public void InitialNonStartPoint()
                {
                    Create("big cats", "big dogs", "big trees");
                    var point = _textBuffer.GetPointInLine(1, 3);
                    _textView.MoveCaretTo(point);
                    _vimBuffer.ProcessNotation("<C-Q>");
                    Assert.Equal(ModeKind.VisualBlock, _vimBuffer.ModeKind);
                    var blockSpan = new BlockSpan(point, tabStop: _tabStop, spaces: 1, height: 1);
                    Assert.Equal(blockSpan, _vimBuffer.GetSelectionBlockSpan());
                }

                /// <summary>
                /// A left movement in block selection should move the selection to the left
                /// </summary>
                [Fact]
                public void Backwards()
                {
                    Create("big cats", "big dogs");
                    _textView.MoveCaretTo(2);
                    _vimBuffer.ProcessNotation("<C-Q>jh");
                    Assert.Equal(ModeKind.VisualBlock, _vimBuffer.ModeKind);
                    var blockSpan = new BlockSpan(_textView.GetPoint(1), tabStop: _tabStop, spaces: 2, height: 2);
                    Assert.Equal(blockSpan, _vimBuffer.GetSelectionBlockSpan());
                }
            }

            public sealed class ExclusiveTest : BlockSelectionTest
            {
                /// <summary>
                /// When selection is exclusive there should still be a single column selected in block
                /// mode even if the original width is 1
                /// </summary>
                [Fact]
                public void OneWidthBlock()
                {
                    Create("the dog", "the cat");
                    _textView.MoveCaretTo(1);
                    _globalSettings.Selection = "exclusive";
                    _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
                    _vimBuffer.Process('j');
                    var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2, tabStop: _tabStop);
                    Assert.Equal(blockSpan, _vimBuffer.GetSelectionBlockSpan());
                    Assert.Equal(_textView.GetPointInLine(1, 1), _textView.GetCaretPoint());
                }

                /// <summary>
                /// When selection is exclusive block selection should shrink by one in width
                /// </summary>
                [Fact]
                public void TwoWidthBlock()
                {
                    Create("the dog", "the cat");
                    _textView.MoveCaretTo(1);
                    _globalSettings.Selection = "exclusive";
                    _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
                    _vimBuffer.Process("jl");
                    var blockSpan = _textBuffer.GetBlockSpan(1, 1, 0, 2, tabStop: _tabStop);
                    Assert.Equal(blockSpan, _vimBuffer.GetSelectionBlockSpan());
                    Assert.Equal(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
                }
            }
        }

        public sealed class ChangeLineSelectionTest : VisualModeIntegrationTest
        {
            /// <summary>
            /// Even a visual character change is still a linewise delete
            /// </summary>
            [Fact]
            public void CharacterIsLineWise()
            {
                Create("cat", "dog");
                _vimBuffer.Process("vC");
                Assert.Equal("cat" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(new[] { "", "dog" }, _textBuffer.GetLines());
            }

            [Fact]
            public void LineIsLineWise()
            {
                Create("cat", "dog");
                _vimBuffer.Process("VC");
                Assert.Equal("cat" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(new[] { "", "dog" }, _textBuffer.GetLines());
            }
        }

        public sealed class DeleteLineSelectionTest : VisualModeIntegrationTest
        {
            /// <summary>
            /// Even a visual character change is still a linewise delete
            /// </summary>
            [Fact]
            public void CharacterIsLineWise()
            {
                Create("cat", "dog");
                _vimBuffer.Process("vD");
                Assert.Equal("cat" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(new[] { "dog" }, _textBuffer.GetLines());
            }

            [Fact]
            public void LineIsLineWise()
            {
                Create("cat", "dog");
                _vimBuffer.Process("VD");
                Assert.Equal("cat" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(new[] { "dog" }, _textBuffer.GetLines());
            }
        }

        public abstract class DeleteSelectionTest : VisualModeIntegrationTest
        {
            public sealed class CharacterTest : DeleteSelectionTest
            {
                /// <summary>
                /// When an entire line is selected in character wise mode and then deleted
                /// it should not be a line delete but instead delete the contents of the 
                /// line.
                /// </summary>
                [Fact]
                public void LineContents()
                {
                    Create("cat", "dog");
                    EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 3));
                    _vimBuffer.Process("x");
                    Assert.Equal("", _textView.GetLine(0).GetText());
                    Assert.Equal("dog", _textView.GetLine(1).GetText());
                }

                /// <summary>
                /// If the character wise selection extents into the line break then the 
                /// entire line should be deleted
                /// </summary>
                [Fact]
                public void LineContentsFromBreak()
                {
                    Create("cat", "dog");
                    _globalSettings.VirtualEdit = "onemore";
                    EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).ExtentIncludingLineBreak);
                    _vimBuffer.Process("x");
                    Assert.Equal("dog", _textView.GetLine(0).GetText());
                }

                [Fact]
                public void Issue1507()
                {
                    Create("cat", "dog", "fish");
                    _textView.MoveCaretTo(1);
                    _vimBuffer.Process("vjllx");
                    Assert.Equal(new[] { "cfish" }, _textBuffer.GetLines());
                }
            }

            public sealed class BlockTest : DeleteSelectionTest
            {
                [Fact]
                public void Simple()
                {
                    Create(4, "cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("<C-q>jjx");
                    Assert.Equal(new[]
                        {
                            "at",
                            "og",
                            "ish"
                        },
                        _textBuffer.GetLines());
                }

                [Fact]
                public void PartialTab()
                {
                    Create(4, "cat", "\tdog", "fish");
                    _vimBuffer.ProcessNotation("<C-q>jjx");
                    Assert.Equal(new[]
                        {
                            "at",
                            "   dog",
                            "ish"
                        },
                        _textBuffer.GetLines());
                }
            }

            public sealed class MiscTest : DeleteSelectionTest
            {
                /// <summary>
                /// The 'e' motion should result in a selection that encompasses the entire word
                /// </summary>
                [Fact]
                public void EndOfWord()
                {
                    Create("the dog. cat");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("vex");
                    Assert.Equal("dog", UnnamedRegister.StringValue);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// The 'e' motion should result in a selection that encompasses the entire word
                /// </summary>
                [Fact]
                public void EndOfWord_Block()
                {
                    Create("the dog. end", "the cat. end", "the fish. end");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process(KeyInputUtil.CharWithControlToKeyInput('q'));
                    _vimBuffer.Process("jex");
                    Assert.Equal("the . end", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("the . end", _textBuffer.GetLine(1).GetText());
                    Assert.Equal("the fish. end", _textBuffer.GetLine(2).GetText());
                }

                /// <summary>
                /// The 'w' motion should result in a selection that encompasses the entire word
                /// </summary>
                [Fact]
                public void Word()
                {
                    Create("the dog. cat");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("vwx");
                    Assert.Equal("dog.", UnnamedRegister.StringValue);
                    Assert.Equal(4, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// The 'e' motion should select up to and including the end of the word
                ///
                /// https://github.com/jaredpar/VsVim/issues/568
                /// </summary>
                [Fact]
                public void EndOfWordMotion()
                {
                    Create("ThisIsALongWord. ThisIsAnotherLongWord!");
                    _vimBuffer.Process("vex");
                    Assert.Equal(". ThisIsAnotherLongWord!", _textBuffer.GetLine(0).GetText());
                }
            }
        }

        public sealed class ExclusiveSelection : VisualModeIntegrationTest
        {
            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _globalSettings.Selection = "exclusive";
            }

            /// <summary>
            /// The caret position should be on the next character for a move right
            /// </summary>
            [Fact]
            public void CaretPosition_Right()
            {
                Create("the dog");
                _vimBuffer.Process("vl");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The caret position should be on the start of the next word after leaving visual mode
            /// </summary>
            [Fact]
            public void CaretPosition_Word()
            {
                Create("the dog");
                _vimBuffer.Process("vw");
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure the 'e' motion still goes one character extra during a line wise movement
            /// </summary>
            [Fact]
            public void CaretPosition_EndOfWordLineWise()
            {
                Create("the dog. the cat");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("Ve");
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The $ movement should put the caret past the end of the line
            /// </summary>
            [Fact]
            public void MoveEndOfLine_Dollar()
            {
                Create("cat", "dog");
                _vimBuffer.Process("v$");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The 'l' movement should put the caret past the end of the line 
            /// </summary>
            [Fact]
            public void MoveEndOfLine_Right()
            {
                Create("cat", "dog");
                _vimBuffer.Process("vlll");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The entire word should be selected 
            /// </summary>
            [Fact]
            public void InnerWord()
            {
                Create("cat   dog");
                _vimBuffer.Process("viw");
                Assert.Equal("cat", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The entire word plus the trailing white space should be selected
            /// </summary>
            [Fact]
            public void AllWord()
            {
                Create("cat   dog");
                _vimBuffer.Process("vaw");
                Assert.Equal("cat   ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The initial character selection in exclusive selection should be empty 
            /// </summary>
            [Fact]
            public void Issue1483()
            {
                Create("cat dog");
                _vimBuffer.Process("v");
                Assert.Equal(0, _textView.GetSelectionSpan().Length);
            }
        }

        public abstract class BlockInsertTest : VisualModeIntegrationTest
        {
            /// <summary>
            /// Simulate intellisense scenarios and make sure that we correctly insert the resulting
            /// text
            /// </summary>
            public sealed class IntellisenseTest : BlockInsertTest
            {
                /// <summary>
                /// Pretend there was nothing to delete, it just got inserted by hitting Ctrl+Space 
                /// and selecting the value
                /// </summary>
                [Fact]
                public void SimpleIntellisense()
                {
                    Create(
@" string Prop1
 string Prop2
 string Prop3");
                    _vimBuffer.ProcessNotation("<C-Q>jjI");

                    // No simulate the intellisense operation here.  It will delete the "matched" text
                    // and replace with the "completed" text
                    _textBuffer.Replace(new Span(0, 0), "protected");
                    _vimBuffer.ProcessNotation("<Esc>");
                    Assert.Equal(new[]
                        {
                            "protected string Prop1",
                            "protected string Prop2",
                            "protected string Prop3"
                        },
                        _textBuffer.GetLines());
                }

                [Fact]
                public void Issue1108()
                {
                    Create(
@" string Prop1
 string Prop2
 string Prop3");
                    _vimBuffer.ProcessNotation("<C-Q>jjIpr");

                    // No simulate the intellisense operation here.  It will delete the "matched" text
                    // and replace with the "completed" text
                    _textBuffer.Replace(new Span(0, 2), "protected");
                    _vimBuffer.ProcessNotation("<Esc>");
                    Assert.Equal(new[]
                        {
                            "protected string Prop1",
                            "protected string Prop2",
                            "protected string Prop3"
                        },
                        _textBuffer.GetLines());
                }
            }

            public sealed class PartialTabEditTest : BlockInsertTest
            {
                [Fact]
                public void SimpleMiddle()
                {
                    Create(4, "trucker", "\tdog", "tester");
                    _vimBuffer.ProcessNotation("ll<c-q>jjjIa<Esc>");
                    Assert.Equal(new[]
                        {
                            "traucker",
                            "  a  dog",
                            "teaster"
                        },
                        _textBuffer.GetLines());
                    Assert.Equal(2, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// When the selection is at the start of the tab then the tab should be 
                /// kept because it is not being split 
                /// </summary>
                [Fact]
                public void SimpleStartOfLine()
                {
                    Create(4, "trucker", "\tdog", "tester");
                    _vimBuffer.ProcessNotation("<c-q>jjjIa<Esc>");
                    Assert.Equal(new[]
                        {
                            "atrucker",
                            "a\tdog",
                            "atester"
                        },
                        _textBuffer.GetLines());
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void SimpleOneSpaceIn()
                {
                    Create(4, "trucker", "\tdog", "tester");
                    _vimBuffer.ProcessNotation("l<c-q>jjjIa<Esc>");
                    Assert.Equal(new[]
                        {
                            "tarucker",
                            " a   dog",
                            "taester"
                        },
                        _textBuffer.GetLines());
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void SimpleLastSpaceInTab()
                {
                    Create(4, "trucker", "\tdog", "tester");
                    _vimBuffer.ProcessNotation("lll<c-q>jjjIa<Esc>");
                    Assert.Equal(new[]
                        {
                            "truacker",
                            "   a dog",
                            "tesater"
                        },
                        _textBuffer.GetLines());
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                }
            }

            public sealed class MiscTest : BlockInsertTest
            {
                /// <summary>
                /// The block insert should add the text to every column
                /// </summary>
                [Fact]
                public void Simple()
                {
                    Create("dog", "cat", "fish");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>the <Esc>");
                    Assert.Equal("the dog", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("the cat", _textBuffer.GetLine(1).GetText());
                }

                /// <summary>
                /// The caret should be positioned at the start of the block span when the insertion
                /// starts
                /// </summary>
                [Fact]
                public void CaretPosition()
                {
                    Create("dog", "cat", "fish");
                    _vimBuffer.ProcessNotation("<C-q>jl<S-i>");
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                    Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                }

                /// <summary>
                /// The block insert shouldn't add text to any of the columns which didn't extend into 
                /// the original selection
                /// </summary>
                [Fact]
                public void EmptyColumn()
                {
                    Create("dog", "", "fish");
                    _vimBuffer.ProcessNotation("l<C-q>jjl<S-i> the <Esc>");
                    Assert.Equal("d the og", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("", _textBuffer.GetLine(1).GetText());
                    Assert.Equal("f the ish", _textBuffer.GetLine(2).GetText());
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// The undo of a block insert should undo all of the inserts
                /// </summary>
                [Fact]
                public void Undo()
                {
                    Create("dog", "cat", "fish");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>the <Esc>");
                    Assert.Equal("the dog", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("the cat", _textBuffer.GetLine(1).GetText());
                    _vimBuffer.Process('u');
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                /// <summary>
                /// Delete actions aren't repeated
                /// </summary>
                [Fact]
                public void DontRepeatDelete()
                {
                    Create("dog", "cat", "fish");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i><Del><Esc>");
                    Assert.Equal("og", _textView.GetLine(0).GetText());
                    Assert.Equal("cat", _textView.GetLine(1).GetText());
                }
            }

            public sealed class RepeatTest : BlockInsertTest
            {
                /// <summary>
                /// The repeat of a block insert should work against the same number of lines as the
                /// original change
                /// </summary>
                [Fact]
                public void SameNumberOfLines()
                {
                    Create("cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>x<Esc>j.");
                    Assert.Equal(new[] { "xcat", "xxdog", "xfish" }, _textBuffer.GetLines());
                }

                /// <summary>
                /// If the repeat goes off the end of the ITextBuffer then the change should just be 
                /// applied to the lines from the caret to the end
                /// </summary>
                [Fact]
                public void PasteEndOfBuffer()
                {
                    Create("cat", "dog", "fish");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>x<Esc>jj.");
                    Assert.Equal(new[] { "xcat", "xdog", "xfish" }, _textBuffer.GetLines());
                }

                /// <summary>
                /// Spaces don't matter in the repeat.  The code should just treat them as normal characters and
                /// repeat the edits into them
                /// </summary>
                [Fact]
                public void DontConsiderSpaces()
                {
                    Create("cat", "dog", " fish");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>x<Esc>jj.");
                    Assert.Equal(new[] { "xcat", "xdog", "x fish" }, _textBuffer.GetLines());
                }

                /// <summary>
                /// Make sure that we handle deletes properly.  So long as it leaves us with a new bit of text then
                /// we can repeat it
                /// </summary>
                [Fact]
                public void HandleDeletes()
                {
                    Create("cat", "dog", "fish", "store");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>xy<BS><Esc>jj.");
                    Assert.Equal(new[] { "xcat", "xdog", "xfish", "xstore" }, _textBuffer.GetLines());
                }

                /// <summary>
                /// Make sure the code properly handles the case where the insert results in 0 text being added
                /// to the file.  This should cause us to not do anything even on repeat
                /// </summary>
                [Fact]
                public void HandleEmptyInsertString()
                {
                    Create("cat", "dog", "fish", "store");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>xy<BS><BS><Esc>jj.");
                    Assert.Equal(new[] { "cat", "dog", "fish", "store" }, _textBuffer.GetLines());
                }

                [Fact]
                public void Issue1136()
                {
                    Create("cat", "dog");
                    _vimBuffer.ProcessNotation("<C-q>j<S-i>x<Esc>.");
                    Assert.Equal(new[] { "xxcat", "xxdog" }, _textBuffer.GetLines());
                }
            }
        }

        public sealed class BlockChange : VisualModeIntegrationTest
        {
            /// <summary>
            /// The block insert should add the text to every column
            /// </summary>
            [Fact]
            public void Simple()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>jcthe <Esc>");
                Assert.Equal("the og", _textBuffer.GetLine(0).GetText());
                Assert.Equal("the at", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure an undo of a block edit goes back to the original text and replaces
            /// the cursor at the start of the block
            /// </summary>
            [Fact]
            public void Undo()
            {
                Create("dog", "cat", "fish");
                _vimBuffer.ProcessNotation("<C-q>jcthe <Esc>u");
                Assert.Equal(
                    new[] { "dog", "cat", "fish" },
                    _textBuffer.GetLines());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void RenameFunction()
            {
                Create("foo()", "foo()");
                _vimBuffer.ProcessNotation("<C-q>jllcbar<Esc>");
                Assert.Equal(
                    new[] { "bar()", "bar()" },
                    _textBuffer.GetLines());
            }
        }

        public sealed class Move : VisualModeIntegrationTest
        {
            [Fact]
            public void HomeToStartOfLine()
            {
                Create("cat dog");
                _textView.MoveCaretTo(2);
                _vimBuffer.ProcessNotation("v<Home>");
                Assert.Equal("cat", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            [Fact]
            public void HomeToStartOfLineViaKeypad()
            {
                Create("cat dog");
                _textView.MoveCaretTo(2);
                _vimBuffer.ProcessNotation("v<kHome>");
                Assert.Equal("cat", _textView.GetSelectionSpan().GetText());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Jump to a mark and make sure that the selection correctly updates
            /// </summary>
            [Fact]
            public void JumpMarkLine_Character()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.MarkMap.SetLocalMark('b', _vimBufferData, 1, 1);
                _vimBuffer.Process("v'b");
                Assert.Equal("at\r\nd", _textView.GetSelectionSpan().GetText());
            }

            /// <summary>
            /// Jump to a mark and make sure that the selection correctly updates
            /// </summary>
            [Fact]
            public void JumpMark_Character()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(1);
                _vimBuffer.MarkMap.SetLocalMark('b', _vimBufferData, 1, 1);
                _vimBuffer.Process("v`b");
                Assert.Equal("at\r\ndo", _textView.GetSelectionSpan().GetText());
            }
        }

        public abstract class ReplaceSelectionTest : VisualModeIntegrationTest
        {
            public sealed class CharacterWiseTest : ReplaceSelectionTest
            {
                [Fact]
                public void Simple()
                {
                    Create("cat dog", "tree fish");
                    _vimBuffer.ProcessNotation("vllra");
                    Assert.Equal(new[] { "aaa dog", "tree fish" }, _textBuffer.GetLines());
                }

                [Fact]
                public void ExtendIntoNewLine()
                {
                    Create("cat", "dog");
                    _vimBuffer.GlobalSettings.VirtualEdit = "onemore";
                    _vimBuffer.ProcessNotation("vlllllra");
                    Assert.Equal(new[] { "aaa", "dog" }, _textBuffer.GetLines());
                }

                [Fact]
                public void MultiLine()
                {
                    Create("cat", "dog");
                    _vimBuffer.ProcessNotation("vjra");
                    Assert.Equal(new[] { "aaa", "aog" }, _textBuffer.GetLines());
                }
            }

            public sealed class LineWiseTest : ReplaceSelectionTest
            {
                [Fact]
                public void Single()
                {
                    Create("cat", "dog");
                    _vimBuffer.ProcessNotation("Vra");
                    Assert.Equal(new[] { "aaa", "dog" }, _textBuffer.GetLines());
                }

                [Fact]
                public void Issue1201()
                {
                    Create("one two three", "four five six");
                    _vimBuffer.ProcessNotation("Vr-");
                    Assert.Equal("-------------", _textBuffer.GetLine(0).GetText());
                }
            }

            public sealed class BlockWiseTest : ReplaceSelectionTest
            {
                /// <summary>
                /// This is an anti test.
                /// 
                /// The WPF editor has no way to position the caret in the middle of a 
                /// tab.  It can't for instance place it on the 2 space of the 4 spaces
                /// the caret occupies.  Hence this test have a deviating behavior from
                /// gVim because the caret position differs on the final 'l' 
                /// </summary>
                [Fact]
                public void Overlap()
                {
                    Create("cat", "d\tog");
                    _vimBuffer.LocalSettings.TabStop = 4;
                    _vimBuffer.ProcessNotation("ll<C-q>jlra");
                    Assert.Equal(new[] { "caa", "d aaag" }, _textBuffer.GetLines());
                }
            }
        }

        public sealed class Insert : VisualModeIntegrationTest
        {
            /// <summary>
            /// When switching to insert mode the caret should move to the start of the line
            /// </summary>
            [Fact]
            public void MiddleOfLine()
            {
                Create("cat", "dog");
                _vimBuffer.Process("vllI");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// In an undo the caret should go back to the start of the line.  
            /// Disabled: Undo testing infrastructure doesn't support this yet
            /// </summary>
            public void Undo()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("vllIbig ");
                Assert.Equal("big cat", _textBuffer.GetLine(0).GetText());
                _vimBuffer.ProcessNotation("<Esc>u");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class SelectionTest : VisualModeIntegrationTest
        {
            /// <summary>
            /// In Visual Mode it is possible to move the caret past the end of the line even if
            /// 'virtualedit='.  
            /// </summary>
            [Fact]
            public void MoveToEndOfLineCharacter()
            {
                Create("cat", "dog");
                _vimBuffer.Process("vlll");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void MoveToEndOfLineLine()
            {
                Create("cat", "dog");
                _vimBuffer.Process("Vlll");
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void Issue1790()
            {
                Create(" the");
                _vimBuffer.Process("vas");
                Assert.Equal(_textBuffer.GetSpan(start: 0, length: 4), _textView.GetSelectionSpan());
            }
        }

        public abstract class TagBlockTest : VisualModeIntegrationTest
        {
            public sealed class CharacterWiseTest : TagBlockTest
            {
                [Fact]
                public void InnerSimpleMultiLine()
                {
                    Create("<a>", "blah", "</a>");
                    _textView.MoveCaretToLine(1);
                    _vimBuffer.Process("vity");
                    Assert.Equal(Environment.NewLine + "blah" + Environment.NewLine, UnnamedRegister.StringValue);
                }

                [Fact]
                public void InnerSimpleSingleLine()
                {
                    Create("<a>blah</a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("vit");

                    var span = new Span(_textBuffer.GetPointInLine(0, 3), 4);
                    Assert.Equal(span, _textView.GetSelectionSpan());
                }

                [Fact]
                public void AllSimpleSingleLine()
                {
                    Create("<a>blah</a>");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("vat");

                    var span = new Span(_textBuffer.GetPoint(0), _textBuffer.CurrentSnapshot.Length);
                    Assert.Equal(span, _textView.GetSelectionSpan());
                }
            }

            public sealed class ExpandSelectionTest : TagBlockTest
            {
                [Fact]
                public void InnerSimple()
                {
                    var text = "<a>blah</a>";
                    Create(text);
                    _textView.MoveCaretTo(5);
                    _vimBuffer.Process("vit");
                    Assert.Equal("blah", _textView.GetSelectionSpan().GetText());
                    _vimBuffer.Process("it");
                    Assert.Equal(text, _textView.GetSelectionSpan().GetText());
                }

                [Fact]
                public void InnerNestedNoPadding()
                {
                    var text = "<a><b>blah</b></a>";
                    Create(text);
                    _textView.MoveCaretTo(7);
                    _vimBuffer.Process("vit");
                    Assert.Equal("blah", _textView.GetSelectionSpan().GetText());
                    _vimBuffer.Process("it");
                    Assert.Equal("<b>blah</b>", _textView.GetSelectionSpan().GetText());
                    _vimBuffer.Process("it");
                    Assert.Equal(text, _textView.GetSelectionSpan().GetText());
                }

                [Fact]
                public void InnerNestedPadding()
                {
                    var text = "<a>  <b>blah</b></a>";
                    Create(text);
                    _textView.MoveCaretTo(7);
                    _vimBuffer.Process("vit");
                    Assert.Equal("blah", _textView.GetSelectionSpan().GetText());
                    _vimBuffer.Process("it");
                    Assert.Equal("<b>blah</b>", _textView.GetSelectionSpan().GetText());
                    _vimBuffer.Process("it");
                    Assert.Equal("  <b>blah</b>", _textView.GetSelectionSpan().GetText());
                    _vimBuffer.Process("it");
                    Assert.Equal(text, _textView.GetSelectionSpan().GetText());
                }

                [Fact]
                public void AllNested()
                {
                    var text = "<a><b>blah</b></a>";
                    Create(text);
                    _textView.MoveCaretTo(7);
                    _vimBuffer.Process("vat");
                    Assert.Equal("<b>blah</b>", _textView.GetSelectionSpan().GetText());
                    _vimBuffer.Process("at");
                    Assert.Equal(text, _textView.GetSelectionSpan().GetText());
                }
            }
        }

        public abstract class InvertSelectionTest : VisualModeIntegrationTest
        {
            public sealed class CharacterWiseTest : InvertSelectionTest
            {
                [Fact]
                public void Simple()
                {
                    Create("cat and the dog");
                    _vimBuffer.Process("vlllo");
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                    Assert.Equal(4, _textView.Selection.AnchorPoint.Position);
                    Assert.Equal("cat ", _textView.GetSelectionSpan().GetText());
                }

                [Fact]
                public void SingleCharacterSelected()
                {
                    Create("cat");
                    _vimBuffer.Process("voooo");
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                    Assert.Equal(0, _textView.Selection.AnchorPoint.Position);
                    Assert.Equal("c", _textView.GetSelectionSpan().GetText());
                }

                [Fact]
                public void BackAndForth()
                {
                    Create("cat and the dog");
                    _vimBuffer.Process("vllloo");
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                    Assert.Equal(0, _textView.Selection.AnchorPoint.Position);
                    Assert.Equal("cat ", _textView.GetSelectionSpan().GetText());
                }

                [Fact]
                public void Multiline()
                {
                    Create("cat", "dog");
                    _vimBuffer.Process("lvjo");
                    var span = _textView.GetSelectionSpan();
                    Assert.Equal("at" + Environment.NewLine + "do", span.GetText());
                    Assert.True(_textView.Selection.IsReversed);
                    Assert.Equal(1, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void PastEndOfLine()
                {
                    Create("cat", "dog");
                    _vimBuffer.GlobalSettings.VirtualEdit = "onemore";
                    _vimBuffer.Process("vlllo");
                    Assert.Equal(4, _textView.Selection.StreamSelectionSpan.Length);
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                }

                [Fact]
                public void PastEndOfLineReverse()
                {
                    Create("cat", "dog");
                    _vimBuffer.GlobalSettings.VirtualEdit = "onemore";
                    _vimBuffer.Process("vllloo");
                    Assert.Equal(4, _textView.Selection.StreamSelectionSpan.Length);
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                }
            }

            public sealed class LineWiseTest : InvertSelectionTest
            {
                [Fact]
                public void Simple()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("Vjo");
                    Assert.Equal(0, _textView.GetCaretPoint());
                    var span = _textView.GetSelectionSpan();
                    Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, span.GetText());
                }

                [Fact]
                public void BackAndForth()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("Vjoo");
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                    var span = _textView.GetSelectionSpan();
                    Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, span.GetText());
                }

                [Fact]
                public void SimpleNonZeroStart()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("lVjo");
                    Assert.Equal(1, _textView.GetCaretPoint());
                    var span = _textView.GetSelectionSpan();
                    Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, span.GetText());
                }

                [Fact]
                public void StartOnEmptyLine()
                {
                    Create("cat", "", "dog", "tree");
                    _textView.MoveCaretTo(_textBuffer.GetLine(1).Start);
                    _vimBuffer.ProcessNotation("Vjo");
                    Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
                    var span = _textView.GetSelectionSpan();
                    Assert.Equal(Environment.NewLine + "dog" + Environment.NewLine, span.GetText());
                }
            }

            public sealed class BlockTest : InvertSelectionTest
            {
                [Fact]
                public void Simple()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("<C-q>ljo");
                    Assert.Equal(0, _textView.GetCaretPoint().Position);
                    var blockSpan = _vimBuffer.GetSelectionBlockSpan();
                    Assert.Equal(2, blockSpan.Height);
                    Assert.Equal(2, blockSpan.Spaces);
                }

                [Fact]
                public void SimpleBackAndForth()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("<C-q>ljoo");
                    Assert.Equal(_textView.GetPointInLine(1, 1), _textView.GetCaretPoint().Position);
                    var blockSpan = _vimBuffer.GetSelectionBlockSpan();
                    Assert.Equal(2, blockSpan.Height);
                    Assert.Equal(2, blockSpan.Spaces);
                }
            }

            public sealed class BlockColumnOnlyTest : InvertSelectionTest
            {
                [Fact]
                public void Simple()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("<C-q>ljO");
                    Assert.Equal(_textView.GetPointInLine(1, 0), _textView.GetCaretPoint().Position);
                    var blockSpan = _vimBuffer.GetSelectionBlockSpan();
                    Assert.Equal(2, blockSpan.Height);
                    Assert.Equal(2, blockSpan.Spaces);
                }

                [Fact]
                public void SimpleBackAndForth()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("<C-q>ljOO");
                    Assert.Equal(_textView.GetPointInLine(1, 1), _textView.GetCaretPoint().Position);
                    var blockSpan = _vimBuffer.GetSelectionBlockSpan();
                    Assert.Equal(2, blockSpan.Height);
                    Assert.Equal(2, blockSpan.Spaces);
                }

                [Fact]
                public void SimpleReverse()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("<C-q>ljoO");
                    Assert.Equal(_textView.GetPointInLine(0, 1), _textView.GetCaretPoint().Position);
                    var blockSpan = _vimBuffer.GetSelectionBlockSpan();
                    Assert.Equal(2, blockSpan.Height);
                    Assert.Equal(2, blockSpan.Spaces);
                }

                [Fact]
                public void SimpleReverseAndForth()
                {
                    Create("cat", "dog", "tree");
                    _vimBuffer.ProcessNotation("<C-q>ljoOO");
                    Assert.Equal(_textView.GetPointInLine(0, 0), _textView.GetCaretPoint().Position);
                    var blockSpan = _vimBuffer.GetSelectionBlockSpan();
                    Assert.Equal(2, blockSpan.Height);
                    Assert.Equal(2, blockSpan.Spaces);
                }
            }
        }

        public sealed class KeyMappingTest : VisualModeIntegrationTest
        {
            [Fact]
            public void VisualAfterCount()
            {
                Create("cat dog");
                _vimBuffer.Process(":vmap <space> l", enter: true);
                _vimBuffer.ProcessNotation("v2<Space>");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                Assert.Equal("cat", _textView.GetSelectionSpan().GetText());
            }

            [Fact]
            public void Issue890()
            {
                Create("cat > dog");
                _vimBuffer.ProcessNotation(@":vmap > >gv", enter: true);
                _vimBuffer.ProcessNotation(@"vf>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }
        }

        public sealed class CanProcessTest : VisualModeIntegrationTest
        {
            /// <summary>
            /// Visual Mode itself doesn't actually process mouse commands.  That is the job of
            /// the selection mode tracker.  
            /// </summary>
            [Fact]
            public void MouseCommands()
            {
                Create("");
                _vimBuffer.Process("v");
                foreach (var keyInput in KeyInputUtil.VimKeyInputList.Where(x => x.IsMouseKey))
                {
                    bool ret = _vimBuffer.CanProcess(keyInput);
                    Assert.False(_vimBuffer.CanProcess(keyInput));
                }
            }

            [Fact]
            public void Simple()
            {
                Create("");
                Assert.True(_vimBuffer.CanProcess('l'));
                Assert.True(_vimBuffer.CanProcess('k'));
            }
        }

        public sealed class ChangeCase : VisualModeIntegrationTest
        {
            [Fact]
            public void Upper_Character()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("vllU");
                Assert.Equal("CAT dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().GetColumn().Column);
            }

            [Fact]
            public void Lower_Character()
            {
                Create("CAT dog");
                _vimBuffer.ProcessNotation("vllu");
                Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().GetColumn().Column);
            }

            [Fact]
            public void Rot13_Character()
            {
                Create("cat dog");
                _vimBuffer.ProcessNotation("vllg?");
                Assert.Equal("png dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().GetColumn().Column);
            }
        }

        public sealed class MiscAllTest : VisualModeIntegrationTest
        {
            /// <summary>
            /// When changing a line wise selection one blank line should be left remaining in the ITextBuffer
            /// </summary>
            [Theory]
            [PropertyData("VirtualEditOptions")]
            public void Change_LineWise(string virtualEdit)
            {
                Create("cat", "  dog", "  bear", "tree");
                _globalSettings.VirtualEdit = virtualEdit;
                SwitchEnterMode(ModeKind.VisualLine, _textView.GetLineRange(1, 2).ExtentIncludingLineBreak);
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("c");
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal("", _textView.GetLine(1).GetText());
                Assert.Equal("tree", _textView.GetLine(2).GetText());
                Assert.Equal(2, _textView.Caret.Position.VirtualBufferPosition.VirtualSpaces);
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// When changing a word we just delete it all and put the caret at the start of the deleted
            /// selection
            /// </summary>
            [Fact]
            public void Change_Word()
            {
                Create("cat chases the ball");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 4));
                _vimBuffer.LocalSettings.AutoIndent = true;
                _vimBuffer.Process("c");
                Assert.Equal("chases the ball", _textView.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure we handle the virtual spaces properly here.  The 'C' command should leave the caret
            /// in virtual space due to the previous indent and escape should cause the caret to jump back to 
            /// real spaces when leaving insert mode
            /// </summary>
            [Fact]
            public void ChangeLineSelection_VirtualSpaceHandling()
            {
                Create("  cat", "dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 2, 2));
                _vimBuffer.Process('C');
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal("", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.False(_textView.GetCaretVirtualPoint().IsInVirtualSpace);
            }

            /// <summary>
            /// Verify that Shift-V enters Visual Line Mode
            /// </summary>
            [Fact]
            public void EnterVisualLine()
            {
                Create("hello", "world");
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<S-v>"));
                Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
            }

            [Fact]
            public void JoinSelection_KeepSpaces_Simple()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.Process("VjJ");
                Assert.Equal(new[] { "cat dog", "tree" }, _textBuffer.GetLines());
            }

            [Fact]
            public void JoinSelection_RemoveSpaces_Simple()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.Process("VjgJ");
                Assert.Equal(new[] { "catdog", "tree" }, _textBuffer.GetLines());
            }

            [Theory]
            [PropertyData("VirtualEditOptions")]
            public void Repeat1(string virtualEdit)
            {
                Create("dog again", "cat again", "chicken");
                _globalSettings.VirtualEdit = virtualEdit;
                SwitchEnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.LocalSettings.ShiftWidth = 2;
                _vimBuffer.Process(">.");
                Assert.Equal("    dog again", _textView.GetLine(0).GetText());
            }

            [Theory]
            [PropertyData("VirtualEditOptions")]
            public void Repeat2(string virtualEdit)
            {
                Create("dog again", "cat again", "chicken");
                _globalSettings.VirtualEdit = virtualEdit;
                SwitchEnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.LocalSettings.ShiftWidth = 2;
                _vimBuffer.Process(">..");
                Assert.Equal("      dog again", _textView.GetLine(0).GetText());
            }

            [Fact]
            public void ResetCaretFromShiftLeft1()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLineRange(0, 1).Extent);
                _vimBuffer.Process("<");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ResetCaretFromShiftLeft2()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLineRange(0, 1).Extent);
                _vimBuffer.Process("<");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void ResetCaretFromYank1()
            {
                Create("  hello", "  world");
                EnterMode(_textView.TextBuffer.GetSpan(0, 2));
                _vimBuffer.Process("y");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Moving the caret which resets the selection should go to normal mode
            /// </summary>
            [Fact]
            public void SelectionChange1()
            {
                Create("  hello", "  world");
                EnterMode(_textView.TextBuffer.GetSpan(0, 2));
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.Selection.Select(
                    new SnapshotSpan(_textView.GetLine(1).Start, 0),
                    false);
                _context.RunAll();
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Moving the caret which resets the selection should go visual if there is still a selection
            /// </summary>
            [Fact]
            public void SelectionChange2()
            {
                Create("  hello", "  world");
                EnterMode(_textView.TextBuffer.GetSpan(0, 2));
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.Selection.Select(
                    new SnapshotSpan(_textView.GetLine(1).Start, 1),
                    false);
                _context.RunAll();
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure we reset the span we need
            /// </summary>
            [Fact]
            public void SelectionChange3()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLine(0).Extent);
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.Selection.Select(_textView.GetLine(1).Extent, false);
                _vimBuffer.Process(KeyInputUtil.CharToKeyInput('y'));
                _context.RunAll();
                Assert.Equal("  world", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
            }

            /// <summary>
            /// Make sure we reset the span we need
            /// </summary>
            [Fact]
            public void SelectionChange4()
            {
                Create("  hello", "  world");
                EnterMode(_textView.GetLine(0).Extent);
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _textView.SelectAndMoveCaret(new SnapshotSpan(_textView.GetLine(1).Start, 3));
                _context.RunAll();
                _vimBuffer.Process("ly");
                Assert.Equal("  wo", _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
            }

            /// <summary>
            /// Make sure that LastVisualSelection is set to the SnapshotSpan before the shift right
            /// command is executed
            /// </summary>
            [Fact]
            public void ShiftLinesRight_LastVisualSelection()
            {
                Create("cat", "dog", "fish");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.GetLine(0).Start, _textView.GetLine(1).Start.Add(1)));
                _vimBuffer.Process('>');
                var visualSelection = VisualSelection.NewCharacter(
                    new CharacterSpan(_textView.GetLine(0).Start, 2, 1),
                    SearchPath.Forward);
                Assert.True(_vimTextBuffer.LastVisualSelection.IsSome());
                Assert.Equal(visualSelection, _vimTextBuffer.LastVisualSelection.Value);
            }

            /// <summary>
            /// Even though a text span is selected, substitute should operate on the line
            /// </summary>
            [Fact]
            public void Substitute1()
            {
                Create("the boy hit the cat", "bat");
                EnterMode(new SnapshotSpan(_textView.TextSnapshot, 0, 2));
                _vimBuffer.Process(":s/a/o", enter: true);
                Assert.Equal("the boy hit the cot", _textView.GetLine(0).GetText());
                Assert.Equal("bat", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Muliline selection should cause a replace per line
            /// </summary>
            [Fact]
            public void Substitute2()
            {
                Create("the boy hit the cat", "bat");
                EnterMode(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process(":s/a/o", enter: true);
                Assert.Equal("the boy hit the cot", _textView.GetLine(0).GetText());
                Assert.Equal("bot", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Switching to command mode shouldn't clear the selection
            /// </summary>
            [Fact]
            public void Switch_ToCommandShouldNotClearSelection()
            {
                Create("cat", "dog", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process(":");
                Assert.False(_textView.GetSelectionSpan().IsEmpty);
            }

            /// <summary>
            /// Switching to normal mode should clear the selection
            /// </summary>
            [Fact]
            public void Switch_ToNormalShouldClearSelection()
            {
                Create("cat", "dog", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process(VimKey.Escape);
                Assert.True(_textView.GetSelectionSpan().IsEmpty);
            }

            [Fact]
            public void Handle_D_BlockMode()
            {
                Create("dog", "cat", "tree");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                _vimBuffer.Process("D");
                Assert.Equal("d", _textView.GetLine(0).GetText());
                Assert.Equal("c", _textView.GetLine(1).GetText());
            }

            [Theory]
            [PropertyData("VirtualEditOptions")]
            public void IncrementalSearch_LineModeShouldSelectFullLine(string virtualEdit)
            {
                Create("dog", "cat", "tree");
                _globalSettings.VirtualEdit = virtualEdit;
                SwitchEnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process("/c");
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            }

            [Fact]
            public void IncrementalSearch_LineModeShouldSelectFullLineAcrossBlanks()
            {
                Create("dog", "", "cat", "tree");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0, 1).ExtentIncludingLineBreak);
                _vimBuffer.Process("/ca");
                Assert.Equal(_textView.GetLineRange(0, 2).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            }

            [Fact]
            public void IncrementalSearch_CharModeShouldExtendToSearchResult()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.GetLine(0).Start, 1));
                _vimBuffer.Process("/o");
                Assert.Equal(new SnapshotSpan(_textView.GetLine(0).Start, 2), _textView.GetSelectionSpan());
            }

            /// <summary>
            /// An incremental search operation shouldn't change the location of the caret until the search is
            /// completed
            /// </summary>
            [Fact]
            public void IncrementalSearch_DontChangeCaret()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.Process("v/do");
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that Escape will properly exit the incremental search and return us to the previous
            /// visual mode state (with the same selection)
            /// </summary>
            [Fact]
            public void IncrementalSearch_EscapeShouldExitSearch()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.ProcessNotation("vl/dog<Esc>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.False(_vimBuffer.IncrementalSearch.InSearch);
                Assert.Equal("ca", _textView.GetSelectionSpan().GetText());
            }

            /// <summary>
            /// Make sure that enter completes the search which includes updating the caret
            /// </summary>
            [Fact]
            public void IncrementalSearch_EnterShouldCompleteSearch()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.ProcessNotation("vl/dog<Enter>");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                Assert.False(_vimBuffer.IncrementalSearch.InSearch);
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Enter visual mode with the InitialVisualSelection argument which is a character span
            /// </summary>
            [Fact]
            public void InitialVisualSelection_Character()
            {
                Create("dogs", "cats");

                var visualSpan = VimUtil.CreateVisualSpanCharacter(_textBuffer.GetSpan(1, 2));
                var visualSelection = VisualSelection.CreateForward(visualSpan);
                _vimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
                _context.RunAll();
                Assert.Equal(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Character, SelectionKind.Inclusive, tabStop: 4));
            }

            /// <summary>
            /// Enter visual mode with the InitialVisualSelection argument which is a line span
            /// </summary>
            [Fact]
            public void InitialVisualSelection_Line()
            {
                Create("dogs", "cats", "fish");

                var lineRange = _textView.GetLineRange(0, 1);
                var visualSelection = VisualSelection.NewLine(lineRange, SearchPath.Forward, 1);
                _vimBuffer.SwitchMode(ModeKind.VisualLine, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
                _context.RunAll();
                Assert.Equal(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Line, SelectionKind.Inclusive, tabStop: 4));
            }

            /// <summary>
            /// Enter visual mode with the InitialVisualSelection argument which is a block span
            /// </summary>
            [Fact]
            public void InitialVisualSelection_Block()
            {
                Create("dogs", "cats", "fish");

                var blockSpan = _textView.GetBlockSpan(1, 2, 0, 2);
                var visualSelection = VisualSelection.NewBlock(blockSpan, BlockCaretLocation.BottomLeft);
                _vimBuffer.SwitchMode(ModeKind.VisualBlock, ModeArgument.NewInitialVisualSelection(visualSelection, FSharpOption<SnapshotPoint>.None));
                _context.RunAll();
                Assert.Equal(visualSelection, VisualSelection.CreateForSelection(_textView, VisualKind.Block, SelectionKind.Inclusive, tabStop: 4));
            }

            /// <summary>
            /// Record a macro which delets selected text.  When the macro is played back it should
            /// just run the delete against unselected text.  In other words it's just the raw keystrokes
            /// which are saved not the selection state
            /// </summary>
            [Fact]
            public void Macro_RecordDeleteSelectedText()
            {
                Create("the cat chased the dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                _vimBuffer.Process("qcxq");
                Assert.Equal(" cat chased the dog", _textView.GetLine(0).GetText());
                _textView.MoveCaretTo(1);
                _vimBuffer.Process("@c");
                Assert.Equal(" at chased the dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Run the macro to delete the selected text
            /// </summary>
            [Fact]
            public void Macro_RunDeleteSelectedText()
            {
                Create("the cat chased the dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                TestRegister.UpdateValue("x");
                _vimBuffer.Process("@c");
                Assert.Equal(" cat chased the dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// When the final line of the ITextBuffer is an empty line make sure that we can
            /// move up off of it when in Visual Line Mode.  
            /// 
            /// Issue #769
            /// </summary>
            [Fact]
            public void Move_Line_FromBottom()
            {
                Create("cat", "dog", "");
                _textView.MoveCaretToLine(2);
                _vimBuffer.Process("Vk");
                Assert.Equal(_textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            }

            /// <summary>
            /// Make sure that we can use 'j' to go over an empty line in Visual Character 
            /// mode
            /// 
            /// Issue #758
            /// </summary>
            [Fact]
            public void Move_Character_OverEmptyLine()
            {
                Create("cat", "", "dog");
                _vimBuffer.Process("vjj");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Test the movement of the caret over a shorter line and then back to a line long
            /// enough
            /// </summary>
            [Fact]
            public void Move_Block_OverShortLine()
            {
                Create("really long line", "short", "really long line");
                _textView.MoveCaretTo(7);
                _vimBuffer.ProcessNotation("<C-v>lll");
                Assert.Equal("long", _textView.Selection.SelectedSpans[0].GetText());
                _vimBuffer.ProcessNotation("jj");
                var spans = _textView.Selection.SelectedSpans;
                Assert.Equal(3, spans.Count);
                Assert.Equal("long", spans[0].GetText());
                Assert.Equal("", spans[1].GetText());
                Assert.Equal("long", spans[2].GetText());
            }

            /// <summary>
            /// Character should be positioned at the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithSingleCharacterWise()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal("dcatg", _textView.GetLine(0).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
                Assert.Equal("o", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned after the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithSingleCharacterWiseAndCaretMove()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat", OperationKind.CharacterWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dcatg", _textView.GetLine(0).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
                Assert.Equal("o", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the start of the inserted line
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithLineWise()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal("d", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal("g", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be positioned at the first line after the inserted
            /// lines
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithLineWiseAndCaretMove()
            {
                Create("dog");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateValue("cat\n", OperationKind.LineWise);
                _vimBuffer.Process("gp");
                Assert.Equal("d", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal("g", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be positioned at the start of the first line in the
            /// block 
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithBlock()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbat", _textView.GetLine(1).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Caret should be positioned after the line character in the last 
            /// line of the inserted block
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_WithBlockAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 1, 1));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("gp");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
            }

            /// <summary>
            /// When doing a put over selection the text being deleted should be put into
            /// the unnamed register.
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_NamedRegisters()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                _registerMap.GetRegister('c').UpdateValue("pig");
                _vimBuffer.Process("\"cp");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("dog", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// When doing a put over selection the text being deleted should be put into
            /// the unnamed register.  If the put came from the unnamed register then the 
            /// original put value is overwritten
            /// </summary>
            [Fact]
            public void PutOver_CharacterWise_UnnamedRegisters()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineSpan(0, 0, 3));
                UnnamedRegister.UpdateValue("pig");
                _vimBuffer.Process("p");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("dog", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithCharcterWise()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned after the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithCharacterWiseAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("gp");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithLineWise()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned after the end of the inserted text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithLineWiseAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("gp");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the start of the first inserted value
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithBlock()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("aa", _textView.GetLine(0).GetText());
                Assert.Equal("bb", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the first character after the inserted
            /// text
            /// </summary>
            [Fact]
            public void PutOver_LineWise_WithBlockAndCaretMove()
            {
                Create("dog", "cat");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("gp");
                Assert.Equal("aa", _textView.GetLine(0).GetText());
                Assert.Equal("bb", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
                Assert.Equal("dog\r\n", UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Character should be positioned at the start of the first inserted value
            /// </summary>
            [Fact]
            public void PutOver_Block_WithCharacterWise()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("p");
                Assert.Equal("dfishg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Character should be positioned after the last character after the inserted
            /// text
            /// </summary>
            [Fact]
            public void PutOver_Block_WithCharacterWiseAndCaretMove()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish", OperationKind.CharacterWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dfishg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal(5, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Character should be positioned at the start of the inserted line
            /// </summary>
            [Fact]
            public void PutOver_Block_WithLineWise()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("p");
                Assert.Equal("dg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal("fish", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Caret should be positioned at the start of the line which follows the
            /// inserted lines
            /// </summary>
            [Fact]
            public void PutOver_Block_WithLineWiseAndCaretMove()
            {
                Create("dog", "cat", "bear");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateValue("fish\n", OperationKind.LineWise);
                _vimBuffer.Process("gp");
                Assert.Equal("dg", _textView.GetLine(0).GetText());
                Assert.Equal("ct", _textView.GetLine(1).GetText());
                Assert.Equal("fish", _textView.GetLine(2).GetText());
                Assert.Equal(_textView.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Character should be positioned at the start of the first inserted string
            /// from the block
            /// </summary>
            [Fact]
            public void PutOver_Block_WithBlock()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("p");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbt", _textView.GetLine(1).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Caret should be positioned at the first character after the last inserted
            /// charecter of the last string in the block
            /// </summary>
            [Fact]
            public void PutOver_Block_WithBlockAndCaretMove()
            {
                Create("dog", "cat");
                EnterBlock(_textView.GetBlockSpan(1, 1, 0, 2));
                UnnamedRegister.UpdateBlockValues("aa", "bb");
                _vimBuffer.Process("gp");
                Assert.Equal("daag", _textView.GetLine(0).GetText());
                Assert.Equal("cbbt", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
            }

            [Fact]
            public void PutOver_Legacy1()
            {
                Create("dog", "cat", "bear", "tree");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.TextSnapshot, 0, 2));
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
                _vimBuffer.Process("p");
                Assert.Equal("pigg", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void PutOver_Legacy2()
            {
                Create("dog", "cat", "bear", "tree");
                var span = new SnapshotSpan(
                    _textView.GetLine(0).Start.Add(1),
                    _textView.GetLine(1).Start.Add(2));
                EnterMode(ModeKind.VisualCharacter, span);
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
                _vimBuffer.Process("p");
                Assert.Equal("dpigt", _textView.GetLine(0).GetText());
                Assert.Equal("bear", _textView.GetLine(1).GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void PutBefore_Legacy1()
            {
                Create("dog", "cat", "bear", "tree");
                EnterMode(ModeKind.VisualCharacter, _textView.GetLineRange(0).Extent);
                _vimBuffer.RegisterMap.GetRegister(RegisterName.Unnamed).UpdateValue("pig");
                _vimBuffer.Process("P");
                Assert.Equal("pig", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Put with indent commands are another odd ball item in Vim.  It's the one put command
            /// which doesn't delete the selection when putting the text into the buffer.  Instead 
            /// it just continues on in visual mode after the put
            /// </summary>
            [Fact]
            public void PutAfterWithIndent_VisualLine()
            {
                Create("  dog", "  cat", "bear");
                EnterMode(ModeKind.VisualLine, _textView.GetLineRange(0).ExtentIncludingLineBreak);
                UnnamedRegister.UpdateValue("bear" + Environment.NewLine, OperationKind.LineWise);
                _vimBuffer.Process("]p");
                Assert.Equal("  dog", _textView.GetLine(0).GetText());
                Assert.Equal("  bear", _textView.GetLine(1).GetText());
                Assert.Equal(_textView.GetPointInLine(1, 2), _textView.GetCaretPoint());
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
                Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Simple inner word selection on visual mode
            /// </summary>
            [Fact]
            public void TextObject_InnerWord()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process("viw");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When a 'iw' text selection occurs and extends the selection backwards it should reset
            /// the visual caret start point.  This can be demonstrated jumping back and forth between
            /// character and line mode
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_ResetVisualStartPoint()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process("viwVv");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simple inner word selection from the middle of a word.  Should still select the entire
            /// word
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_FromMiddle()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(5);
                _vimBuffer.Process("viw");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// This behavior isn't documented.  But if iw begins on a single white space character 
            /// then repeated iw shouldn't change anything.  It should select the single space and 
            /// go from there
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_FromSingleWhiteSpace()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process('v');
                for (var i = 0; i < 10; i++)
                {
                    _vimBuffer.Process("iw");
                    Assert.Equal(" ", _textView.GetSelectionSpan().GetText());
                    Assert.Equal(3, _textView.GetCaretPoint().Position);
                }
            }

            /// <summary>
            /// From a non-single white space the inner word motion should select
            /// the entire white space
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_FromMultipleWhiteSpace()
            {
                Create("cat  dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("viw");
                Assert.Equal("  ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(4, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The non initial selection from white space should extend to the 
            /// next word
            /// </summary>
            [Fact]
            public void TextObject_InnerWord_MultipleWhiteSpace_Second()
            {
                Create("cat  dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("viwiw");
                Assert.Equal("  dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Simple all word selection
            /// </summary>
            [Fact]
            public void TextObject_AllWord()
            {
                Create("cat dog fish");
                _vimBuffer.Process("vaw");
                Assert.Equal("cat ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Unlike the 'iw' motion the 'aw' motion doesn't have truly odd behavior from
            /// a single white space
            /// </summary>
            [Fact]
            public void TextObject_AllWord_FromSingleWhiteSpace()
            {
                Create("cat dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("vaw");
                Assert.Equal(" dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(6, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Ensure the ab motion includes the parens and puts the caret on the last 
            /// character
            /// </summary>
            [Fact]
            public void TextObject_AllParen_MiddleOfWord()
            {
                Create("cat (dog) fish");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("vab");
                Assert.Equal("(dog)", _textView.GetSelectionSpan().GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Unlike non-block selections multiple calls to ab won't extend the selection
            /// to a sibling block
            /// </summary>
            [Fact]
            public void TextObject_AllParen_Multiple()
            {
                Create("cat (dog) (bear)");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("vabababab");
                Assert.Equal("(dog)", _textView.GetSelectionSpan().GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Text object selections will extend to outer blocks
            /// </summary>
            [Fact]
            public void TextObject_AllParen_ExpandOutward()
            {
                Create("cat (fo(bad)od) bear");
                _textView.MoveCaretTo(9);
                _vimBuffer.Process("vab");
                Assert.Equal("(bad)", _textView.GetSelectionSpan().GetText());
                _vimBuffer.Process("ab");
                Assert.Equal("(fo(bad)od)", _textView.GetSelectionSpan().GetText());
                Assert.Equal(14, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Text object selections will extend to outer blocks
            /// </summary>
            [Fact]
            public void TextObject_Count_AllParen_ExpandOutward()
            {
                Create("cat (fo(bad)od) bear");
                _textView.MoveCaretTo(9);
                _vimBuffer.Process("v2ab");
                Assert.Equal("(fo(bad)od)", _textView.GetSelectionSpan().GetText());
                Assert.Equal(14, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void TextObject_Quotes_Included()
            {
                Create(@"cat ""dog"" tree");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.TextSnapshot, 5, 1));
                _vimBuffer.Process(@"i""i""");
                Assert.Equal(@"""dog""", _textView.GetSelectionSpan().GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void TextObject_Count_Quotes_Included()
            {
                Create(@"cat ""dog"" tree");
                EnterMode(ModeKind.VisualCharacter, new SnapshotSpan(_textView.TextSnapshot, 5, 1));
                _vimBuffer.Process(@"2i""");
                Assert.Equal(@"""dog""", _textView.GetSelectionSpan().GetText());
                Assert.Equal(8, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// If we've already selected the inner block at the caret then move outward 
            /// and select the containing block
            /// </summary>
            [Fact]
            public void TextObject_InnerParen_ExpandOutward()
            {
                Create("a (fo(tree)od) b");
                _textView.MoveCaretTo(7);
                _vimBuffer.Process("vib");
                Assert.Equal("tree", _textView.GetSelectionSpan().GetText());
                _vimBuffer.Process("ib");
                Assert.Equal("fo(tree)od", _textView.GetSelectionSpan().GetText());
                Assert.Equal(12, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// If the entire inner block is not yet selected then go ahead and select it 
            /// </summary>
            [Fact]
            public void TextObject_InnerParen_ExpandToFullBlock()
            {
                Create("a (fo(tree)od) b");
                _textView.MoveCaretTo(8);
                _vimBuffer.Process("vl");
                Assert.Equal("ee", _textView.GetSelectionSpan().GetText());
                _vimBuffer.Process("ib");
            }

            /// <summary>
            /// Ensure the ib motion excludes the parens and puts the caret on the last 
            /// character
            /// </summary>
            [Fact]
            public void TextObject_InnerParen_MiddleOfWord()
            {
                Create("cat (dog) fish");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("vib");
                Assert.Equal("dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Ensure the iB motion excludes the brackets and puts the caret on the last 
            /// character
            /// </summary>
            [Fact]
            public void TextObject_InnerBlock()
            {
                Create("int foo (bar b)", "{", "if (true)", "{", "int a;", "int b;", "}", "}");
                _textView.MoveCaretToLine(4);
                _vimBuffer.Process("viB");
                Assert.Equal(_textBuffer.GetLineRange(4, 5).GetText(), _textView.GetSelectionSpan().GetText());
                Assert.Equal(48, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// All white space and the following word should be selecetd
            /// </summary>
            [Fact]
            public void TextObject_AllWord_FromMultipleWhiteSpace()
            {
                Create("cat  dog fish");
                _textView.MoveCaretTo(3);
                _vimBuffer.Process("vaw");
                Assert.Equal("  dog", _textView.GetSelectionSpan().GetText());
                Assert.Equal(7, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When standing in middle of word the following whitespace after . should be selected
            /// </summary>
            [Fact]
            public void TextObject_AllSentence_MiddleWord()
            {
                Create("cat. dog. fish.");
                _textView.MoveCaretTo(6);
                _vimBuffer.Process("vas");
                Assert.Equal("dog. ", _textView.GetSelectionSpan().GetText());
                Assert.Equal(9, _textView.GetCaretPoint().Position);
            }

            [Fact]
            public void Issue1456()
            {
                Create("foo", "bar", "baz");

                _textView.MoveCaretTo(5);
                _vimBuffer.Process("vap");

                Assert.Equal(_textView.GetLineRange(0, 2).GetText(), _textView.GetSelectionSpan().GetText());
            }

            [Fact]
            public void Issue679()
            {
                Create(4, "  <div>", "\t<b>Reason:</b>", "\t@Model.Foo", "  </div>");
                _vimBuffer.ProcessNotation("<c-q>ljjjjx");
                Assert.Equal(new[]
                    {
                       "<div>",
                       "  <b>Reason:</b>",
                       "  @Model.Foo",
                       "</div>"
                    },
                    _textBuffer.GetLines());
            }

            [Fact]
            public void Issue903()
            {
                Create(4, "some line1", "\tsome line 2");
                _textView.MoveCaretTo(8);
                _vimBuffer.ProcessNotation("<c-q>j");
                Assert.Equal(new[]
                    {
                        _textBuffer.GetLineSpan(0, 8, 1),
                        _textBuffer.GetLineSpan(1, 5, 1)
                    },
                    _textView.Selection.SelectedSpans);
            }

            [Fact]
            public void Issue1213()
            {
                Create("hello world");
                _vimBuffer.ProcessNotation("v<c-c>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [Fact]
            public void Issue1317()
            {
                Create("hello world");
                _vimBuffer.ProcessNotation("vl");
                Assert.False(_vimBuffer.CanProcess(VimKey.LeftDrag));
            }

            [Fact]
            public void Issue1715()
            {
                Create(@"        public override void Name(List<object> parameter)
        {
            throw new NotImplementedException();
        }");
                var index = _textBuffer.GetLine(0).GetText().IndexOf('N');
                _textView.MoveCaretTo(index);
                _vimBuffer.Process("Vj%");
                Assert.Equal(_textBuffer.GetLineRange(startLine: 0, endLine: 3).ExtentIncludingLineBreak, _textView.GetSelectionSpan());
            }
        }

        public sealed class TextObjectTest : VisualModeIntegrationTest
        {
            [Fact]
            public void InnerBlockYankAndPasteIsLinewise()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation("vi}");
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
                _vimBuffer.ProcessNotation("y");
                Assert.True(UnnamedRegister.OperationKind.IsCharacterWise);
                _vimBuffer.ProcessNotation("p");
                Assert.Equal(
                    new[] { "   statement;", " statement;" },
                    _textBuffer.GetLineRange(startLine: 2, endLine: 3).Lines.Select(x => x.GetText()));
            }

            [Fact]
            public void InnerBlockShouldGoToEol()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");
                _textView.MoveCaretToLine(2);
                _vimBuffer.ProcessNotation("vi}");

                var column = _textView.GetCaretColumn();
                Assert.True(column.IsInsideLineBreak);
            }
        }

        public abstract class YankSelectionTest : VisualModeIntegrationTest
        {
            public sealed class BlockTest : YankSelectionTest
            {
                private void AssertRegister(params string[] lines)
                {
                    var data = UnnamedRegister.StringData;
                    Assert.True(data.IsBlock);
                    Assert.Equal(lines, ((StringData.Block)data).Item);
                }

                [Fact]
                public void Simple()
                {
                    Create("cat", "dog");
                    _vimBuffer.ProcessNotation("<c-q>ljy");
                    AssertRegister("ca", "do");
                }

                [Fact]
                public void SimpleNonZeroColumn()
                {
                    Create("cats", "dogs");
                    _vimBuffer.ProcessNotation("l<c-q>ljy");
                    AssertRegister("at", "og");
                }

                [Fact]
                public void SimpleWidthOneSelection()
                {
                    Create("cats", "dogs");
                    _vimBuffer.ProcessNotation("l<c-q>jy");
                    AssertRegister("a", "o");
                }

                [Fact]
                public void PartialTab()
                {
                    Create(4, "trucker", "\tdog");
                    _vimBuffer.ProcessNotation("ll<c-q>lljy");
                    AssertRegister("uck", "  d");
                }

                [Fact]
                public void CompleteTab()
                {
                    Create(4, "trucker", "\tdog");
                    _vimBuffer.ProcessNotation("<c-q>lllljy");
                    AssertRegister("truck", "\td");
                }

                [Fact]
                public void PartialTabInMiddleLine()
                {
                    Create(4, "trucker", "\tdog", "fisher");
                    _vimBuffer.ProcessNotation("ll<c-q>lljjy");
                    AssertRegister("uck", "  d", "she");
                }
            }

            public sealed class CharacterTest : YankSelectionTest
            {
                [Fact]
                public void InsideLineBreak()
                {
                    Create("cat dog", "bear");
                    _globalSettings.VirtualEdit = "onemore";
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("vllly");
                    Assert.Equal("dog" + Environment.NewLine, UnnamedRegister.StringValue);
                }

                /// <summary>
                /// When the caret ends on an empty line then that line is included when the
                /// yank is performed
                /// </summary>
                [Fact]
                public void EmptyLine()
                {
                    Create("the dog", "", "cat");
                    _textView.MoveCaretTo(4);
                    _vimBuffer.Process("vjy");
                    Assert.Equal("dog" + Environment.NewLine + Environment.NewLine, UnnamedRegister.StringValue);
                }

                /// <summary>
                /// The yank selection command should exit visual mode after the operation
                /// </summary>
                [Fact]
                public void ShouldExitVisualMode()
                {
                    Create("cat", "dog");
                    EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
                    _vimBuffer.Process("y");
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                    Assert.True(_textView.Selection.IsEmpty);
                }
            }

            public sealed class LineTest : YankSelectionTest
            {
                /// <summary>
                /// Ensure that after yanking and leaving Visual Mode that the proper value is
                /// maintained for LastVisualSelection.  It should be the selection before the command
                /// was executed
                /// </summary>
                [Theory]
                [PropertyData("VirtualEditOptions")]
                public void LastVisualSelectionWithVeOnemore(string virtualEdit)
                {
                    Create("cat", "dog", "fish");
                    var span = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak;
                    _globalSettings.VirtualEdit = virtualEdit;
                    SwitchEnterMode(ModeKind.VisualLine, span);
                    _vimBuffer.Process('y');
                    Assert.True(_vimTextBuffer.LastVisualSelection.IsSome());
                    Assert.Equal(span, _vimTextBuffer.LastVisualSelection.Value.VisualSpan.EditSpan.OverarchingSpan);
                }

                [Theory]
                [PropertyData("VirtualEditOptions")]
                public void ReselectLastVisual(string virtualEdit)
                {
                    Create("cat", "dog", "fish");
                    _globalSettings.VirtualEdit = virtualEdit;
                    _vimBuffer.Process("Vj");
                    var expected = _textView.GetSelectionSpan();
                    Assert.Equal(10, _textView.Selection.Start.Position.Difference(_textView.Selection.End.Position));
                    _vimBuffer.Process("y");
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                    _vimBuffer.Process("gv");
                    Assert.Equal(ModeKind.VisualLine, _vimBuffer.ModeKind);
                    Assert.Equal(expected, _textView.GetSelectionSpan());
                }

                /// <summary>
                /// The yank line selection command should exit visual mode after the operation
                /// </summary>
                [Fact]
                public void ShouldExitVisualMode()
                {
                    Create("cat", "dog");
                    EnterMode(ModeKind.VisualCharacter, _textView.GetLine(0).Extent);
                    _vimBuffer.Process("Y");
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                    Assert.True(_textView.Selection.IsEmpty);
                }
            }
        }
    }
}
