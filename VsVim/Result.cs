using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace VsVim
{
    public struct Result<T>
    {
        private readonly bool _isError;
        private readonly T _value;
        private readonly int _hresult;

        public bool IsValue
        {
            get { return !_isError; }
        }

        public bool IsError
        {
            get { return _isError; }
        }

        public T Value
        {
            get
            {
                if (!IsValue)
                {
                    throw Marshal.GetExceptionForHR(_hresult);
                }

                return _value;
            }
        }

        public int HResult
        {
            get
            {
                if (IsValue)
                {
                    throw Marshal.GetExceptionForHR(_hresult);
                }

                return _hresult;
            }
        }

        public Result(T value)
        {
            _value = value;
            _isError = false;
            _hresult = 0;
        }

        public Result(int hresult)
        {
            _hresult = hresult;
            _isError = true;
            _value = default(T);
        }

        public T GetValueOrDefault(T defaultValue = default(T))
        {
            return IsValue ? Value : defaultValue;
        }

        public static implicit operator Result<T>(Result result)
        {
            return new Result<T>(hresult: result.HResult);
        }

        public static implicit operator Result<T>(T value)
        {
            return new Result<T>(value);
        }
    }

    public struct Result
    {
        private readonly int m_hresult;

        private Result(int hresult)
        {
            m_hresult = hresult;
        }

        public int HResult
        {
            get { return m_hresult; }
        }

        public static Result<T> CreateValue<T>(T value)
        {
            return new Result<T>(value);
        }

        public static Result CreateError()
        {
            return new Result(VSConstants.E_FAIL);
        }

        public static Result CreateError(int value)
        {
            return new Result(value);
        }

        public static Result CreateError(Exception ex)
        {
            return CreateError(Marshal.GetHRForException(ex));
        }

        public static Result<T> CreateValueOrError<T>(T potentialValue, int hresult)
        {
            return ErrorHandler.Succeeded(hresult)
                ? CreateValue(potentialValue)
                : new Result<T>(hresult: hresult);
        }
    }
}
