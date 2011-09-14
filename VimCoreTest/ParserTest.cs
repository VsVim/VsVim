using NUnit.Framework;
using Vim.Extensions;
using Vim.Interpreter;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    public sealed class ParserTest
    {
        private LineCommand ParseLineCommand(string text)
        {
            var parseResult = Parser.ParseLineCommand(text);
            Assert.IsTrue(parseResult.IsSucceeded);
            return parseResult.AsSucceeded().Item;
        }

        private LineRange ParseLineRange(string text)
        {
            var parser = new Parser(text);
            var option = parser.ParseLineRange();
            Assert.IsTrue(option.IsSome());
            return option.Value;
        }

        /// <summary>
        /// Make sure we can parse out the close command
        /// </summary>
        [Test]
        public void Parse_LineCommand_Close_NoBang()
        {
            var command = ParseLineCommand("close");
            Assert.IsTrue(command.IsClose);
        }
        /// <summary>
        /// Make sure we can parse out the close wit bang
        /// </summary>
        [Test]
        public void Parse_LineCommand_Close_WithBang()
        {
            var command = ParseLineCommand("close!");
            Assert.IsTrue(command.IsClose);
            Assert.IsTrue(command.AsClose().Item);
        }

        /// <summary>
        /// Make sure we can parse out the '%' range
        /// </summary>
        [Test]
        public void Parse_LineRange_EntireBuffer()
        {
            var lineRange = ParseLineRange("%");
            Assert.IsTrue(lineRange.IsEntireBuffer);
        }

        /// <summary>
        /// Make sure we can parse out a single line number range
        /// </summary>
        [Test]
        public void Parse_LineRange_SingleLineNumber()
        {
            var lineRange = ParseLineRange("42");
            Assert.IsTrue(lineRange.IsSingleLine);
            Assert.IsTrue(lineRange.AsSingleLine().Item.IsNumber(42));
        }
    }
}
