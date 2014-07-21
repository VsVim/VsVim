using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class HistorySessionTest : VimTestBase
    {
        #region Client

        public sealed class Client : IHistoryClient<int, int>
        {
            public HistoryList HistoryList { get; set; }

            public IRegisterMap RegisterMap { get; set; }

            public KeyRemapMode RemapMode { get; set; }

            public int BeepCount { get; set; }

            public int? CancelledValue { get; set; }

            public Tuple<int, string> CompletedValue { get; set; }

            public int? CompletedReturn { get; set; }

            public Tuple<int, string> ProcessValue { get; set; }

            public int? ProcessReturn { get; set; }


            public void Beep()
            {
                BeepCount++;
            }

            public void Cancelled(int value)
            {
                CancelledValue = value;
            }

            public int Completed(int data, string command)
            {
                CompletedValue = Tuple.Create(data, command);
                return CompletedReturn ?? data;
            }

            public int ProcessCommand(int data, string command)
            {
                ProcessValue = Tuple.Create(data, command);
                return ProcessReturn ?? data;
            }
        }

        #endregion

        private readonly Client _client;
        private readonly IHistorySession<int, int> _historySession;
        private BindData<int> _bindData;

        public HistorySessionTest()
        {
            _client = new Client() { HistoryList = new HistoryList(), RegisterMap = Vim.RegisterMap };
            _historySession = HistoryUtil.CreateHistorySession(_client, 0, "");
            _bindData = _historySession.CreateBindDataStorage().CreateBindData();
        }

        public void ProcessNotation(string notation)
        {
            foreach (var keyInput in KeyNotationUtil.StringToKeyInputSet(notation).KeyInputs)
            {
                Process(keyInput);
            }
        }

        public void Process(KeyInput keyInput)
        {
            var result = _bindData.BindFunction.Invoke(keyInput);
            _bindData = result.IsNeedMoreInput
                ? ((BindResult<int>.NeedMoreInput)result).Item
                : null;
        }

        public sealed class PasteTest : HistorySessionTest
        {
            [Fact]
            public void InPasteWait()
            {
                ProcessNotation("<C-R>");
                Assert.True(_historySession.InPasteWait);
            }

            [Fact]
            public void PasteComplete()
            {
                Vim.RegisterMap.GetRegister('c').UpdateValue("test");
                ProcessNotation("<C-R>c");
                Assert.Equal("test", _client.ProcessValue.Item2);
            }

            [Fact]
            public void ResetCommandCancelsPaste()
            {
                ProcessNotation("<C-R>");
                Assert.True(_historySession.InPasteWait);
                _historySession.ResetCommand("any");
                Assert.False(_historySession.InPasteWait);
            }

            /// <summary>
            /// Enter has no register hence it implicitly cancels the paste
            /// </summary>
            [Fact]
            public void EnterCancels()
            {
                ProcessNotation("cat<C-r>");
                Assert.True(_historySession.InPasteWait);
                Assert.Equal("cat", _client.ProcessValue.Item2);
                ProcessNotation("<CR>");
                Assert.False(_historySession.InPasteWait);
                Assert.Equal("cat", _client.ProcessValue.Item2);
            }
        }

        public sealed class ClearCommandLineTest : HistorySessionTest
        {
            [Fact]
            public void Simple()
            {
                ProcessNotation("cat");
                Assert.Equal("cat", _client.ProcessValue.Item2);
                ProcessNotation("<c-u>");
                Assert.Equal("", _client.ProcessValue.Item2);
            }
        }
    }
}
