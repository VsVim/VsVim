using System.Collections.Generic;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class BuiltinFunctionsTest
    {
        private readonly BuiltinFunctionCaller _callerUnderTest;
        private readonly Dictionary<string, VariableValue> _variableMap;

        public BuiltinFunctionsTest()
        {
            _variableMap = new Dictionary<string, VariableValue>();
            _callerUnderTest = new BuiltinFunctionCaller(_variableMap);
        }

        [Fact]
        public void Exists_should_return_0_for_variable_that_does_not_exist()
        {
            var value = _callerUnderTest.Call(BuiltinFunctionCall.NewExists("x"));

            Assert.Equal(VariableValue.NewNumber(0), value);
        }

        [Fact]
        public void Exists_should_return_1_for_variable_that_does_exist()
        {
            _variableMap["foo"] = VariableValue.NewString("bar");

            var value = _callerUnderTest.Call(BuiltinFunctionCall.NewExists("foo"));

            Assert.Equal(VariableValue.NewNumber(1), value);
        }

        [Fact]
        public void Localtime_should_return_current_Unix_time()
        {
            var value = _callerUnderTest.Call(BuiltinFunctionCall.Localtime);

            Assert.NotEqual(VariableValue.NewNumber(0), value);
        }

        [Fact]
        public void Escape_should_escape_the_specified_characters_in_a_string_with_backslash()
        {
            var value = _callerUnderTest.Call(BuiltinFunctionCall.NewEscape(@"C:\Program Files", @" \"));

            Assert.Equal(VariableValue.NewString(@"C:\\Program\ Files"), value);
        }

        [Fact]
        public void Nr2char_should_return_the_ASCII_codepoint_for_the_integer()
        {
            var value1 = _callerUnderTest.Call(BuiltinFunctionCall.NewNr2char(32));
            var value2 = _callerUnderTest.Call(BuiltinFunctionCall.NewNr2char(64));
            Assert.Equal(VariableValue.NewString(" "), value1);
            Assert.Equal(VariableValue.NewString("@"), value2);
        }
    }
}
