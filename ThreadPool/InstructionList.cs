using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;

/*
	This is a async implementation that follows a hybrid
	async and deferred execution pattern. The function
	can be run asyncronously until the get method of the
	corresponding future is called. At that point the 
	function will be run on the querying thread unless
	the function is already being run by one of the task
	execution threads. This threadpool will run with 0
	worker threads, however all functions will be executed
	in a deferred manner.

	All functions are put through a lock free queue. New
	nodes are enqueued at the back of the queue so as the
	queue gets longer the time it takes to enqueue an item
	will grow. The queue also suffers a cache miss for each
	item traversed during the enqueue operation.

	Within the detail namespace the promise class is used
	to store the data for the function. If all future
	instances referring to the promise are destroyed then
	the promise will be deleted without the function being
	executed. future::release can be called to let the 
	promise execute on its own. The downside to this method
	is that, as the promise instance is managed by its 
	future instances, the owner will have to delete the
	promise on their own. To allow access to the promise,
	the method getPromise() has been provided. (Note: if
	there are other futures still holding references to 
	the promise they will still delete the promise if the
	reference counter in the promise goes down to 0. To
	prevent this the refs field is promise should be 
	incremented by one to prevent the reference counter
	from being decremented to 0.) To facilitate this all
	members of the promise class have been made public.
	Changing fields in the promise other than the refs
	field could cause other threads to deadlock permanently
	within the future.get() method, incorrect values to
	be returned or the method could potentially be
	executed multiple times, causing Undefined Behavior.
	You have been warned. However correct usage of these 
	values can allow the user to set the value returned
	independently of the function, if required.
*/
public class ThreadPool
{
    private static readonly object tasksync = new object();
    private static AtomicBoolean terminate;
    private static List<Thread> threads;
    private static InstructionList list;
    private static ConcurrentQueue<InstructionList.NodeBase> background_tasks;

    private class AtomicBoolean
    {
        private const int TRUE_VALUE = 1;
        private const int FALSE_VALUE = 0;
        private int zeroOrOne = FALSE_VALUE;

        public AtomicBoolean()
            : this(false)
        { }

        public AtomicBoolean(bool initialValue)
        {
            this.Value = initialValue;
        }

        /// <summary>
        /// Provides (non-thread-safe) access to the backing value
        /// </summary>
        public bool Value
        {
            get
            {
                return zeroOrOne == TRUE_VALUE;
            }
            set
            {
                zeroOrOne = (value ? TRUE_VALUE : FALSE_VALUE);
            }
        }

        /// <summary>
        /// Attempt changing the backing value from true to false.
        /// </summary>
        /// <returns>Whether the value was (atomically) changed from false to true.</returns>
        public bool FalseToTrue()
        {
            return SetWhen(true, false);
        }

        /// <summary>
        /// Attempt changing the backing value from false to true.
        /// </summary>
        /// <returns>Whether the value was (atomically) changed from true to false.</returns>
        public bool TrueToFalse()
        {
            return SetWhen(false, true);
        }

        /// <summary>
        /// Attempt changing from "whenValue" to "setToValue".
        /// Fails if this.Value is not "whenValue".
        /// </summary>
        /// <param name="setToValue"></param>
        /// <param name="whenValue"></param>
        /// <returns></returns>
        public bool SetWhen(bool setToValue, bool whenValue)
        {
            int comparand = whenValue ? TRUE_VALUE : FALSE_VALUE;
            int result = Interlocked.CompareExchange(ref zeroOrOne, (setToValue ? TRUE_VALUE : FALSE_VALUE), comparand);
            bool originalValue = result == TRUE_VALUE;
            return originalValue == whenValue;
        }

    }

    //<summary>This is a lock-free queue supporting concurrent enqueuing and dequeuing
    //as well as concurrent random removal from the queue.
    //
    // This queue is based on a linked list. This causes pushes to take the
    // longest amount of time as they have to iterate through the entire list
    // (potentially multiple times) to enqueue an item.</summary>
    private class InstructionList
    {
        int ListCounter = 0;
        volatile NodeBase Head = null;

        internal class NodeBase
        {
            public volatile NodeBase next;
            public UInt32 id;
            public AtomicBoolean available;

            public abstract void Execute();

            public NodeBase()
            {
                next = null;
                id = (uint)Interlocked.Increment(ref list.ListCounter);
            }
        }

