using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ThreadPool
{

    //Generic type
    public class Promise<T> : ThreadPool.NodeBase
    {
        internal AtomicBoolean Executing;
        internal AtomicBoolean Complete;
        public readonly object waitsync = new object();
        public T Result;

        private Func<T> Function;

        public override void Execute()
        {
            if (!Executing.FalseToTrue())
                return;

            Result = Function();

            Complete.FalseToTrue();
            Monitor.PulseAll(waitsync);
        }

        public Promise(Func<T> func)
        {
            Function = func;
            Executing = new AtomicBoolean(false);
            Complete = new AtomicBoolean(false);
        }

        public bool IsExecuting()
        {
            return Executing.Value;
        }
        public bool IsComplete()
        {
            return Complete.Value;
        }
    }
    //Specialization for void
    public class Promise : ThreadPool.NodeBase
    {
        internal AtomicBoolean Executing;
        internal AtomicBoolean Complete;
        public readonly object waitsync = new object();

        private Action Function;

        public override void Execute()
        {
            if (!Executing.FalseToTrue())
                return;

            Function();

            Complete.FalseToTrue();
            Monitor.PulseAll(waitsync);
        }

        public Promise(Action func)
        {
            Function = func;
            Executing = new AtomicBoolean(false);
            Complete = new AtomicBoolean(false);
        }

        public bool IsExecuting()
        {
            return Executing.Value;
        }
        public bool IsComplete()
        {
            return Complete.Value;
        }
    }
}
