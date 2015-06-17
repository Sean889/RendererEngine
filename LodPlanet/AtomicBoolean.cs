using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace LodPlanet
{
#pragma warning disable 420
    public struct AtomicBoolean
    {
        private volatile int value;

        private const int TRUE = 1;
        private const int FALSE = 0;

        public bool Value
        {
            get
            {
                return value != FALSE;
            }
            set
            {
                this.value = value ? TRUE : FALSE;
            }
        }

        /// <summary>
        /// Attempt changing the value from CompareAnd to Target.
        /// </summary>
        /// <param name="Target"> The target value. </param>
        /// <param name="CompareAnd"> The value that is being compared to. </param>
        /// <returns> Whether the change succeded. </returns>
        public bool SetWhen(bool Target, bool CompareAnd)
        {
            int comparand = CompareAnd ? TRUE : FALSE;
            int result = Interlocked.CompareExchange(ref value, Target ? TRUE : FALSE, comparand);
            return (result == TRUE) == CompareAnd;
        }
        /// <summary>
        /// Attempt changing the value from False to True.
        /// </summary>
        /// <returns> Whether the change occured. </returns>
        public bool FalseToTrue()
        {
            return SetWhen(false, true);
        }
        /// <summary>
        /// Attempt changing the value from True to False.
        /// </summary>
        /// <returns> Whether the change occured. </returns>
        public bool TrueToFalse()
        {
            return SetWhen(true, false);
        }
        /// <summary>
        /// Attempts to switch the value.
        /// </summary>
        /// <returns> Whether the change occured. </returns>
        public bool Switch()
        {
            bool val = value != FALSE;
            return SetWhen(!val, val);
        }

        /// <summary>
        /// Initalizes with the given value.
        /// </summary>
        /// <param name="Value"></param>
        public AtomicBoolean(bool Value = false)
        {
            value = Value ? 1 : 0;
        }
    }
}
