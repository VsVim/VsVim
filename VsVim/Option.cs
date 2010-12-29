using System;

namespace VsVim
{
    public struct Option<T>
    {
        private readonly bool _isSome;
        private readonly T _value;

        public bool IsSome
        {
            get { return _isSome; }
        }

        public bool IsNone
        {
            get { return !_isSome; }
        }

        public T Value
        {
            get
            {
                if (!IsSome)
                {
                    throw new InvalidOperationException("No value present");
                }

                return _value;
            }
        }

        public Option(T value)
        {
            _value = value;
            _isSome = true;
        }

        public T GetValueOrDefault(T defaultValue = default(T))
        {
            return IsSome ? Value : defaultValue;
        }

        public static implicit operator Option<T>(Option option)
        {
            return new Option<T>();
        }

        public static implicit operator Option<T>(T value)
        {
            return new Option<T>(value);
        }
    }

    public class Option
    {
        public static Option None
        {
            get { return null; }
        }

        private Option()
        {

        }

        public static Option<T> CreateValue<T>(T value)
        {
            return new Option<T>(value);
        }
    }
}
