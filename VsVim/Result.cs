using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace VsVim
{
    public struct Result<T>
    {
        private readonly bool m_isError;
        private readonly T m_value;
        private readonly int m_hresult;

        public bool IsValue
        {
            get { return !m_isError; }
        }

        public bool IsError
        {
            get { return m_isError; }
        }

        public T Value
        {
            get
            {
                if (!IsValue)
                {
                    throw Marshal.GetExceptionForHR(m_hresult);
                }

                return m_value;
            }
        }

        public int HResult
        {
            get
            {
                if (IsValue)
                {
                    throw Marshal.GetExceptionForHR(m_hresult);
                }

                return m_hresult;
            }
        }

        public Result(T value)
        {
            m_value = value;
            m_isError = false;
            m_hresult = 0;
        }

        public Result(int hresult)
        {
            m_hresult = hresult;
            m_isError = true;
            m_value = default(T);
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
