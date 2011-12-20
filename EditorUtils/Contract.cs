using System;
using System.Diagnostics;

namespace EditorUtils
{
    internal static class Contract
    {
        [Serializable]
        internal sealed class ContractException : Exception
        {
            internal ContractException() { }
            internal ContractException(string message) : base(message) { }
            internal ContractException(string message, Exception inner) : base(message, inner) { }
            internal ContractException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context)
                : base(info, context) { }
        }

        internal static void Fail()
        {
            Requires(false);
        }

        internal static void Requires(bool condition)
        {
            if (!condition)
            {
                throw new ContractException();
            }
        }

        [Conditional("DEBUG")]
        internal static void Assert(bool condition)
        {
            Requires(condition);
        }
    }
}