        public void Push(NodeBase node)
        {
            NodeBase current;
            node.id = (uint)Interlocked.Increment(ref ListCounter);
            node.available.Value = true;

            for(;;)
            {
                current = Head;

                while(current != null && current.next != null)
                {
                    current = current.next;
                }

                if(current != null)
                {
                    NodeBase none = null;
                    if(Interlocked.CompareExchange(ref current.next, node, none) == none)
                    {
                        break;
                    }
                }
                else
                {
                    NodeBase none = null;
                    if(Interlocked.CompareExchange(ref Head, node, none) == none)
                    {
                        break;
                    }
                }

                Monitor.Pulse(tasksync);
            }
        }
        public NodeBase Pop()
        {
            for(;;)
            {
                NodeBase current = Head;
                NodeBase next = current.next;

                if(current == null)
                    //The list is empty return null
                    return null;
                uint id = current.id;
                //Try and remove the first item from the list using CAS
                while(Interlocked.CompareExchange(ref Head, current, current.next) != current.next)
                {
                    //It didn't work, retry.
                    current = Head;
                    next = current.next;
		            //Check to make sure the queue is not empty
					if (current == null)
						//The queue emptied while we were waiting for it.
						return null;
                    //Set the ID for later
                    id = current.id;
                }
                return current;
            }
        }
        public void Remove(NodeBase node)
        {
            if (Head == null)
                //The list is empty, won't find anything here
                return;
            NodeBase prev, ptr;
            for(;;)
            {
                prev = Head;
                ptr = prev.next;
                while(!(ptr == node || ptr == null))
                {
                    //Not the one we want, contiue on
                    prev = prev.next;
                    ptr = prev.next;
                }

                //Good, we've found the one we want or reached the end of the list
                //Check to make sure the pointer isn't null, and if not, remove the node from the list using CAS
			    if(ptr != null && Interlocked.CompareExchange(ref prev.next, ptr.next, ptr) != ptr)
                {
                    //Someone else stole the entry out from under us.
                    //Hmm ok, what now?
                    //Retry from the start.
                    continue;
                }
                break;
            }
        }

        public bool Empty()
        {
            return Interlocked.Equals(Head, null);
        }
    }

    private static void BackgroundThreadExecutor()
    {
        do
        {
            lock(tasksync)
            {
                Monitor.Wait(tasksync);
            }

            InstructionList.NodeBase background_task, task = list.Pop();
            if (!background_tasks.TryDequeue(out background_task))
                background_task = null;

            while(task != null || background_task != null)
            {
                if(task != null)
                {
                    task.Execute();
                    task = list.Pop();
                }
                else if(background_task != null)
                {
                    background_task.Execute();
                    if (!background_tasks.TryDequeue(out background_task))
                        background_task = null;
                }
            }
        } while(!terminate.Value);
    }
    private static void ThreadExecutor()
    {
        do
        {
            lock(tasksync)
            {
                Monitor.Wait(tasksync);
            }

            InstructionList.NodeBase task = list.Pop();

            while(task != null)
            {
                task.Execute();
                task = list.Pop();
            }
        } while (!terminate.Value);
    }

    //Initialization and termination functions

    //Initializes the threadpool with the specified
	//number of threads. Before this function is 
	//called no tasks will be executed except when
	//future<_Ty>.get() is called.
    public static void Init(uint num_threads)
    {
        list = new InstructionList();
        terminate.Value = false;
        threads.Add(new Thread(BackgroundThreadExecutor));
        for(uint i = 1; i < num_threads; i++)
        {
            threads.Add(new Thread(ThreadExecutor));
        }
        Monitor.PulseAll(tasksync);
    }
    //Terminates the threadpool, all tasks remaining
	//in the task queue will be executed before this 
	//function is finished, any tasks enqueued after
	//this function will not be executed until init
	//is called again
    public static void Terminate()
    {
        terminate.Value = true;
        Monitor.PulseAll(tasksync);
        int size = threads.Count;
        for (int i = 0; i < size; i++)
        {
            threads[i].Join();
        }
        threads = null;
        list = null;
    }

    public static bool ExecuteSingleFunction()
    {
        InstructionList.NodeBase task = list.Pop();
        if(task != null)
        {
            task.Execute();
            return true;
        }
        return false;
    }

