using System;
using System.Collections.Generic;

namespace EditorUtils.Implementation.Utilities
{
    internal static class EqualityUtility
    {
        internal sealed class DelegateComparer<T> : IEqualityComparer<T>
        {
            private Func<T, T, bool> _equalsFunc;
            private Func<T, int> _getHashCodeFunc;

            internal DelegateComparer(Func<T, T, bool> equalsFunc, Func<T, int> getHashCodeFunc)
            {
                _equalsFunc = equalsFunc;
                _getHashCodeFunc = getHashCodeFunc;
            }

            bool IEqualityComparer<T>.Equals(T x, T y)
            {
                return _equalsFunc(x, y);
            }

            int IEqualityComparer<T>.GetHashCode(T obj)
            {
                return _getHashCodeFunc(obj);
            }
        }

        internal static IEqualityComparer<T> Create<T>(
            Func<T, T, bool> equalsFunc,
            Func<T, int> getHashCodeFunc)
        {
            return new DelegateComparer<T>(equalsFunc, getHashCodeFunc);
        }
    }
}
