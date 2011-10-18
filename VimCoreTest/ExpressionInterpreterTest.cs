using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Vim.Interpreter;
using Vim.UnitTest;
using Moq;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class ExpressionInterpreterTest
    {
        private ExpressionInterpreter _interpreter;
        private Mock<IStatusUtil> _statusUtil;

        [SetUp]
        public void Setup()
        {
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _interpreter = new ExpressionInterpreter(_statusUtil.Object);
        }

        private Value Run(string expr)
        {
            var parseResult = Parser.ParseExpression(expr);
            Assert.IsTrue(parseResult.IsSucceeded);
            return _interpreter.RunExpression(parseResult.AsSucceeded().Item);
        }

        private void Run(string expr, int number)
        {
            var value = Run(expr);
            Assert.AreEqual(number, value.AsNumber().Item);
        }

        /// <summary>
        /// Add two numbers together and test the result
        /// </summary>
        [Test]
        public void Add_SimpleNumber()
        {
            Run("1 + 2", 3);
        }
    }
}
