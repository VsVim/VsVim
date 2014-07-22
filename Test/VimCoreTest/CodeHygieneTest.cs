using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Vim.Extensions;
using Microsoft.FSharp.Core;   

namespace Vim.UnitTest
{
    /// <summary>
    /// Pedantic code hygiene tests for the code base
    /// </summary>
    public abstract class CodeHygieneTest
    {
        private readonly Assembly _testAssembly = typeof(CodeHygieneTest).Assembly;
        private readonly Assembly _sourceAssembly = typeof(Vim).Assembly;

        static bool IsDiscriminatedUnion(Type type)
        {
            var attribute = type.GetCustomAttributes(typeof(CompilationMappingAttribute), inherit: true);
            if (attribute == null || attribute.Length != 1)
            {
                return false;
            }

            var compilatioMappingAttribute = (CompilationMappingAttribute)attribute[0];
            var flags = compilatioMappingAttribute.SourceConstructFlags & ~SourceConstructFlags.NonPublicRepresentation;
            return flags == SourceConstructFlags.SumType;
        }

        /// <summary>
        /// Determine if this type is one that was embedded from FSharp.Core.dll
        /// </summary>
        static bool IsFSharpCore(Type type)
        {
            return type.FullName.StartsWith("Microsoft.FSharp", StringComparison.Ordinal);
        }

        public sealed class NamingTest : CodeHygieneTest
        {
            [Fact]
            public void TestNamespace()
            {
                const string prefix = "Vim.UnitTest.";
                foreach (var type in _testAssembly.GetTypes().Where(x => x.IsPublic))
                {
                    Assert.True(type.FullName.StartsWith(prefix, StringComparison.Ordinal));
                }
            }

            [Fact]
            public void CodeNamespace()
            {
                const string prefix = "Vim.";
                foreach (var type in typeof(IVim).Assembly.GetTypes())
                {
                    if (type.FullName.StartsWith("<Startup", StringComparison.Ordinal) ||
                        type.FullName.StartsWith("Microsoft.FSharp", StringComparison.Ordinal) ||
                        type.FullName.StartsWith("Microsoft.BuildSettings", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.True(type.FullName.StartsWith(prefix, StringComparison.Ordinal), string.Format("Type {0} has incorrect prefix", type.FullName));
                }
            }

            /// <summary>
            /// Make sure all discriminated unions in the code base have RequiresQualifiedAccess
            /// on them
            /// </summary>
            [Fact]
            public void RequiresQualifiedAccess()
            {
                var any = false;
                var list = new List<string>();
                var types = _sourceAssembly
                    .GetTypes()
                    .Where(IsDiscriminatedUnion)
                    .Where(x => !IsFSharpCore(x));
                foreach (var type in types)
                {
                    any = true;
                    var attrib = type.GetCustomAttributes(typeof(RequireQualifiedAccessAttribute), inherit: true);
                    if (attrib == null || attrib.Length != 1)
                    {
                        list.Add(string.Format("{0} does not have [<RequiresQualifiedAccess>]", type.Name));
                    }

                }

                Assert.True(any);
                var msg = list.Count == 0 
                    ? string.Empty
                    : list.Aggregate((x, y) => x + Environment.NewLine + y);
                Assert.True(0 == list.Count, msg);
            }
        }

        /// <summary>
        /// Simple code coverage checks that just don't merit and entire class to themselves
        /// </summary>
        public abstract class CodeCoverageTest : CodeHygieneTest
        {
            public sealed class Equality : CodeCoverageTest
            {
                private void Run<T>(T value, T otherValue)
                {
                    EqualityUtil.RunAll(EqualityUnit.Create(value)
                        .WithEqualValues(value)
                        .WithNotEqualValues(otherValue));
                }

                [Fact]
                public void DiscriminatedUnions()
                {
                    Run(BlockCaretLocation.BottomLeft, BlockCaretLocation.BottomRight);
                    Run(CaretColumn.NewInLastLine(0), CaretColumn.NewInLastLine(1));
                    Run(CaretColumn.NewInLastLine(0), CaretColumn.NewInLastLine(2));
                    Run(CaseSpecifier.IgnoreCase, CaseSpecifier.None);
                    Run(ChangeCharacterKind.Rot13, ChangeCharacterKind.ToggleCase);
                    Run(CharSearchKind.TillChar, CharSearchKind.ToChar);
                    Run(KeyRemapMode.Language, KeyRemapMode.Normal);
                    Run(DirectiveKind.If, DirectiveKind.Else);
                    Run(MagicKind.NoMagic, MagicKind.Magic);
                    Run(MatchingTokenKind.Braces, MatchingTokenKind.Brackets);
                    Run(MotionContext.AfterOperator, MotionContext.Movement);
                    Run(MotionKind.LineWise, MotionKind.CharacterWiseExclusive);
                    Run(NumberFormat.Alpha, NumberFormat.Decimal);
                    Run(NumberValue.NewAlpha('c'), NumberValue.NewDecimal(1));
                    Run(OperationKind.CharacterWise, OperationKind.LineWise);
                    Run(QuickFix.Next, QuickFix.Previous);
                    Run(RegisterOperation.Delete, RegisterOperation.Yank);
                    Run(SectionKind.Default, SectionKind.OnCloseBrace);
                    Run(SentenceKind.Default, SentenceKind.NoTrailingCharacters);
                    Run(SettingKind.Number, SettingKind.String);
                    Run(SelectionKind.Exclusive, SelectionKind.Inclusive);
                    Run(SettingValue.NewNumber(1), SettingValue.NewNumber(2));
                    Run(TextObjectKind.AlwaysCharacter, TextObjectKind.AlwaysLine);
                    Run(UnmatchedTokenKind.CurlyBracket, UnmatchedTokenKind.Paren);
                    Run(WordKind.BigWord, WordKind.NormalWord);
                }
            }
        }
    }
}
