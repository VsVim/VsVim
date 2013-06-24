using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public abstract class TextChangeTest
    {
        public sealed class InsertTextTest : TextChangeTest
        {
            /// <summary>
            /// The InsertText for a simple insert is just the text
            /// </summary>
            [Fact]
            public void Simple()
            {
                var textChange = TextChange.NewInsert("dog");
                Assert.Equal("dog", textChange.InsertText.Value);
            }

            /// <summary>
            /// Combined inserts should just combine the values
            /// </summary>
            [Fact]
            public void CombinedInsert()
            {
                var textChange = TextChange.NewCombination(
                    TextChange.NewInsert("hello "),
                    TextChange.NewInsert("world"));
                Assert.Equal("hello world", textChange.InsertText.Value);
            }

            /// <summary>
            /// A naked delete should not have an InsertText
            /// </summary>
            [Fact]
            public void DeleteLeft()
            {
                var textChange = TextChange.NewDeleteLeft(1);
                Assert.True(textChange.InsertText.IsNone());
            }

            /// <summary>
            /// If the delete doesn't remove all of the text then there is still an insert
            /// </summary>
            [Fact]
            public void SmallDeleteLeft()
            {
                var textChange = TextChange.NewCombination(
                    TextChange.NewInsert("dogs"),
                    TextChange.NewDeleteLeft(1));
                Assert.Equal("dog", textChange.InsertText.Value);
            }

            /// <summary>
            /// If the delete removes all of the text then there is no insert
            /// </summary>
            [Fact]
            public void BigDeleteLeft()
            {
                var textChange = TextChange.NewCombination(
                    TextChange.NewInsert("dogs"),
                    TextChange.NewDeleteLeft(10));
                Assert.True(textChange.InsertText.IsNone());
            }
        }

        public abstract class ReduceTest : TextChangeTest
        {
            /// <summary>
            /// There are a lot of empty states which can be produced by the reduce 
            /// routines.  We should empty them out to 0 
            /// </summary>
            public sealed class EmptyTest : ReduceTest
            {
                /// <summary>
                /// The insert + delete here should collapse to an empty Insert which should
                /// simply be resolved out to nothing by the 
                /// </summary>
                [Fact]
                public void InsertAndDeleteLeft()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewCombination(
                            TextChange.NewInsert("cat"),
                            TextChange.NewDeleteLeft(3)),
                        TextChange.NewDeleteRight(3));
                    Assert.Equal(3, textChange.AsDeleteRight().Item);
                }

                /// <summary>
                /// The insert + delete here should collapse to an empty Insert which should
                /// simply be resolved out to nothing by the 
                /// </summary>
                [Fact]
                public void InsertAndDeleteLeftReverse()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewDeleteRight(3),
                        TextChange.NewCombination(
                            TextChange.NewInsert("cat"),
                            TextChange.NewDeleteLeft(3)));
                    Assert.Equal(3, textChange.AsDeleteRight().Item);
                }
            }

            public sealed class MiscTest : ReduceTest
            {

                [Fact]
                public void DoubleInsert()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewInsert("a"),
                        TextChange.NewInsert("b"));
                    Assert.Equal("ab", textChange.AsInsert().Item);
                }

                [Fact]
                public void DoubleDeleteLeft()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewDeleteLeft(5),
                        TextChange.NewDeleteLeft(6));
                    Assert.Equal(11, textChange.AsDeleteLeft().Item);
                }

                [Fact]
                public void DoubleDeleteRight()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewDeleteRight(5),
                        TextChange.NewDeleteRight(6));
                    Assert.Equal(11, textChange.AsDeleteRight().Item);
                }

                [Fact]
                public void InsertThenDeletePartial()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewInsert("cat"),
                        TextChange.NewDeleteLeft(2));
                    Assert.Equal("c", textChange.AsInsert().Item);
                }

                [Fact]
                public void InsertThenDeleteMore()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewInsert("cat"),
                        TextChange.NewDeleteLeft(4));
                    Assert.Equal(1, textChange.AsDeleteLeft().Item);
                }

                [Fact]
                public void InsertThenDeleteExact()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewInsert("cat"),
                        TextChange.NewDeleteLeft(3));
                    Assert.Equal("", textChange.AsInsert().Item);
                }

                /// <summary>
                /// The tree for Issue 1108 can be constructed 2 ways.  Left or right heavy.  This 
                /// does the opposite way to make sure we get the same answer
                /// </summary>
                [Fact]
                public void Issue1108Reversed()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewCombination(
                            TextChange.NewInsert("pr"),
                            TextChange.NewDeleteLeft(2)),
                        TextChange.NewInsert("protected"));
                    Assert.Equal("protected", textChange.AsInsert().Item);
                }

                /// <summary>
                /// This is a pretty typical intellisense pattern.  Need to make sure we 
                /// can delete it down to the simple resulting insert
                /// </summary>
                [Fact]
                public void Issue1108()
                {
                    var textChange = TextChange.CreateReduced(
                        TextChange.NewInsert("pr"),
                        TextChange.NewCombination(
                            TextChange.NewDeleteLeft(2),
                            TextChange.NewInsert("protected")));
                    Assert.Equal("protected", textChange.AsInsert().Item);
                }
            }
        }
    }
}
