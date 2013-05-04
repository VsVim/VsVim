using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Vim.UI.Wpf.Implementation.Keyboard;

namespace Vim.UI.Wpf.UnitTest
{
    public sealed class FrugalListTest
    {
        private void AssertList<T>(FrugalList<T> list, params T[] contents)
        {
            Assert.True(contents.SequenceEqual(list.GetValues()));
        }

        [Fact]
        public void CreateSingle()
        {
            var list = FrugalList.Create(42);
            AssertList(list, 42);
        }

        [Fact]
        public void CreateEmpty()
        {
            var list = new FrugalList<int>();
            AssertList(list);
        }

        [Fact]
        public void AddMany()
        {
            var list = new FrugalList<int>();
            var range = Enumerable.Range(0, 5);
            foreach (var cur in range)
            {
                list.Add(cur);
            }
            AssertList(list, range.ToArray());
        }

        [Fact]
        public void AddOne()
        {
            var list = new FrugalList<int>();
            list.Add(42);
            AssertList(list, 42);
        }

        [Fact]
        public void AddTwo()
        {
            var list = new FrugalList<int>();
            list.Add(1);
            list.Add(2);
            AssertList(list, 1, 2);
        }

        [Fact]
        public void AddExhaustive()
        {
            const int count = 20;
            var list = new FrugalList<int>();
            var range = Enumerable.Range(0, count).ToArray();
            for (int i = 0; i < range.Length; i++)
            {
                list.Add(range[i]);
                AssertList(list, Enumerable.Range(0, i + 1).ToArray());
            }
        }
    }
}
