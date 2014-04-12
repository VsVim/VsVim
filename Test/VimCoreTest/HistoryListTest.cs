using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class HistoryListTest 
    {
        private readonly HistoryList _historyList = new HistoryList();

        public sealed class LimitTest : HistoryListTest
        {
            [Fact]
            public void Default()
            {
                Assert.Equal(VimConstants.DefaultHistoryLength, _historyList.Limit);
            }

            [Fact]
            public void DontExceedTheLimit()
            {
                _historyList.Limit = 1;
                for (int i = 0; i < 100; i++)
                {
                    var msg = i.ToString();
                    _historyList.Add(msg);
                    Assert.Equal(1, _historyList.Count);
                    Assert.Equal(msg, _historyList.Items.First());
                }
            }

            /// <summary>
            /// The total count should represent the number of items ever added to the history
            /// list.  This is true irrespective of the total count of the list
            /// </summary>
            [Fact]
            public void TotalCount()
            {
                _historyList.Limit = 4;
                for (int i = 0; i < 100; i++)
                {
                    _historyList.Add("dog");
                    Assert.Equal(i + 1, _historyList.TotalCount);
                }
            }
        }

        public sealed class AddTest : HistoryListTest
        {
            /// <summary>
            /// The most recent item should be at the head of the list
            /// </summary>
            [Fact]
            public void MostRecentIsHead()
            {
                for (int i = 0; i < 10; i++)
                {
                    var msg = i.ToString();
                    _historyList.Add(msg);
                    Assert.Equal(msg, _historyList.First());
                }
            }

            /// <summary>
            /// An Add operation should remove all occurences of the string within the
            /// list and put the most recent one at the front of the list
            /// </summary>
            [Fact]
            public void RemovesPreviousUsages()
            {
                _historyList.AddRange("cat", "dog");
                _historyList.Add("cat");
                Assert.Equal(2, _historyList.Count);
                Assert.Equal(new [] { "cat", "dog" }, _historyList);
            }
        }
    }
}
