using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf.Implementation.Keyboard
{
    /// <summary>
    /// Useful class when you expect a collection to have very few elements in the standard
    /// case
    /// </summary>
    internal sealed class FrugalList<T>
    {
        private object _storage;

        internal FrugalList()
        {
            _storage = null;
        }

        internal FrugalList(T item)
        {
            _storage = item;
        }

        internal void Add(T value)
        {
            if (_storage == null)
            {
                _storage = value;
                return;
            }

            var list = _storage as List<T>;
            if (list == null)
            {
                list = new List<T>();
                list.Add((T)_storage);
                _storage = list;
            }

            list.Add(value);
        }

        internal IEnumerable<T> GetValues()
        {
            if (_storage == null)
            {
                yield break;
            }

            var list = _storage as List<T>;
            if (list != null)
            {
                foreach (var cur in list)
                {
                    yield return cur;
                }
            }
            else
            {
                yield return (T)_storage;
            }
        }
    }

    internal static class FrugalList
    {
        internal static FrugalList<T> Create<T>(T value)
        {
            return new FrugalList<T>(value);
        }
    }
}