    //Generic type
    public class Promise<T> : InstructionList.NodeBase
    {
        public AtomicBoolean Executing;
        public AtomicBoolean Complete;
        public readonly object waitsync = new object();
        public volatile T Result;

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
            Executing.Value = false;
            Complete.Value = false;
        }
    }
    //Specialization for void
    public class VoidPromise : InstructionList.NodeBase
    {
        public AtomicBoolean Executing;
        public AtomicBoolean Complete;
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

        public VoidPromise(Action func)
        {
            Function = func;
            Executing.Value = false;
            Complete.Value = false;
        }
    }

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
                throw new NullReferenceException();

            if (!promise.Complete.Value)
            {
                if(!promise.Executing.Value)
                {
                    list.Remove(promise);
                    if(promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                        return promise.Result;
                    }
                }

                while(!promise.Complete.Value)
                {
                    ExecuteSingleFunction();
                }
            }

            return promise.Result;
        }
        //Forces the function to complete
        //before returning
        public void Complete()
        {
            if (promise == null)
                throw new NullReferenceException();

            if (!promise.Complete.Value)
            {
                if (!promise.Executing.Value)
                {
                    list.Remove(promise);
                    if (promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                    }
                }

                while (!promise.Complete.Value)
                {
                    ExecuteSingleFunction();
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

        //Default constructor
        //The future is invalid until assigned to.
        public Future()
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
    }
    //Specialization for void
    public struct VoidFuture
    {
        private VoidPromise promise;

        //Block until the promise is ready.
        //If the promise has not been run
        //asyncronously yet, run it now.
        //If the promise is being executed
        //busy wait until it is not.
        public void Get()
        {
            if (promise == null)
                throw new NullReferenceException();

            if (!promise.Complete.Value)
            {
                if (!promise.Executing.Value)
                {
                    list.Remove(promise);
                    if (promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                    }
                }

                while (!promise.Complete.Value)
                {
                    ExecuteSingleFunction();
                }
            }
        }
        //Forces the function to complete
        //before returning
        public void Complete()
        {
            if (promise == null)
                throw new NullReferenceException();

            if (!promise.Complete.Value)
            {
                if (!promise.Executing.Value)
                {
                    list.Remove(promise);
                    if (promise.Executing.FalseToTrue())
                    {
                        promise.Execute();
                        promise.Complete.FalseToTrue();
                    }
                }

                while (!promise.Complete.Value)
                {
                    ExecuteSingleFunction();
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

        //Default constructor
        //The future is invalid until assigned to.
        public VoidFuture()
        {
            promise = null;
        }

        //Construct future to refer to the specific promise.
        //The future is valid after this.
        public VoidFuture(VoidPromise pr)
        {
            promise = pr;
        }

        public static bool operator ==(VoidFuture rhs, VoidFuture lhs)
        {
            return rhs.promise == lhs.promise;
        }
        public static bool operator !=(VoidFuture rhs, VoidFuture lhs)
        {
            return rhs.promise != lhs.promise;
        }
        //Implicit validity check
        public static implicit operator bool(VoidFuture f)
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
        public VoidPromise GetPromise()
        {
            return promise;
        }
    }

    //Creates a task from the given function and enqueues it
    //to the task queue.
    public Future<T> QueueAsync<T>(Func<T> func)
    {
        Promise<T> pr = new Promise<T>(func);
        list.Push(pr);
        return new Future<T>(pr);
    }
    //Directly takes the given promise and enqueues it to the
    //task queue. This can be used to do a bulk enqueue 
    //operation if the promise->next field points to another
    //promise.
    public void QueueAsync<T>(Promise<T> promise)
    {
        list.Push(promise);
        Monitor.Pulse(tasksync);
    }

    //Overload for void returns
    public VoidFuture QueueAsync<T>(Action func)
    {
        VoidPromise pr = new VoidPromise(func);
        list.Push(pr);
        return new VoidFuture(pr);
    }

    //Directly takes the given promise and enqueues it to the
    //task queue. This can be used to do a bulk enqueue 
    //operation if the promise->next field points to another
    //promise. The task will run at a lower priority than
    //the other tasks.
    void QueueBackgroundAsync(Action func)
    {
        list.Push(new VoidPromise(func));
        Monitor.Pulse(tasksync);
    }

    //Creates a task that will execute automatically, the
    //task will delete itself when it is completed. Use like
    //QueueBackgroundAsync() but with normal priority.
    void QueueAsyncAuto(Action func)
    {
        list.Push(new VoidPromise(func));
        Monitor.Pulse(tasksync);
    }
}
