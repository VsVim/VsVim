using System.Collections.Generic;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class BuiltinFunctionsTest
    {
        private BuiltinFunctionCaller _callerUnderTest;
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
    }
}
