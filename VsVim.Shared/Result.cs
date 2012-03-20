using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace VsVim
{
    public struct Result<T>
    {
        private readonly bool _isSuccess;
        private readonly T _value;
        private readonly int _hresult;

        public bool IsSuccess
        {
            get { return _isSuccess; }
        }

        public bool IsError
        {
            get { return !_isSuccess; }
        }

        // TOOD: Get rid of this.  Make it a method that says throws
        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException();
                }

                return _value;
            }
        }

        public int HResult
        {
            get
            {
                if (IsSuccess)
                {
                    throw new InvalidOperationException();
                }

                return _hresult;
            }
        }

        public Result(T value)
        {
            _value = value;
            _isSuccess = true;
            _hresult = 0;
        }

        public Result(int hresult)
        {
            _hresult = hresult;
            _isSuccess = false;
            _value = default(T);
        }

        public T GetValueOrDefault(T defaultValue = default(T))
        {
            return IsSuccess ? Value : defaultValue;
        }

        public bool TryGetValue(out T value)
        {
            if (IsSuccess)
            {
                value = Value;
                return true;
            }

            value = default(T);
            return false;
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
        private readonly bool _isSuccess;
        private readonly int _hresult;

        public bool IsSuccess
        {
            get { return _isSuccess;  }
        }

        public bool IsError
        {
            get { return !_isSuccess; }
        }

        public int HResult
        {
            get
            {
                if (!IsError)
                {
                    throw new InvalidOperationException();
                }
                return _hresult;
            }
        }

        private Result(int hresult)
        {
            _hresult = hresult;
            _isSuccess = ErrorHandler.Succeeded(hresult);
        }

        public static Result Error
        {
            get { return new Result(VSConstants.E_FAIL); }
        }

        public static Result Success
        {
            get { return new Result(VSConstants.S_OK); }
        }

        public static Result<T> CreateSuccess<T>(T value)
        {
            return new Result<T>(value);
        }

        public static Result<T> CreateSuccessNonNull<T>(T value)
            where T : class
        {
            if (value == null)
            {
                return Result.Error;
            }

            return new Result<T>(value);
        }

        public static Result CreateError(int value)
        {
            return new Result(value);
        }

        public static Result CreateError(Exception ex)
        {
            return CreateError(Marshal.GetHRForException(ex));
        }

        public static Result<T> CreateSuccessOrError<T>(T potentialValue, int hresult)
        {
            return ErrorHandler.Succeeded(hresult)
                ? CreateSuccess(potentialValue)
                : new Result<T>(hresult: hresult);
        }
    }
}
