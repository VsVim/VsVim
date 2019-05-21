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

            public Tuple<int, string, bool> CompletedValue { get; set; }

            public int? CompletedReturn { get; set; }

            public Tuple<int, string, bool> ProcessValue { get; set; }

            public int? ProcessReturn { get; set; }


            public void Beep()
            {
                BeepCount++;
            }

            public void Cancelled(int value)
            {
                CancelledValue = value;
            }

            public int Completed(int data, string command, bool wasMapped)
            {
                CompletedValue = Tuple.Create(data, command, wasMapped);
                return CompletedReturn ?? data;
            }

            public int ProcessCommand(int data, string command)
            {
                ProcessValue = Tuple.Create(data, command, false);
                return ProcessReturn ?? data;
            }
        }

        #endregion

        private readonly Client _client;
        private readonly IHistorySession<int, int> _historySession;
        private MappedBindData<int> _bindData;

        public HistorySessionTest()
        {
            _client = new Client() { HistoryList = new HistoryList(), RegisterMap = Vim.RegisterMap };
            _historySession = HistoryUtil.CreateHistorySession(_client, 0, "", null);
            _bindData = _historySession.CreateBindDataStorage().CreateMappedBindData();
        }

        public void ProcessNotation(string notation)
        {
            foreach (var keyInput in KeyNotationUtil.StringToKeyInputSet(notation).KeyInputs)
            {
                Process(keyInput);
            }
        }

        public void Process(KeyInput keyInput, bool wasMapped = false)
        {
            var keyInputData = KeyInputData.Create(keyInput, wasMapped);
            var result = _bindData.MappedBindFunction.Invoke(keyInputData);
            _bindData = result.IsNeedMoreInput
                ? ((MappedBindResult<int>.NeedMoreInput)result).MappedBindData
                : null;
        }

        public sealed class PasteTest : HistorySessionTest
        {
            [WpfFact]
            public void InPasteWait()
            {
                ProcessNotation("<C-R>");
                Assert.True(_historySession.InPasteWait);
            }

            [WpfFact]
            public void PasteComplete()
            {
                Vim.RegisterMap.GetRegister('c').UpdateValue("test");
                ProcessNotation("<C-R>c");
                Assert.Equal("test", _client.ProcessValue.Item2);
            }

            [WpfFact]
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
            [WpfFact]
            public void EnterCancels()
            {
                ProcessNotation("cat<C-r>");
                Assert.True(_historySession.InPasteWait);
                Assert.Equal("cat", _client.ProcessValue.Item2);
                ProcessNotation("<CR>");
                Assert.False(_historySession.InPasteWait);
                Assert.Equal("cat", _client.ProcessValue.Item2);
            }

            [WpfFact]
            public void CantUsePasteSpecialFirst()
            {
                ProcessNotation("cat<C-w>");
                Assert.False(_historySession.InPasteWait);
                Assert.Equal("", _client.ProcessValue.Item2);
            }
        }

        public sealed class ClearCommandLineTest : HistorySessionTest
        {
            [WpfFact]
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
