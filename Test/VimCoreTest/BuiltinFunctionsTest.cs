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
            var value = _callerUnderTest.Call(BuiltinFunctionCall.NewExists(VariableValue.NewString("x")));

            Assert.Equal(VariableValue.NewNumber(0), value);
        }

        [Fact]
        public void Exists_should_return_1_for_variable_that_does_exist()
        {
            _variableMap["foo"] = VariableValue.NewString("bar");

            var value = _callerUnderTest.Call(BuiltinFunctionCall.NewExists(VariableValue.NewString("foo")));

            Assert.Equal(VariableValue.NewNumber(1), value);
        }

        [Fact]
        public void Localtime_should_return_current_Unix_time()
        {
            var value = _callerUnderTest.Call(BuiltinFunctionCall.Localtime);

            Assert.NotEqual(VariableValue.NewNumber(0), value);
        }
    }
}
