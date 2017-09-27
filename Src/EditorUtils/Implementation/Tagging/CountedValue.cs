using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using EditorUtils;
using Microsoft.VisualStudio.Text.Classification;

namespace EditorUtils.Implementation.Tagging
{
    /// <summary>
    /// This solves the same problem as CountedTagger but for IClassifier
    /// </summary>
    internal sealed class CountedValue<T> 
    {
        private readonly T _value;
        private readonly object _key;
        private readonly PropertyCollection _propertyCollection;
        private int _count;

        internal T Value
        {
            get { return _value; }
        }

        private CountedValue(
            PropertyCollection propertyCollection,
            object key,
            T value)
        {
            _value = value;
            _key = key;
            _propertyCollection = propertyCollection;
            _count = 1;
        }

        internal void Release()
        {
            _count--;
            if (_count == 0)
            {
                var disposable = _value as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
                _propertyCollection.RemoveProperty(_key);
            }
        }

        internal static CountedValue<T> GetOrCreate(
            PropertyCollection propertyCollection,
            object key,
            Func<T> createFunc)
        {
            CountedValue<T> countedValue;
            if (propertyCollection.TryGetPropertySafe(key, out countedValue))
            {
                countedValue._count++;
                return countedValue;
            }

            countedValue = new CountedValue<T>(propertyCollection, key, createFunc());
            propertyCollection[key] = countedValue;
            return countedValue;
        }
    }
}
