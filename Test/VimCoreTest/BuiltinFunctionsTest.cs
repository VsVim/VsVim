using System.Collections.Generic;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class BuiltinFunctionsTest
    {
        private readonly BuiltinFunctionCaller _callerUnderTest;
        private Dictionary<string, VariableValue> _variableMap;

        public BuiltinFunctionsTest()
        {
            _variableMap = new Dictionary<string, VariableValue>();
            _callerUnderTest = new BuiltinFunctionCaller(_variableMap);
        }

        [Fact]
        public void Exists_should_return_0_for_variable_that_does_not_exist()
        {
            var maybeValue = _callerUnderTest.Call(BuiltinFunctionCall.NewExists(VariableValue.NewString("x")));

            Assert.True(maybeValue.IsSome(VariableValue.NewNumber(0)));
        }

        [Fact]
        public void Exists_should_return_1_for_variable_that_does_exist()
        {
            _variableMap["foo"] = VariableValue.NewString("bar");

            var maybeValue = _callerUnderTest.Call(BuiltinFunctionCall.NewExists(VariableValue.NewString("foo")));

            Assert.True(maybeValue.IsSome(VariableValue.NewNumber(1)));
        }
    }
}
