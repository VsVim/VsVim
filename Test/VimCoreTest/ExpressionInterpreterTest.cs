using System.Collections.Generic;
using Moq;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class ExpressionInterpreterTest
    {
        private readonly ExpressionInterpreter _interpreter;
        private readonly Mock<IStatusUtil> _statusUtil;

        public ExpressionInterpreterTest()
        {
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _interpreter = new ExpressionInterpreter(_statusUtil.Object, null, null, new Dictionary<string, VariableValue>(), null);
        }

        private VariableValue Run(string expr)
        {
            var parseResult = VimUtil.ParseExpression(expr);
            Assert.True(parseResult.IsSucceeded, "Expression failed to parse");
            return _interpreter.RunExpression(parseResult.AsSucceeded().Value);
        }

        private void Run(string expr, string expected)
        {
            var value = Run(expr);
            Assert.Equal(expected, value.AsString().String);
        }

        private void Run(string expr, int expected)
        {
            var value = Run(expr);
            Assert.Equal(expected, value.AsNumber().Number);
        }

        /// <summary>
        /// Add two numbers together and test the result
        /// </summary>
        [Fact]
        public void Add_SimpleNumber()
        {
            Run("1 + 2", 3);
        }

        [Fact]
        public void Concat_two_strings()
        {
            Run("'vs' . 'vim'", "vsvim");
        }

        [Fact]
        public void Concat_two_integers()
        {
            Run("2 . 3", "23");
        }

        [Fact]
        public void Empty_list()
        {
            Assert.True(Run("[]").AsList().VariableValues.IsEmpty);
        }

        [Fact]
        public void Run_builtin_function_of_no_arguments()
        {
            Assert.NotEqual(0, Run("localtime()").AsNumber().Number);
        }

        [Fact]
        public void Run_builtin_function_of_one_string_argument()
        {
            Run("exists('foo')", 0);
        }

        [Fact]
        public void Run_builtin_function_of_one_integer_argument()
        {
            Run("nr2char(64)", "@");
        }

        [Fact]
        public void Run_builtin_function_of_multiple_arguments()
        {
            Run(@"escape('C:/Program Files', ' ')", @"C:/Program\ Files");
        }
    }
}
