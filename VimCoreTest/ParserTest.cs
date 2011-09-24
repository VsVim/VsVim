using System;
using NUnit.Framework;
using Vim;
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

        private LineSpecifier ParseLineSpecifier(string text)
        {
            var parser = new Parser(text);
            var option = parser.ParseLineSpecifier();
            Assert.IsTrue(option.IsSome());
            return option.Value;
        }

        /// <summary>
        /// Make sure we can handle the count argument of :delete
        /// </summary>
        [Test]
        public void Parse_Delete_WithCount()
        {
            var lineCommand = ParseLineCommand("delete 2");
            Assert.AreEqual(2, lineCommand.AsDelete().Item3.Value);
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
        /// Make sure that we detect the trailing characters in the close command
        /// </summary>
        [Test]
        public void Parse_LineCommand_Close_Trailing()
        {
            var parseResult = Parser.ParseLineCommand("close foo");
            Assert.IsTrue(parseResult.IsFailed(Resources.CommandMode_TrailingCharacters));
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

        /// <summary>
        /// Make sure we can parse out a range of the current line and itself
        /// </summary>
        [Test]
        public void Parse_LineRange_RangeOfCurrentLine()
        {
            var lineRange = ParseLineRange(".,.");
            Assert.IsTrue(lineRange.AsRange().Item1.IsCurrentLine);
            Assert.IsTrue(lineRange.AsRange().Item2.IsCurrentLine);
            Assert.IsFalse(lineRange.AsRange().item3);
        }

        /// <summary>
        /// Make sure we can parse out a range of numbers
        /// </summary>
        [Test]
        public void Parse_LineRange_RangeOfNumbers()
        {
            var lineRange = ParseLineRange("1,2");
            Assert.IsTrue(lineRange.AsRange().Item1.IsNumber(1));
            Assert.IsTrue(lineRange.AsRange().Item2.IsNumber(2));
            Assert.IsFalse(lineRange.AsRange().item3);
        }

        /// <summary>
        /// Make sure we can parse out a range of numbers with the adjust caret 
        /// option specified
        /// </summary>
        [Test]
        public void Parse_LineRange_RangeOfNumbersWithAdjustCaret()
        {
            var lineRange = ParseLineRange("1;2");
            Assert.IsTrue(lineRange.AsRange().Item1.IsNumber(1));
            Assert.IsTrue(lineRange.AsRange().Item2.IsNumber(2));
            Assert.IsTrue(lineRange.AsRange().item3);
        }

        /// <summary>
        /// Ensure we can parse out a simple next pattern
        /// </summary>
        [Test]
        public void Parse_LineSpecifier_NextPattern()
        {
            var lineSpecifier = ParseLineSpecifier("/dog/");
            Assert.AreEqual("dog", lineSpecifier.AsNextLineWithPattern().Item);
        }

        /// <summary>
        /// Ensure we can parse out a simple previous pattern
        /// </summary>
        [Test]
        public void Parse_LineSpecifier_PreviousPattern()
        {
            var lineSpecifier = ParseLineSpecifier("?dog?");
            Assert.AreEqual("dog", lineSpecifier.AsPreviousLineWithPattern().Item);
        }

        /// <summary>
        /// When we pass in a full command name to try expand it shouldn't have any effect
        /// </summary>
        [Test]
        public void TryExpand_Full()
        {
            var parser = new Parser("");
            Assert.AreEqual("close", parser.TryExpand("close"));
        }

        /// <summary>
        /// Make sure the abbreviation can be expanded
        /// </summary>
        [Test]
        public void TryExpand_Abbrevation()
        {
            var parser = new Parser("");
            foreach (var tuple in Parser.s_LineCommandNamePair)
            {
                if (!String.IsNullOrEmpty(tuple.Item2))
                {
                    Assert.AreEqual(tuple.Item1, parser.TryExpand(tuple.Item2));
                }
            }
        }
    }
}
