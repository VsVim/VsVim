using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NUnit.Framework;

namespace Vim.UnitTest
{
    public class EqualityUnit<T>
    {
        private static readonly ReadOnlyCollection<T> EmptyCollection = new ReadOnlyCollection<T>(new T[] { });
 
        public readonly T Value;
        public readonly ReadOnlyCollection<T> EqualValues;
        public readonly ReadOnlyCollection<T> NotEqualValues;
        public IEnumerable<T> AllValues
        {
            get { return Enumerable.Repeat(Value, 1).Concat(EqualValues).Concat(NotEqualValues); }
        }
 
        public EqualityUnit(T value)
        {
            Value = value;
            EqualValues = EmptyCollection;
            NotEqualValues = EmptyCollection;
        }
 
        public EqualityUnit(
            T value,
            ReadOnlyCollection<T> equalValues,
            ReadOnlyCollection<T> notEqualValues)
        {
            Value = value;
            EqualValues = equalValues;
            NotEqualValues = notEqualValues;
        }
 
        public EqualityUnit<T> WithEqualValues(params T[] equalValues)
        {
            return new EqualityUnit<T>(
                Value,
                EqualValues.Concat(equalValues).ToList().AsReadOnly(),
                NotEqualValues);
        }
 
        public EqualityUnit<T> WithNotEqualValues(params T[] notEqualValues)
        {
            return new EqualityUnit<T>(
                Value,
                EqualValues,
                NotEqualValues.Concat(notEqualValues).ToList().AsReadOnly());
        }
    }
 
    public static class EqualityUnit
    {
        public static EqualityUnit<T> Create<T>(T value)
        {
            return new EqualityUnit<T>(value);
        }
    }
 
    /// <summary>
    /// Base class which does a lot of the boiler plate work for testing that the equality pattern
    /// is properly implemented in objects
    /// </summary>
    public sealed class EqualityUtil<T>
    {
        private readonly ReadOnlyCollection<EqualityUnit<T>> _equalityUnits;
        private readonly Func<T, T, bool> _compareWithEqualityOperator;
        public readonly Func<T, T, bool> _compareWithInequalityOperator;
 
        public EqualityUtil(
            IEnumerable<EqualityUnit<T>> equalityUnits,
            Func<T, T, bool> compEquality,
            Func<T, T, bool> compInequality)
        {
            _equalityUnits = equalityUnits.ToList().AsReadOnly();
            _compareWithEqualityOperator = compEquality;
            _compareWithInequalityOperator = compInequality;
        }
 
        public void RunAll(
            bool skipOperators = false,
            bool skipEquatable = false)
        {
            if (!skipOperators)
            {
                EqualityOperator();
                EqualityOperatorCheckNull();
                InEqualityOperator();
                InEqualityOperatorCheckNull();
            }

            if (!skipEquatable)
            {
                ImplementsIEquatable();
                EquatableEquals();
                EquatableEqualsCheckNull();
            }

            ObjectEquals();
            ObjectEqualsCheckNull();
            ObjectEqualsDifferentType();
            GetHashCodeSemantics();
        }
 
        private void EqualityOperator()
        {
            foreach (var unit in _equalityUnits)
            {
                foreach (var value in unit.EqualValues)
                {
                    Assert.IsTrue(_compareWithEqualityOperator(unit.Value, value));
                    Assert.IsTrue(_compareWithEqualityOperator(value, unit.Value));
                }
 
                foreach (var value in unit.NotEqualValues)
                {
                    Assert.IsFalse(_compareWithEqualityOperator(unit.Value, value));
                    Assert.IsFalse(_compareWithEqualityOperator(value, unit.Value));
                }
            }
        }
 
        private void EqualityOperatorCheckNull()
        {
            if (typeof(T).IsValueType)
            {
                return;
            }
 
            foreach (var value in _equalityUnits.SelectMany(x => x.AllValues))
            {
                if ( !Object.ReferenceEquals(value, null) )
                {
                    Assert.IsFalse(_compareWithEqualityOperator(default(T), value));
                    Assert.IsFalse(_compareWithEqualityOperator(value, default(T)));
                }
            }
        }
 
