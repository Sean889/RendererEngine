using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CppThreadPool
{
    //Generic type
    public struct Future<T>
    {
        private Promise<T> promise;

        //Block until the promise is ready.
        //If the promise has not been run
        //asyncronously yet, run it now.
        //If the promise is being executed
        //busy wait until it is not.
        public T Get()
        {
            if (promise == null)
                throw new NullReferenceException("Get() called on incomplete future.");

            if (!promise.Complete.Value)
            {
                if (!promise.Executing.Value)
                {
                    ThreadPool.list.Remove(promise);
                    if (promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                        return promise.Result;
                    }
                }

                while (!promise.Complete.Value)
                {
                    ThreadPool.ExecuteSingleFunction();
                }
            }

            return promise.Result;
        }
        //Forces the function to complete
        //before returning
        public void Complete()
        {
            if (promise == null)
                throw new NullReferenceException("Complete() called on incomplete future.");

            if (!promise.Complete.Value)
            {
                if (!promise.Executing.Value)
                {
                    ThreadPool.list.Remove(promise);
                    if (promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                    }
                }

                while (!promise.Complete.Value)
                {
                    ThreadPool.ExecuteSingleFunction();
                }
            }
        }
        //Releases whatever part of the 
        //shared ownership of the promise
        //the future has. Destruction of
        //the promise is handled by the GC.
        public void Release()
        {
            promise = null;
        }

        //Construct future to refer to the specific promise.
        //The future is valid after this.
        public Future(Promise<T> pr)
        {
            promise = pr;
        }

        public static bool operator ==(Future<T> rhs, Future<T> lhs)
        {
            return rhs.promise == lhs.promise;
        }
        public static bool operator !=(Future<T> rhs, Future<T> lhs)
        {
            return rhs.promise != lhs.promise;
        }
        //Implicit validity check
        public static implicit operator bool(Future<T> f)
        {
            return f.promise != null;
        }
        //Explicit validity check
        public bool IsValid()
        {
            return this;
        }
        //Returns whether the function has been completed
        public bool IsComplete()
        {
            return promise.Complete.Value;
        }
        //Accessor
        public Promise<T> GetPromise()
        {
            return promise;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    //Specialization for void
    public struct Future
    {
        private Promise promise;

        //Block until the promise is ready.
        //If the promise has not been run
        //asyncronously yet, run it now.
        //If the promise is being executed
        //busy wait until it is not.
        public void Get()
        {
            if (promise == null)
                throw new NullReferenceException("Get() called on incomplete future.");

            if (!promise.Complete.Value)
            {
                if (!promise.Executing.Value)
                {
                    ThreadPool.list.Remove(promise);
                    if (promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                    }
                }

                while (!promise.Complete.Value)
                {
                    ThreadPool.ExecuteSingleFunction();
                }
            }
        }
        //Forces the function to complete
        //before returning
        public void Complete()
        {
            if (promise == null)
                throw new NullReferenceException("Complete() called on incomplete future.");

            if (!promise.Complete.Value)
            {
                if (!promise.Executing.Value)
                {
                    ThreadPool.list.Remove(promise);
                    if (promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                    }
                }

                while (!promise.Complete.Value)
                {
                    ThreadPool.ExecuteSingleFunction();
                }
            }
        }
        //Releases whatever part of the 
        //shared ownership of the promise
        //the future has. Destruction of
        //the promise is handled by the GC.
        public void Release()
        {
            promise = null;
        }

        //Construct future to refer to the specific promise.
        //The future is valid after this.
        public Future(Promise pr)
        {
            promise = pr;
        }

        public static bool operator ==(Future rhs, Future lhs)
        {
            return rhs.promise == lhs.promise;
        }
        public static bool operator !=(Future rhs, Future lhs)
        {
            return rhs.promise != lhs.promise;
        }
        //Implicit validity check
        public static implicit operator bool(Future f)
        {
            return f.promise != null;
        }
        //Explicit validity check
        public bool IsValid()
        {
            return this;
        }
        //Returns whether the function has been completed
        public bool IsComplete()
        {
            return promise.Complete.Value;
        }
        //Accessor
        public Promise GetPromise()
        {
            return promise;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
