using System;
using System.Collections.Generic;
using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Moq;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;
using Microsoft.FSharp.Core;
using System.Threading;

namespace Vim.UnitTest
{
    public class CommonOperationsTest : VimTestBase
    {
        private TestableSynchronizationContext _context;
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private IFoldManager _foldManager;
        private IRegisterMap _registerMap;
        private MockRepository _factory;
        private Mock<IVimHost> _vimHost;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimLocalSettings> _localSettings;
        private Mock<IOutliningManager> _outlining;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IVimTextBuffer> _vimTextBuffer;
        private IUndoRedoOperations _undoRedoOperations;
        private ISearchService _searchService;
        private IVimData _vimData;
        private ICommonOperations _operations;
        private CommonOperations _operationsRaw;

        protected void Create(params string[] lines)
        {
            _context = new TestableSynchronizationContext();
            _textView = CreateTextView(lines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _textBuffer = _textView.TextBuffer;
            _foldManager = FoldManagerFactory.GetFoldManager(_textView);
            _factory = new MockRepository(MockBehavior.Strict);

            // Create the Vim instance with our Mock'd services
            _registerMap = Vim.RegisterMap;
            _vimHost = _factory.Create<IVimHost>();
            _vimHost.Setup(x => x.DoActionWhenTextViewReady(It.IsAny<FSharpFunc<Unit, Unit>>(), It.IsAny<ITextView>()))
                .Callback((FSharpFunc<Unit, Unit> action, ITextView textView) => action.Invoke(null));
            var globalSettings = Vim.GlobalSettings;
            globalSettings.Magic = true;
            globalSettings.SmartCase = false;
            globalSettings.IgnoreCase = true;
            globalSettings.VirtualEdit = "onemore";
            globalSettings.Selection = "inclusive";
            globalSettings.WrapScan = true;
            _vimData = new VimData(globalSettings);
            _searchService = new SearchService(TextSearchService, globalSettings);
            var vim = MockObjectFactory.CreateVim(
                registerMap: _registerMap,
                host: _vimHost.Object,
                settings: globalSettings,
                searchService: _searchService,
                factory: _factory);

            // Create the IVimTextBuffer instance with our Mock'd services
            _localSettings = MockObjectFactory.CreateLocalSettings(globalSettings, _factory);
            _localSettings.SetupGet(x => x.AutoIndent).Returns(false);
            _localSettings.SetupGet(x => x.GlobalSettings).Returns(globalSettings);
            _localSettings.SetupGet(x => x.ExpandTab).Returns(true);
            _localSettings.SetupGet(x => x.TabStop).Returns(4);
            _localSettings.SetupGet(x => x.ShiftWidth).Returns(2);
            _localSettings.SetupGet(x => x.IsKeyword).Returns("@,_,0-9");
            _localSettings.SetupGet(x => x.IsKeywordCharSet).Returns(VimCharSet.TryParse("@,_,0-9").Value);

            _statusUtil = _factory.Create<IStatusUtil>();
            _undoRedoOperations = VimUtil.CreateUndoRedoOperations(_statusUtil.Object);
            _vimTextBuffer = MockObjectFactory.CreateVimTextBuffer(
                _textBuffer,
                localSettings: _localSettings.Object,
                vim: vim.Object,
                undoRedoOperations: _undoRedoOperations,
                factory: _factory);
            _vimTextBuffer.SetupGet(x => x.UseVirtualSpace).Returns(false);
            _vimTextBuffer.SetupGet(x => x.InOneTimeCommand).Returns(FSharpOption<ModeKind>.None);

            // Create the VimBufferData instance with our Mock'd services
            _jumpList = _factory.Create<IJumpList>();
            var vimBufferData = CreateVimBufferData(
                _vimTextBuffer.Object,
                _textView,
                statusUtil: _statusUtil.Object,
                jumpList: _jumpList.Object);

            _outlining = _factory.Create<IOutliningManager>();
            _outlining
                .Setup(x => x.ExpandAll(It.IsAny<SnapshotSpan>(), It.IsAny<Predicate<ICollapsed>>()))
                .Returns<IEnumerable<ICollapsible>>(null);

            var commonOperationsFactory = _factory.Create<ICommonOperationsFactory>();
            var bulkOperations = CompositionContainer.GetExportedValue<IBulkOperations>();
            _operationsRaw = new CommonOperations(
                commonOperationsFactory.Object,
                vimBufferData,
                EditorOperationsFactoryService.GetEditorOperations(_textView),
                FSharpOption.Create(_outlining.Object),
                MouseDevice,
                bulkOperations);
            _operations = _operationsRaw;
            commonOperationsFactory.Setup(x => x.GetCommonOperations(vimBufferData)).Returns(_operations);
        }

        private static string CreateLinesWithLineBreak(params string[] lines)
        {
            return lines.Aggregate((x, y) => x + Environment.NewLine + y) + Environment.NewLine;
        }

        public sealed class MiscTest : CommonOperationsTest
        {

            /// <summary>
            /// Standard case of deleting several lines in the buffer
            /// </summary>
            [WpfFact]
            public void DeleteLines_Multiple()
            {
                Create("cat", "dog", "bear");
                _operations.DeleteLines(_textBuffer.GetLine(0), 2, VimUtil.MissingRegisterName);
                Assert.Equal(CreateLinesWithLineBreak("cat", "dog"), UnnamedRegister.StringValue);
                Assert.Equal("bear", _textView.GetLine(0).GetText());
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
            }

            /// <summary>
            /// Verify the deleting of lines where the count causes the deletion to cross 
            /// over a fold
            /// </summary>
            [WpfFact]
            public void DeleteLines_OverFold()
            {
                Create("cat", "dog", "bear", "fish", "tree");
                _foldManager.CreateFold(_textView.GetLineRange(1, 2));
                _operations.DeleteLines(_textBuffer.GetLine(0), 3, VimUtil.MissingRegisterName);
                Assert.Equal(CreateLinesWithLineBreak("cat", "dog", "bear", "fish"), UnnamedRegister.StringValue);
                Assert.Equal("tree", _textView.GetLine(0).GetText());
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
            }

            /// <summary>
            /// Verify the deleting of lines where the count causes the deletion to cross 
            /// over a fold which begins the deletion span
            /// </summary>
            [WpfFact]
            public void DeleteLines_StartOfFold()
            {
                Create("cat", "dog", "bear", "fish", "tree");
                _foldManager.CreateFold(_textView.GetLineRange(0, 1));
                _operations.DeleteLines(_textBuffer.GetLine(0), 2, VimUtil.MissingRegisterName);
                Assert.Equal(CreateLinesWithLineBreak("cat", "dog", "bear"), UnnamedRegister.StringValue);
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
            }

            [WpfFact]
            public void DeleteLines_Simple()
            {
                Create("foo", "bar", "baz", "jaz");
                _operations.DeleteLines(_textBuffer.GetLine(0), 1, VimUtil.MissingRegisterName);
                Assert.Equal("bar", _textView.GetLine(0).GetText());
                Assert.Equal("foo" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void DeleteLines_WithCount()
            {
                Create("foo", "bar", "baz", "jaz");
                _operations.DeleteLines(_textBuffer.GetLine(0), 2, VimUtil.MissingRegisterName);
                Assert.Equal("baz", _textView.GetLine(0).GetText());
                Assert.Equal("foo" + Environment.NewLine + "bar" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Delete the last line and make sure it actually deletes a line from the buffer
            /// </summary>
            [WpfFact]
            public void DeleteLines_LastLine()
            {
                Create("foo", "bar");
                _operations.DeleteLines(_textBuffer.GetLine(1), 1, VimUtil.MissingRegisterName);
                Assert.Equal("bar" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(1, _textView.TextSnapshot.LineCount);
                Assert.Equal("foo", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Ensure that a join of 2 lines which don't have any blanks will produce lines which
            /// are separated by a single space
            /// </summary>
            [WpfFact]
            public void Join_RemoveSpaces_NoBlanks()
            {
                Create("foo", "bar");
                _operations.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces);
                Assert.Equal("foo bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal(1, _textView.TextSnapshot.LineCount);
            }

            /// <summary>
            /// Ensure that we properly remove the leading spaces at the start of the next line if
            /// we are removing spaces
            /// </summary>
            [WpfFact]
            public void Join_RemoveSpaces_BlanksStartOfSecondLine()
            {
                Create("foo", "   bar");
                _operations.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces);
                Assert.Equal("foo bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal(1, _textView.TextSnapshot.LineCount);
            }

            /// <summary>
            /// Don't touch the spaces when we join without editing them
            /// </summary>
            [WpfFact]
            public void Join_KeepSpaces_BlanksStartOfSecondLine()
            {
                Create("foo", "   bar");
                _operations.Join(_textView.GetLineRange(0, 1), JoinKind.KeepEmptySpaces);
                Assert.Equal("foo   bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal(1, _textView.TextSnapshot.LineCount);
            }

            /// <summary>
            /// Do a join of 3 lines
            /// </summary>
            [WpfFact]
            public void Join_RemoveSpaces_ThreeLines()
            {
                Create("foo", "bar", "baz");
                _operations.Join(_textView.GetLineRange(0, 2), JoinKind.RemoveEmptySpaces);
                Assert.Equal("foo bar baz", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal(1, _textView.TextSnapshot.LineCount);
            }

            /// <summary>
            /// Ensure we can properly join an empty line
            /// </summary>
            [WpfFact]
            public void Join_RemoveSpaces_EmptyLine()
            {
                Create("cat", "", "dog", "tree", "rabbit");
                _operations.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces);
                Assert.Equal("cat ", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// No tabs is just a column offset
            /// </summary>
            [WpfFact]
            public void GetSpacesToColumn_NoTabs()
            {
                Create("hello world");
                Assert.Equal(2, _operationsRaw.GetSpacesToColumnNumber(_textBuffer.GetLine(0), 2));
            }

            /// <summary>
            /// Tabs count as tabstop spaces
            /// </summary>
            [WpfFact]
            public void GetSpacesToColumn_Tabs()
            {
                Create("\thello world");
                _localSettings.SetupGet(x => x.TabStop).Returns(4);
                Assert.Equal(5, _operationsRaw.GetSpacesToColumnNumber(_textBuffer.GetLine(0), 2));
            }

            /// <summary>
            /// Wide characters count double
            /// </summary>
            [WpfFact]
            public void GetSpacesToColumn_WideChars()
            {
                Create("\u3042\u3044\u3046\u3048\u304A");
                Assert.Equal(10, _operationsRaw.GetSpacesToColumnNumber(_textBuffer.GetLine(0), 5));
            }

            /// <summary>
            /// Non spacing characters are not taken into account
            /// </summary>
            [WpfFact]
            public void GetSpacesToColumn_NonSpacingChars()
            {
                // h̸ello̊​w̵orld
                Create("h\u0338ello\u030A\u200bw\u0335orld");
                Assert.Equal(10, _operationsRaw.GetSpacesToColumnNumber(_textBuffer.GetLine(0), 14));
            }

            /// <summary>
            /// Without any tabs this should be a straight offset
            /// </summary>
            [WpfFact]
            public void GetPointForSpaces_NoTabs()
            {
                Create("hello world");
                var column = _operationsRaw.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), 2);
                Assert.Equal(_textBuffer.GetPoint(2), column.StartPoint);
            }

            /// <summary>
            /// Count the tabs as a 'tabstop' value when calculating the Point
            /// </summary>
            [WpfFact]
            public void GetPointForSpaces_Tabs()
            {
                Create("\thello world");
                _localSettings.SetupGet(x => x.TabStop).Returns(4);
                var column = _operationsRaw.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), 5);
                Assert.Equal(_textBuffer.GetPoint(2), column.StartPoint);
            }

            /// <summary>
            /// Verify that we properly return the new line text for the first line
            /// </summary>
            [WpfFact]
            public void GetNewLineText_FirstLine()
            {
                Create("cat", "dog");
                Assert.Equal(Environment.NewLine, _operations.GetNewLineText(_textBuffer.GetPoint(0)));
            }

            /// <summary>
            /// Verify that we properly return the new line text for the first line when using a non
            /// default new line ending
            /// </summary>
            [WpfFact]
            public void GetNewLineText_FirstLine_LineFeed()
            {
                Create("cat", "dog");
                _textBuffer.Replace(new Span(0, 0), "cat\ndog");
                Assert.Equal("\n", _operations.GetNewLineText(_textBuffer.GetPoint(0)));
            }

            /// <summary>
            /// Verify that we properly return the new line text for middle lines
            /// </summary>
            [WpfFact]
            public void GetNewLineText_MiddleLine()
            {
                Create("cat", "dog", "bear");
                Assert.Equal(Environment.NewLine, _operations.GetNewLineText(_textBuffer.GetLine(1).Start));
            }

            /// <summary>
            /// Verify that we properly return the new line text for middle lines when using a non
            /// default new line ending
            /// </summary>
            [WpfFact]
            public void GetNewLineText_MiddleLine_LineFeed()
            {
                Create("");
                _textBuffer.Replace(new Span(0, 0), "cat\ndog\nbear");
                Assert.Equal("\n", _operations.GetNewLineText(_textBuffer.GetLine(1).Start));
            }

            /// <summary>
            /// Verify that we properly return the new line text for end lines
            /// </summary>
            [WpfFact]
            public void GetNewLineText_EndLine()
            {
                Create("cat", "dog", "bear");
                Assert.Equal(Environment.NewLine, _operations.GetNewLineText(_textBuffer.GetLine(2).Start));
            }

            /// <summary>
            /// Verify that we properly return the new line text for middle lines when using a non
            /// default new line ending
            /// </summary>
            [WpfFact]
            public void GetNewLineText_EndLine_LineFeed()
            {
                Create("");
                _textBuffer.Replace(new Span(0, 0), "cat\ndog\nbear");
                Assert.Equal("\n", _operations.GetNewLineText(_textBuffer.GetLine(2).Start));
            }

            [WpfFact]
            public void GoToDefinition1()
            {
                Create("foo");
                _jumpList.Setup(x => x.Add(_textView.GetCaretVirtualPoint())).Verifiable();
                _vimHost.Setup(x => x.GoToDefinition()).Returns(true);
                var res = _operations.GoToDefinition();
                Assert.True(res.IsSucceeded);
                _jumpList.Verify();
            }

            [WpfFact]
            public void GoToDefinition2()
            {
                Create("foo");
                _vimHost.Setup(x => x.GoToDefinition()).Returns(false);
                var res = _operations.GoToDefinition();
                Assert.True(res.IsFailed);
                Assert.Contains("foo", ((Result.Failed)res).Error);
            }

            /// <summary>
            /// Make sure we don't crash when nothing is under the cursor
            /// </summary>
            [WpfFact]
            public void GoToDefinition3()
            {
                Create("      foo");
                _vimHost.Setup(x => x.GoToDefinition()).Returns(false);
                var res = _operations.GoToDefinition();
                Assert.True(res.IsFailed);
            }

            [WpfFact]
            public void GoToDefinition4()
            {
                Create("  foo");
                _vimHost.Setup(x => x.GoToDefinition()).Returns(false);
                var res = _operations.GoToDefinition();
                Assert.True(res.IsFailed);
                Assert.Equal(Resources.Common_GotoDefNoWordUnderCursor, res.AsFailed().Error);
            }

            [WpfFact]
            public void GoToDefinition5()
            {
                Create("foo bar baz");
                _vimHost.Setup(x => x.GoToDefinition()).Returns(false);
                var res = _operations.GoToDefinition();
                Assert.True(res.IsFailed);
                Assert.Equal(Resources.Common_GotoDefFailed("foo"), res.AsFailed().Error);
            }

            /// <summary>
            /// Simple insertion of a single item into the ITextBuffer
            /// </summary>
            [WpfFact]
            public void Put_Single()
            {
                Create("dog", "cat");
                _operations.Put(_textView.GetLine(0).Start.Add(1), StringData.NewSimple("fish"), OperationKind.CharacterWise);
                Assert.Equal("dfishog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Put a block StringData value into the ITextBuffer over existing text
            /// </summary>
            [WpfFact]
            public void Put_BlockOverExisting()
            {
                Create("dog", "cat");
                _operations.Put(_textView.GetLine(0).Start, VimUtil.CreateStringDataBlock("a", "b"), OperationKind.CharacterWise);
                Assert.Equal("adog", _textView.GetLine(0).GetText());
                Assert.Equal("bcat", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Put a block StringData value into the ITextBuffer where the length of the values
            /// exceeds the number of lines in the ITextBuffer.  This will force the insert to create
            /// new lines to account for it
            /// </summary>
            [WpfFact]
            public void Put_BlockLongerThanBuffer()
            {
                Create("dog");
                _operations.Put(_textView.GetLine(0).Start.Add(1), VimUtil.CreateStringDataBlock("a", "b"), OperationKind.CharacterWise);
                Assert.Equal("daog", _textView.GetLine(0).GetText());
                Assert.Equal(" b", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// A linewise insertion for Block should just insert each value onto a new line
            /// </summary>
            [WpfFact]
            public void Put_BlockLineWise()
            {
                Create("dog", "cat");
                _operations.Put(_textView.GetLine(1).Start, VimUtil.CreateStringDataBlock("a", "b"), OperationKind.LineWise);
                Assert.Equal("dog", _textView.GetLine(0).GetText());
                Assert.Equal("a", _textView.GetLine(1).GetText());
                Assert.Equal("b", _textView.GetLine(2).GetText());
                Assert.Equal("cat", _textView.GetLine(3).GetText());
            }

            /// <summary>
            /// Put a single StringData instance linewise into the ITextBuffer. 
            /// </summary>
            [WpfFact]
            public void Put_LineWiseSingleWord()
            {
                Create("cat");
                _operations.Put(_textView.GetLine(0).Start, StringData.NewSimple("fish\n"), OperationKind.LineWise);
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("cat", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Do a put at the end of the ITextBuffer which is of a single StringData and is characterwise
            /// </summary>
            [WpfFact]
            public void Put_EndOfBufferSingleCharacterwise()
            {
                Create("cat");
                _operations.Put(_textView.GetEndPoint(), StringData.NewSimple("dog"), OperationKind.CharacterWise);
                Assert.Equal("catdog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Do a put at the end of the ITextBuffer linewise.  This is a corner case because the code has
            /// to move the final line break from the end of the StringData to the front.  Ensure that we don't
            /// keep the final \n in the inserted string because that will mess up the line count in the
            /// ITextBuffer
            /// </summary>
            [WpfFact]
            public void Put_EndOfBufferLinewise()
            {
                Create("cat");
                Assert.Equal(1, _textView.TextSnapshot.LineCount);
                _operations.Put(_textView.GetEndPoint(), StringData.NewSimple("dog\n"), OperationKind.LineWise);
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal(2, _textView.TextSnapshot.LineCount);
            }

            /// <summary>
            /// Do a put at the end of the ITextBuffer linewise.  Same as previous
            /// test but the buffer contains a trailing line break
            /// </summary>
            [WpfFact]
            public void Put_EndOfBufferLinewiseWithTrailingLineBreak()
            {
                Create("cat", "");
                Assert.Equal(2, _textView.TextSnapshot.LineCount);
                _operations.Put(_textView.GetEndPoint(), StringData.NewSimple("dog\n"), OperationKind.LineWise);
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal(3, _textView.TextSnapshot.LineCount);
            }

            /// <summary>
            /// Put into empty buffer should create a buffer with the contents being put
            /// </summary>
            [WpfFact]
            public void Put_IntoEmptyBuffer()
            {
                Create("");
                _operations.Put(_textView.GetLine(0).Start, StringData.NewSimple("fish\n"), OperationKind.LineWise);
                Assert.Equal("fish", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Only shift whitespace
            /// </summary>
            [WpfFact]
            public void ShiftLineRangeLeft1()
            {
                Create("foo");
                _operations.ShiftLineRangeLeft(_textBuffer.GetLineRange(0), 1);
                Assert.Equal("foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            }

            /// <summary>
            /// Don't puke on an empty line
            /// </summary>
            [WpfFact]
            public void ShiftLineRangeLeft2()
            {
                Create("");
                _operations.ShiftLineRangeLeft(_textBuffer.GetLineRange(0), 1);
                Assert.Equal("", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft3()
            {
                Create("  foo", "  bar");
                _operations.ShiftLineRangeLeft(_textBuffer.GetLineRange(0, 1), 1);
                Assert.Equal("foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal("bar", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(1).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft4()
            {
                Create("   foo");
                _operations.ShiftLineRangeLeft(_textBuffer.GetLineRange(0), 1);
                Assert.Equal(" foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft5()
            {
                Create("  a", "  b", "c");
                _operations.ShiftLineRangeLeft(_textBuffer.GetLineRange(0), 1);
                Assert.Equal("a", _textBuffer.GetLine(0).GetText());
                Assert.Equal("  b", _textBuffer.GetLine(1).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft6()
            {
                Create("   foo");
                _operations.ShiftLineRangeLeft(_textView.GetLineRange(0), 1);
                Assert.Equal(" foo", _textBuffer.GetLineRange(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft7()
            {
                Create(" foo");
                _operations.ShiftLineRangeLeft(_textView.GetLineRange(0), 400);
                Assert.Equal("foo", _textBuffer.GetLineRange(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft8()
            {
                Create("   foo", "    bar");
                _operations.ShiftLineRangeLeft(2);
                Assert.Equal(" foo", _textBuffer.GetLineRange(0).GetText());
                Assert.Equal("  bar", _textBuffer.GetLineRange(1).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft9()
            {
                Create(" foo", "   bar");
                _textView.MoveCaretTo(_textBuffer.GetLineRange(1).Start.Position);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal(" foo", _textBuffer.GetLineRange(0).GetText());
                Assert.Equal(" bar", _textBuffer.GetLineRange(1).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft10()
            {
                Create(" foo", "", "   bar");
                _operations.ShiftLineRangeLeft(3);
                Assert.Equal("foo", _textBuffer.GetLineRange(0).GetText());
                Assert.Equal("", _textBuffer.GetLineRange(1).GetText());
                Assert.Equal(" bar", _textBuffer.GetLineRange(2).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft11()
            {
                Create(" foo", "   ", "   bar");
                _operations.ShiftLineRangeLeft(3);
                Assert.Equal("foo", _textBuffer.GetLineRange(0).GetText());
                Assert.Equal(" ", _textBuffer.GetLineRange(1).GetText());
                Assert.Equal(" bar", _textBuffer.GetLineRange(2).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft_TabStartUsingSpaces()
            {
                Create("\tcat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(true);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("  cat", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Vim will actually normalize the line and then shift
            /// </summary>
            [WpfFact]
            public void ShiftLineRangeLeft_MultiTabStartUsingSpaces()
            {
                Create("\t\tcat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(true);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("      cat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft_TabStartUsingTabs()
            {
                Create("\tcat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("  cat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft_SpaceStartUsingTabs()
            {
                Create("    cat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("  cat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft_TabStartFollowedBySpacesUsingTabs()
            {
                Create("\t    cat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("\t  cat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft_SpacesStartFollowedByTabFollowedBySpacesUsingTabs()
            {
                Create("    \t    cat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("\t\t  cat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeLeft_SpacesStartFollowedByTabFollowedBySpacesUsingTabsWithModifiedTabStop()
            {
                Create("    \t    cat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _localSettings.SetupGet(x => x.TabStop).Returns(2);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("\t\t\t\tcat", _textView.GetLine(0).GetText());
            }
            [WpfFact]
            public void ShiftLineRangeLeft_ShortSpacesStartFollowedByTabFollowedBySpacesUsingTabs()
            {
                Create("  \t    cat");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _operations.ShiftLineRangeLeft(1);
                Assert.Equal("\t  cat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeRight1()
            {
                Create("foo");
                _operations.ShiftLineRangeRight(_textBuffer.GetLineRange(0), 1);
                Assert.Equal("  foo", _textBuffer.CurrentSnapshot.GetLineFromLineNumber(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeRight2()
            {
                Create("a", "b", "c");
                _operations.ShiftLineRangeRight(_textBuffer.GetLineRange(0), 1);
                Assert.Equal("  a", _textBuffer.GetLine(0).GetText());
                Assert.Equal("b", _textBuffer.GetLine(1).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeRight3()
            {
                Create("foo");
                _operations.ShiftLineRangeRight(1);
                Assert.Equal("  foo", _textBuffer.GetLineRange(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeRight4()
            {
                Create("foo", " bar");
                _operations.ShiftLineRangeRight(2);
                Assert.Equal("  foo", _textBuffer.GetLineRange(0).GetText());
                Assert.Equal("   bar", _textBuffer.GetLineRange(1).GetText());
            }

            /// <summary>
            /// Shift the line range right starting with the second line
            /// </summary>
            [WpfFact]
            public void ShiftLineRangeRight_SecondLine()
            {
                Create("foo", " bar");
                _textView.MoveCaretTo(_textBuffer.GetLineRange(1).Start.Position);
                _operations.ShiftLineRangeRight(1);
                Assert.Equal("foo", _textBuffer.GetLineRange(0).GetText());
                Assert.Equal("   bar", _textBuffer.GetLineRange(1).GetText());
            }

            /// <summary>
            /// Blank lines should expand when shifting right
            /// </summary>
            [WpfFact]
            public void ShiftLineRangeRight_ExpandBlank()
            {
                Create("foo", " ", "bar");
                _operations.ShiftLineRangeRight(3);
                Assert.Equal("  foo", _textBuffer.GetLineRange(0).GetText());
                Assert.Equal("   ", _textBuffer.GetLineRange(1).GetText());
                Assert.Equal("  bar", _textBuffer.GetLineRange(2).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeRight_NoExpandTab()
            {
                Create("cat", "dog");
                _localSettings.SetupGet(x => x.ShiftWidth).Returns(4);
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _operations.ShiftLineRangeRight(1);
                Assert.Equal("\tcat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeRight_NoExpandTabKeepSpacesWhenFewerThanTabStop()
            {
                Create("cat", "dog");
                _localSettings.SetupGet(x => x.ShiftWidth).Returns(2);
                _localSettings.SetupGet(x => x.TabStop).Returns(4);
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _operations.ShiftLineRangeRight(1);
                Assert.Equal("  cat", _textView.GetLine(0).GetText());
            }

            [WpfFact]
            public void ShiftLineRangeRight_SpacesStartUsingTabs()
            {
                Create("  cat", "dog");
                _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
                _localSettings.SetupGet(x => x.TabStop).Returns(2);
                _operations.ShiftLineRangeRight(1);
                Assert.Equal("\t\tcat", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure it shifts on the appropriate column and not column 0
            /// </summary>
            [WpfFact]
            public void ShiftLineBlockRight_Simple()
            {
                Create("cat", "dog");
                _operations.ShiftLineBlockRight(_textView.GetBlock(column: 1, length: 1, startLine: 0, lineCount: 2), 1);
                Assert.Equal("c  at", _textView.GetLine(0).GetText());
                Assert.Equal("d  og", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure it shifts on the appropriate column and not column 0
            /// </summary>
            [WpfFact]
            public void ShiftLineBlockLeft_Simple()
            {
                Create("c  at", "d  og");
                _operations.ShiftLineBlockLeft(_textView.GetBlock(column: 1, length: 1, startLine: 0, lineCount: 2), 1);
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure the caret column is maintained when specified going down
            /// </summary>
            [WpfFact]
            public void MaintainCaretColumn_Down()
            {
                Create("the dog chased the ball", "hello", "the cat climbed the tree");
                var motionResult = VimUtil.CreateMotionResult(
                    _textView.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(2),
                    flags: MotionResultFlags.MaintainCaretColumn);
                _operations.MoveCaretToMotionResult(motionResult);
                Assert.Equal(2, _operationsRaw.MaintainCaretColumn.AsSpaces().Count);
            }

            /// <summary>
            /// Make sure the caret column is kept when specified
            /// </summary>
            [WpfFact]
            public void SetCaretColumn()
            {
                Create("the dog chased the ball");
                var motionResult = VimUtil.CreateMotionResult(
                    _textView.GetFirstLine().ExtentIncludingLineBreak,
                    motionKind: MotionKind.CharacterWiseExclusive,
                    desiredColumn: CaretColumn.NewScreenColumn(100));
                _operations.MoveCaretToMotionResult(motionResult);
                Assert.Equal(100, _operationsRaw.MaintainCaretColumn.AsSpaces().Count);
            }


            /// <summary>
            /// If the MotionResult specifies end of line caret maintenance then it should
            /// be saved as that special value 
            /// </summary>
            [WpfFact]
            public void MaintainCaretColumn_EndOfLine()
            {
                Create("the dog chased the ball", "hello", "the cat climbed the tree");
                var motionResult = VimUtil.CreateMotionResult(
                    _textView.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(2),
                    flags: MotionResultFlags.MaintainCaretColumn | MotionResultFlags.EndOfLine);
                _operations.MoveCaretToMotionResult(motionResult);
                Assert.True(_operationsRaw.MaintainCaretColumn.IsEndOfLine);
            }

            /// <summary>
            /// Don't maintain the caret column if the maintain flag is not specified
            /// </summary>
            [WpfFact]
            public void MaintainCaretColumn_IgnoreIfFlagNotSpecified()
            {
                Create("the dog chased the ball", "hello", "the cat climbed the tree");
                var motionResult = VimUtil.CreateMotionResult(
                    _textView.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    motionKind: MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(2),
                    flags: MotionResultFlags.None);
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 1, 2),
                    true,
                    MotionKind.CharacterWiseInclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void MoveCaretToMotionResult2()
            {
                Create("foo", "bar", "baz");
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 1),
                    true,
                    MotionKind.CharacterWiseInclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void MoveCaretToMotionResult3()
            {
                Create("foo", "bar", "baz");
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 0),
                    true,
                    MotionKind.CharacterWiseInclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void MoveCaretToMotionResult4()
            {
                Create("foo", "bar", "baz");
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 3),
                    false,
                    MotionKind.CharacterWiseInclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            [WpfFact]
            public void MoveCaretToMotionResult6()
            {
                Create("foo", "bar", "baz");
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 1),
                    true,
                    MotionKind.CharacterWiseExclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Make sure we move to the empty last line if the flag is specified
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult_EmptyLastLine()
            {
                Create("foo", "bar", "");
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length),
                    true,
                    MotionKind.LineWise,
                    MotionResultFlags.IncludeEmptyLastLine);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(2, _textView.GetCaretPoint().GetContainingLine().LineNumber);
            }

            /// <summary>
            /// Don't move to the empty last line if it's not specified
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult_IgnoreEmptyLastLine()
            {
                Create("foo", "bar", "");
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length),
                    true,
                    MotionKind.LineWise,
                    MotionResultFlags.None);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(1, _textView.GetCaretPoint().GetContainingLine().LineNumber);
            }

            /// <summary>
            /// Need to respect the specified column
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult8()
            {
                Create("foo", "bar", "");
                var data = VimUtil.CreateMotionResult(
                    _textBuffer.GetLineRange(0, 1).Extent,
                    true,
                    MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(1));
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(Tuple.Create(1, 1), SnapshotPointUtil.GetLineNumberAndOffset(_textView.GetCaretPoint()));
            }

            /// <summary>
            /// Ignore column if it's past the end of the line
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult9()
            {
                Create("foo", "bar", "");
                Vim.GlobalSettings.VirtualEdit = "";
                var data = VimUtil.CreateMotionResult(
                    _textBuffer.GetLineRange(0, 1).Extent,
                    true,
                    MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(100));
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(Tuple.Create(1, 2), SnapshotPointUtil.GetLineNumberAndOffset(_textView.GetCaretPoint()));
            }

            /// <summary>
            /// "Need to respect the specified column
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult10()
            {
                Create("foo", "bar", "");
                var data = VimUtil.CreateMotionResult(
                    _textBuffer.GetLineRange(0, 1).Extent,
                    true,
                    MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(0));
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(Tuple.Create(1, 0), SnapshotPointUtil.GetLineNumberAndOffset(_textView.GetCaretPoint()));
            }

            /// <summary>
            /// "Reverse spans should move to the start of the span
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult11()
            {
                Create("dog", "cat", "bear");
                var data = VimUtil.CreateMotionResult(
                    _textBuffer.GetLineRange(0, 1).Extent,
                    false,
                    MotionKind.CharacterWiseInclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(Tuple.Create(0, 0), SnapshotPointUtil.GetLineNumberAndOffset(_textView.GetCaretPoint()));
            }

            /// <summary>
            /// Reverse spans should move to the start of the span and respect column
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult12()
            {
                Create("dog", "cat", "bear");
                var data = VimUtil.CreateMotionResult(
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    false,
                    MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(2));
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(Tuple.Create(0, 2), SnapshotPointUtil.GetLineNumberAndOffset(_textView.GetCaretPoint()));
            }

            /// <summary>
            /// Exclusive spans going backward should go through normal movements
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult14()
            {
                Create("dog", "cat", "bear");
                var data = VimUtil.CreateMotionResult(
                    _textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    false,
                    MotionKind.CharacterWiseExclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(_textBuffer.GetLine(0).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Used with the - motion
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult_ReverseLineWiseWithColumn()
            {
                Create(" dog", "cat", "bear");
                var data = VimUtil.CreateMotionResult(
                    span: _textView.GetLineRange(0, 1).ExtentIncludingLineBreak,
                    isForward: false,
                    motionKind: MotionKind.LineWise,
                    desiredColumn: CaretColumn.NewInLastLine(1));
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Spans going forward which have the AfterLastLine value should have the caret after the 
            /// last line
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult_CaretAfterLastLine()
            {
                Create("dog", "cat", "bear");
                var data = VimUtil.CreateMotionResult(
                    _textBuffer.GetLineRange(0).ExtentIncludingLineBreak,
                    true,
                    MotionKind.LineWise,
                    desiredColumn: CaretColumn.AfterLastLine);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Exclusive motions should not go to the end if it puts them into virtual space and 
            /// we don't have 've=onemore'
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult_InVirtualSpaceWithNoVirtualEdit()
            {
                Create("foo", "bar", "baz");
                Vim.GlobalSettings.VirtualEdit = "";
                var data = VimUtil.CreateMotionResult(
                    new SnapshotSpan(_textBuffer.CurrentSnapshot, 1, 2),
                    true,
                    MotionKind.CharacterWiseExclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// An exclusive selection should cause inclusive motions to be treated as
            /// if they were exclusive for caret movement
            /// </summary>
            [WpfFact]
            public void MoveCaretToMotionResult_InclusiveWithExclusiveSelection()
            {
                Create("the dog");
                Vim.GlobalSettings.Selection = "exclusive";
                _vimTextBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualBlock);
                var data = VimUtil.CreateMotionResult(_textBuffer.GetSpan(0, 3), motionKind: MotionKind.CharacterWiseInclusive);
                _operations.MoveCaretToMotionResult(data);
                Assert.Equal(3, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// If the point is within the current ITextBuffer then simply navigate to that particular
            /// point
            /// </summary>
            [WpfFact]
            public void NavigateToPoint_InBuffer()
            {
                Create("hello world");
                _operations.NavigateToPoint(new VirtualSnapshotPoint(_textBuffer.GetPoint(3)));
                Assert.Equal(3, _textView.GetCaretColumn().ColumnNumber);
            }

            /// <summary>
            /// If the point is inside another ITextBuffer then we need to defer to the IVimHost to
            /// do the navigation
            /// </summary>
            [WpfFact]
            public void NavigateToPoint_InOtherBuffer()
            {
                Create("hello world");
                var textBuffer = CreateTextBuffer("cat");
                var point = new VirtualSnapshotPoint(textBuffer.GetPoint(1));
                _vimHost.Setup(x => x.NavigateTo(point)).Returns(true).Verifiable();
                _operations.NavigateToPoint(point);
                _vimHost.Verify();
            }

            [WpfFact]
            public void Beep1()
            {
                Create(string.Empty);
                Vim.GlobalSettings.VisualBell = false;
                _vimHost.Setup(x => x.Beep()).Verifiable();
                _operations.Beep();
                _factory.Verify();
            }

            [WpfFact]
            public void Beep2()
            {
                Create(string.Empty);
                Vim.GlobalSettings.VisualBell = true;
                _operations.Beep();
                _factory.Verify();
            }

            /// <summary>
            /// Only once per line
            /// </summary>
            [WpfFact]
            public void Substitute1()
            {
                Create("bar bar", "foo");
                _operations.Substitute("bar", "again", _textView.GetLineRange(0), SubstituteFlags.None);
                Assert.Equal("again bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal("foo", _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
            }

            /// <summary>
            /// Should run on every line in the span
            /// </summary>
            [WpfFact]
            public void Substitute2()
            {
                Create("bar bar", "foo bar");
                _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 2))).Verifiable();
                _operations.Substitute("bar", "again", _textView.GetLineRange(0, 1), SubstituteFlags.None);
                Assert.Equal("again bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal("foo again", _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
                _statusUtil.Verify();
            }

            /// <summary>
            /// Replace all if the option is set
            /// </summary>
            [WpfFact]
            public void Substitute3()
            {
                Create("bar bar", "foo bar");
                _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
                _operations.Substitute("bar", "again", _textView.GetLineRange(0), SubstituteFlags.ReplaceAll);
                Assert.Equal("again again", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                Assert.Equal("foo bar", _textView.TextSnapshot.GetLineFromLineNumber(1).GetText());
                _statusUtil.Verify();
            }

            /// <summary>
            /// Ignore case
            /// </summary>
            [WpfFact]
            public void Substitute4()
            {
                Create("bar bar", "foo bar");
                _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.IgnoreCase);
                Assert.Equal("again bar", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            }

            /// <summary>
            /// Ignore case and replace all
            /// </summary>
            [WpfFact]
            public void Substitute5()
            {
                Create("bar bar", "foo bar");
                _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
                _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
                Assert.Equal("again again", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                _statusUtil.Verify();
            }

            /// <summary>
            /// Ignore case and replace all
            /// </summary>
            [WpfFact]
            public void Substitute6()
            {
                Create("bar bar", "foo bar");
                _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
                _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
                Assert.Equal("again again", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
                _statusUtil.Verify();
            }

            /// <summary>
            /// No matches
            /// </summary>
            [WpfFact]
            public void Substitute7()
            {
                Create("bar bar", "foo bar");
                var pattern = "BAR";
                _statusUtil.Setup(x => x.OnError(Resources.Common_PatternNotFound(pattern))).Verifiable();
                _operations.Substitute("BAR", "again", _textView.GetLineRange(0), SubstituteFlags.OrdinalCase);
                _statusUtil.Verify();
            }

            /// <summary>
            /// Invalid regex
            /// </summary>
            [WpfFact]
            public void Substitute8()
            {
                Create("bar bar", "foo bar");
                var original = _textView.TextSnapshot;
                var pattern = "(foo";
                _statusUtil.Setup(x => x.OnError(Resources.Common_PatternNotFound(pattern))).Verifiable();
                _operations.Substitute(pattern, "again", _textView.GetLineRange(0), SubstituteFlags.OrdinalCase);
                _statusUtil.Verify();
                Assert.Same(original, _textView.TextSnapshot);
            }

            /// <summary>
            /// Report only shouldn't make any changes
            /// </summary>
            [WpfFact]
            public void Substitute9()
            {
                Create("bar bar", "foo bar");
                var tss = _textView.TextSnapshot;
                _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 1))).Verifiable();
                _operations.Substitute("bar", "again", _textView.GetLineRange(0), SubstituteFlags.ReplaceAll | SubstituteFlags.ReportOnly);
                _statusUtil.Verify();
                Assert.Same(tss, _textView.TextSnapshot);
            }

            /// <summary>
            /// No matches and report only
            /// </summary>
            [WpfFact]
            public void Substitute10()
            {
                Create("bar bar", "foo bar");
                var tss = _textView.TextSnapshot;
                var pattern = "BAR";
                _operations.Substitute(pattern, "again", _textView.GetLineRange(0), SubstituteFlags.OrdinalCase | SubstituteFlags.ReportOnly);
            }

            /// <summary>
            /// Across multiple lines one match per line should be processed
            /// </summary>
            [WpfFact]
            public void Substitute11()
            {
                Create("cat", "bat");
                _statusUtil.Setup(x => x.OnStatus(Resources.Common_SubstituteComplete(2, 2))).Verifiable();
                _operations.Substitute("a", "o", _textView.GetLineRange(0, 1), SubstituteFlags.None);
                Assert.Equal("cot", _textView.GetLine(0).GetText());
                Assert.Equal("bot", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Respect the magic flag
            /// </summary>
            [WpfFact]
            public void Substitute12()
            {
                Create("cat", "bat");
                Vim.GlobalSettings.Magic = false;
                _operations.Substitute(".", "b", _textView.GetLineRange(0, 0), SubstituteFlags.Magic);
                Assert.Equal("bat", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Respect the nomagic flag
            /// </summary>
            [WpfFact]
            public void Substitute13()
            {
                Create("cat.", "bat");
                Vim.GlobalSettings.Magic = true;
                _operations.Substitute(".", "s", _textView.GetLineRange(0, 0), SubstituteFlags.Nomagic);
                Assert.Equal("cats", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Don't error when the pattern is not found if SuppressErrors is passed
            /// </summary>
            [WpfFact]
            public void Substitute14()
            {
                Create("cat", "bat");
                _operations.Substitute("z", "b", _textView.GetLineRange(0, 0), SubstituteFlags.SuppressError);
                _factory.Verify();
            }

            [WpfFact]
            public void GoToGlobalDeclaration1()
            {
                Create("foo bar");
                _jumpList.Setup(x => x.Add(It.IsAny<VirtualSnapshotPoint>()));
                _vimHost.Setup(x => x.GoToGlobalDeclaration(_textView, "foo")).Returns(true).Verifiable();
                _operations.GoToGlobalDeclaration();
                _vimHost.Verify();
            }

            [WpfFact]
            public void GoToGlobalDeclaration2()
            {
                Create("foo bar");
                _vimHost.Setup(x => x.GoToGlobalDeclaration(_textView, "foo")).Returns(false).Verifiable();
                _vimHost.Setup(x => x.Beep()).Verifiable();
                _operations.GoToGlobalDeclaration();
                _vimHost.Verify();
            }

            [WpfFact]
            public void GoToLocalDeclaration1()
            {
                Create("foo bar");
                _jumpList.Setup(x => x.Add(It.IsAny<VirtualSnapshotPoint>()));
                _vimHost.Setup(x => x.GoToLocalDeclaration(_textView, "foo")).Returns(true).Verifiable();
                _operations.GoToLocalDeclaration();
                _vimHost.Verify();
            }

            [WpfFact]
            public void GoToLocalDeclaration2()
            {
                Create("foo bar");
                _vimHost.Setup(x => x.GoToLocalDeclaration(_textView, "foo")).Returns(false).Verifiable();
                _vimHost.Setup(x => x.Beep()).Verifiable();
                _operations.GoToLocalDeclaration();
                _vimHost.Verify();
            }

            [WpfFact]
            public void GoToFile1()
            {
                Create("foo bar");
                _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
                _vimHost.Setup(x => x.LoadFileIntoExistingWindow("foo", _textView)).Returns(true).Verifiable();
                _operations.GoToFile();
                _vimHost.Verify();
            }

            [WpfFact]
            public void GoToFile2()
            {
                Create("foo bar");
                _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
                _vimHost.Setup(x => x.LoadFileIntoExistingWindow("foo", _textView)).Returns(false).Verifiable();
                _statusUtil.Setup(x => x.OnError(Resources.NormalMode_CantFindFile("foo"))).Verifiable();
                _operations.GoToFile();
                _statusUtil.Verify();
                _vimHost.Verify();
            }

            /// <summary>
            /// Make sure the appropriate error is raised if the buffer is dirty 
            /// </summary>
            [WpfFact]
            public void GoToFile_DirtyBuffer()
            {
                Create("foo bar");
                _vimHost.Setup(x => x.IsDirty(It.IsAny<ITextBuffer>())).Returns(true).Verifiable();
                _statusUtil.Setup(x => x.OnError(Resources.Common_NoWriteSinceLastChange)).Verifiable();
                _operations.GoToFile();
                _vimHost.Verify();
                _statusUtil.Verify();
            }

            /// <summary>
            /// If there is no match anywhere in the ITextBuffer raise the appropriate message
            /// </summary>
            [WpfFact]
            public void RaiseSearchResultMessages_NoMatch()
            {
                Create("");
                _statusUtil.Setup(x => x.OnError(Resources.Common_PatternNotFound("dog"))).Verifiable();
                _operations.RaiseSearchResultMessage(SearchResult.NewNotFound(
                    VimUtil.CreateSearchData("dog"),
                    false));
                _statusUtil.Verify();
            }

            /// <summary>
            /// If the match is not found but would be found if we enabled wrapping then raise
            /// a different message
            /// </summary>
            [WpfFact]
            public void RaiseSearchResultMessages_NoMatchInPathForward()
            {
                Create("");
                _statusUtil.Setup(x => x.OnError(Resources.Common_SearchHitBottomWithout("dog"))).Verifiable();
                _operations.RaiseSearchResultMessage(SearchResult.NewNotFound(
                    VimUtil.CreateSearchData("dog", SearchKind.Forward),
                    true));
                _statusUtil.Verify();
            }

            /// <summary>
            /// If the match is not found but would be found if we enabled wrapping then raise
            /// a different message
            /// </summary>
            [WpfFact]
            public void RaiseSearchResultMessages_NoMatchInPathBackward()
            {
                Create("");
                _statusUtil.Setup(x => x.OnError(Resources.Common_SearchHitTopWithout("dog"))).Verifiable();
                _operations.RaiseSearchResultMessage(SearchResult.NewNotFound(
                    VimUtil.CreateSearchData("dog", SearchKind.Backward),
                    true));
                _statusUtil.Verify();
            }

            /// <summary>
            /// Make sure that host indent trumps 'autoindent'
            /// </summary>
            [WpfFact]
            public void GetNewLineIndent_EditorTrumpsAutoIndent()
            {
                Create("cat", "dog", "");
                _vimHost.Setup(x => x.GetNewLineIndent(_textView, It.IsAny<ITextSnapshotLine>(), It.IsAny<ITextSnapshotLine>(), It.IsAny<IVimLocalSettings>())).Returns(FSharpOption.Create(8));
                var indent = _operations.GetNewLineIndent(_textView.GetLine(1), _textView.GetLine(2));
                Assert.Equal(8, indent.Value);
            }

            /// <summary>
            /// Use Vim settings if the 'useeditorindent' setting is not present
            /// </summary>
            [WpfFact]
            public void GetNewLineIndent_RevertToVimIndentIfEditorIndentFails()
            {
                Create("  cat", "  dog", "");
                _localSettings.SetupGet(x => x.AutoIndent).Returns(true);
                _vimHost.Setup(x => x.GetNewLineIndent(_textView, It.IsAny<ITextSnapshotLine>(), It.IsAny<ITextSnapshotLine>(), It.IsAny<IVimLocalSettings>())).Returns(FSharpOption<int>.None);
                var indent = _operations.GetNewLineIndent(_textView.GetLine(1), _textView.GetLine(2));
                Assert.Equal(2, indent.Value);
            }

            /// <summary>
            /// Make sure that we handle the case where the synchronization
            /// context isn't  set
            /// </summary>
            [WpfFact]
            public void BadSynchronizationContext()
            {
                // Install testable synchronization context.
                Create("cat", "dog", "");

                // Define a testable callback.
                var count = 0;
                Unit action(Unit arg)
                {
                    count += 1;
                    return null;
                }

                var oldContext = (TestableSynchronizationContext)SynchronizationContext.Current;
                try
                {
                    // Temporarily null out the current synchronization
                    // context.
                    SynchronizationContext.SetSynchronizationContext(null);

                    _operations.DoActionAsync(FSharpFuncUtil.Create<Unit, Unit>(action));

                    // The old context should not be accessible, but the
                    // function should still have been called.
                    Assert.True(oldContext.IsEmpty);
                    Assert.Equal(1, count);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(oldContext);
                }
            }
        }

        public sealed class SetRegisterValueTest : CommonOperationsTest
        {
            private static void AssertRegister(Register reg, string value, OperationKind kind)
            {
                Assert.Equal(value, reg.StringValue);
                Assert.Equal(kind, reg.RegisterValue.OperationKind);
            }

            private void AssertRegister(RegisterName name, string value, OperationKind kind)
            {
                AssertRegister(_registerMap.GetRegister(name), value, kind);
            }

            private void AssertRegister(char name, string value, OperationKind kind)
            {
                AssertRegister(_registerMap.GetRegister(name), value, kind);
            }

            public SetRegisterValueTest()
            {
                Create();
            }

            /// <summary>
            /// Delete of a singel line should update many registers
            /// </summary>
            [WpfFact]
            public void DeleteSingleLine()
            {
                var reg = _registerMap.GetRegister('c');
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Delete, new RegisterValue("foo bar\n", OperationKind.CharacterWise));
                AssertRegister(reg, "foo bar\n", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar\n", OperationKind.CharacterWise);
                AssertRegister('1', "foo bar\n", OperationKind.CharacterWise);
            }

            /// <summary>
            /// This shouldn't update the numbered registers since it was less than a line
            /// </summary>
            [WpfFact]
            public void DeletePartialLine()
            {
                var reg = _registerMap.GetRegister('c');
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Delete, new RegisterValue("foo bar", OperationKind.CharacterWise));
                AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
                AssertRegister('1', "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// A yank operation shouldn't update the SmallDelete register
            /// </summary>
            [WpfFact]
            public void Yank()
            {
                var reg = _registerMap.GetRegister('c');
                _registerMap.GetRegister(RegisterName.SmallDelete).UpdateValue("", OperationKind.CharacterWise);
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Yank, new RegisterValue("foo bar", OperationKind.CharacterWise));
                AssertRegister(reg, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Ensure the numbered registers are updated correctly for deletes
            /// </summary>
            [WpfFact]
            public void Numbered()
            {
                var reg = _registerMap.GetRegister('c');
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Delete, new RegisterValue("f\n", OperationKind.CharacterWise));
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Delete, new RegisterValue("o\n", OperationKind.CharacterWise));
                AssertRegister(reg, "o\n", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "o\n", OperationKind.CharacterWise);
                AssertRegister('1', "o\n", OperationKind.CharacterWise);
                AssertRegister('2', "f\n", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Ensure the small delete register isn't update when a named register is used 
            /// </summary>
            [WpfFact]
            public void IgnoreSmallDelete()
            {
                var reg = _registerMap.GetRegister('c');
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Delete, new RegisterValue("foo", OperationKind.CharacterWise));
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Ensure the small delete register is not updated when a delete occurs on the unnamed register
            /// </summary>
            [WpfFact]
            public void IgnoreSmallDeleteOnUnnamed()
            {
                var reg = _registerMap.GetRegister(RegisterName.Unnamed);
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Delete, new RegisterValue("foo", OperationKind.CharacterWise));
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
            }
            
            /// <summary>
            /// Ensure the small delete register is updated when a delete occurs without a specified register
            /// </summary>
            [WpfFact]
            public void UpdateSmallDeleteOnUnspecified()
            {
                _operations.SetRegisterValue(null, RegisterOperation.Delete, new RegisterValue("foo", OperationKind.CharacterWise));
                AssertRegister(RegisterName.SmallDelete, "foo", OperationKind.CharacterWise);
            }

            /// <summary>
            /// The SmallDelete register shouldn't update for a delete of multiple lines
            /// </summary>
            [WpfFact]
            public void DeleteOfMultipleLines()
            {
                _registerMap.GetRegister(RegisterName.SmallDelete).UpdateValue("", OperationKind.CharacterWise);
                var reg = _registerMap.GetRegister('c');
                var text = "cat" + Environment.NewLine + "dog";
                _operations.SetRegisterValue(reg.Name, RegisterOperation.Delete, new RegisterValue(text, OperationKind.CharacterWise));
                AssertRegister(RegisterName.SmallDelete, "", OperationKind.CharacterWise);
            }

            /// <summary>
            /// Deleting to the black hole register shouldn't affect unnamed or others
            /// </summary>
            [WpfFact]
            public void ForSpan_DeleteToBlackHole()
            {
                _registerMap.GetRegister(RegisterName.Blackhole).UpdateValue("", OperationKind.CharacterWise);
                _registerMap.GetRegister(RegisterName.NewNumbered(NumberedRegister.Number1)).UpdateValue("hey", OperationKind.CharacterWise);
                var namedReg = _registerMap.GetRegister('c');
                _operations.SetRegisterValue(namedReg.Name, RegisterOperation.Yank, new RegisterValue("foo bar", OperationKind.CharacterWise));
                _operations.SetRegisterValue(RegisterName.Blackhole, RegisterOperation.Delete, new RegisterValue("foo bar", OperationKind.CharacterWise));
                AssertRegister(namedReg, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Unnamed, "foo bar", OperationKind.CharacterWise);
                AssertRegister(RegisterName.NewNumbered(NumberedRegister.Number1), "hey", OperationKind.CharacterWise);
                AssertRegister(RegisterName.Blackhole, "", OperationKind.CharacterWise);
            }

            [WpfFact]
            public void MissingRegisterNameUpdatedUnamedEvenWithClipboardUnnamed()
            {
                Vim.GlobalSettings.ClipboardOptions = ClipboardOptions.Unnamed;
                _operations.SetRegisterValue(VimUtil.MissingRegisterName, RegisterOperation.Yank, new RegisterValue("dog", OperationKind.CharacterWise));
                Assert.Equal("dog", RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
                Assert.Equal("dog", RegisterMap.GetRegister(0).StringValue);
                Assert.Equal("dog", RegisterMap.GetRegister(RegisterName.NewSelectionAndDrop(SelectionAndDropRegister.Star)).StringValue);
            }

            [WpfFact]
            public void ExplicitUnnamedRegisterWithClipboardUnnamed()
            {
                Vim.GlobalSettings.ClipboardOptions = ClipboardOptions.Unnamed;
                var clipboardName = RegisterName.NewSelectionAndDrop(SelectionAndDropRegister.Star);
                RegisterMap.GetRegister(clipboardName).UpdateValue("cat");
                _operations.SetRegisterValue(FSharpOption.Create(RegisterName.Unnamed), RegisterOperation.Yank, new RegisterValue("dog", OperationKind.CharacterWise));
                Assert.Equal("dog", RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
                Assert.Equal("dog", RegisterMap.GetRegister(0).StringValue);
                Assert.Equal("cat", RegisterMap.GetRegister(RegisterName.NewSelectionAndDrop(SelectionAndDropRegister.Star)).StringValue);
            }

            [WpfFact]
            public void ExplicitStarRegisterWithClipboardUnnamed()
            {
                Vim.GlobalSettings.ClipboardOptions = ClipboardOptions.Unnamed;
                var clipboardName = RegisterName.NewSelectionAndDrop(SelectionAndDropRegister.Star);
                RegisterMap.GetRegister(0).UpdateValue("cat");
                _operations.SetRegisterValue(clipboardName, RegisterOperation.Yank, new RegisterValue("dog", OperationKind.CharacterWise));
                Assert.Equal("dog", RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
                Assert.Equal("cat", RegisterMap.GetRegister(0).StringValue);
                Assert.Equal("dog", RegisterMap.GetRegister(clipboardName).StringValue);
            }
        }
    }
}