        private void InEqualityOperator()
        {
            foreach (var unit in _equalityUnits)
            {
                foreach (var value in unit.EqualValues)
                {
                    Assert.IsFalse(_compareWithInequalityOperator(unit.Value, value));
                    Assert.IsFalse(_compareWithInequalityOperator(value, unit.Value));
                }
 
                foreach (var value in unit.NotEqualValues)
                {
                    Assert.IsTrue(_compareWithInequalityOperator(unit.Value, value));
                    Assert.IsTrue(_compareWithInequalityOperator(value, unit.Value));
                }
            }
        }
 
        private void InEqualityOperatorCheckNull()
        {
            if (typeof(T).IsValueType)
            {
                return;
            }
            foreach (var value in _equalityUnits.SelectMany(x => x.AllValues))
            {
                if ( !Object.ReferenceEquals(value, null) )
                {
                    Assert.IsTrue(_compareWithInequalityOperator(default(T), value));
                    Assert.IsTrue(_compareWithInequalityOperator(value, default(T)));
                }
            }
        }
 
        private void ImplementsIEquatable()
        {
            var type = typeof(T);
            var targetType = typeof(IEquatable<T>);
            Assert.IsTrue(type.GetInterfaces().Contains(targetType));
        }
 
        private void ObjectEquals()
        {
            foreach (var unit in _equalityUnits)
            {
                var unitValue = unit.Value;
                foreach (var value in unit.EqualValues)
                {
                    Assert.IsTrue(unitValue.Equals(value));
                    Assert.IsTrue(value.Equals(unitValue));
                }
            }
        }
 
        /// <summary>
        /// Comparison with Null should be false for reference types
        /// </summary>
        private void ObjectEqualsCheckNull()
        {
            if (typeof(T).IsValueType)
            {
                return;
            }
 
            var allValues = _equalityUnits.SelectMany(x => x.AllValues);
            foreach (var value in allValues)
            {
                Assert.IsFalse(value.Equals(null));
            }
        }
 
        /// <summary>
        /// Passing a value of a different type should just return false
        /// </summary>
        private void ObjectEqualsDifferentType()
        {
            var allValues = _equalityUnits.SelectMany(x => x.AllValues);
            foreach (var value in allValues)
            {
                Assert.IsFalse(value.Equals(42));
            }
        }
 
        private void GetHashCodeSemantics()
        {
            foreach (var unit in _equalityUnits)
            {
                foreach (var value in unit.EqualValues)
                {
                    Assert.AreEqual(value.GetHashCode(), unit.Value.GetHashCode());
                }
            }
        }
 
        private void EquatableEquals()
        {
            foreach (var unit in _equalityUnits)
            {
                var equatableUnit = (IEquatable<T>)unit.Value;
                foreach (var value in unit.EqualValues)
                {
                    Assert.IsTrue(equatableUnit.Equals(value));
                    var equatableValue = (IEquatable<T>)value;
                    Assert.IsTrue(equatableValue.Equals(unit.Value));
                }
 
                foreach (var value in unit.NotEqualValues)
                {
                    Assert.IsFalse(equatableUnit.Equals(value));
                    var equatableValue = (IEquatable<T>)value;
                    Assert.IsFalse(equatableValue.Equals(unit.Value));
                }
            }
        }
 
        /// <summary>
        /// If T is a reference type, null should return false in all cases
        /// </summary>
        private void EquatableEqualsCheckNull()
        {
            if (typeof(T).IsValueType)
            {
                return;
            }
 
            foreach (var cur in _equalityUnits.SelectMany(x => x.AllValues))
            {
                var value = (IEquatable<T>)cur;
                Assert.IsFalse(value.Equals(null));
            }
        }
    }
 
    public static class EqualityUtil
    {
        public static void RunAll<T>(
            Func<T, T, bool> compEqualsOperator,
            Func<T, T, bool> compNotEqualsOperator,
            bool skipOperators = false,
            bool skipEquatable = false,
            params EqualityUnit<T>[] values)
        {
            var util = new EqualityUtil<T>(values, compEqualsOperator, compNotEqualsOperator);
            util.RunAll(skipOperators:skipOperators, skipEquatable:skipEquatable);
        }
    }
}
 
 

