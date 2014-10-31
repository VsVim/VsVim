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
            // This is where testing F# with C# gets ugly...
            var maybeExpression = _callerUnderTest.Call(BuiltinFunctionCall.NewExists(Expression.NewConstantValue(VariableValue.NewString("x"))));

            Assert.True(maybeExpression.IsSome(Expression.NewConstantValue(VariableValue.NewNumber(0))));
        }
    }
}
