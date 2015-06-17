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
namespace CppThreadPool
{
    public class ThreadPool
    {
        //internal static readonly object tasksync = new object();
        internal static AtomicBoolean terminate;
        internal static List<Thread> threads;
        internal static InstructionList list;
        internal static ConcurrentQueue<NodeBase> background_tasks;
        internal static AutoResetEvent Event = new AutoResetEvent(false);

#pragma warning disable 420

        public class NodeBase
        {
            public volatile NodeBase next;
            public UInt32 id;

            //Called to execute the task in the Instruction List
            public virtual void Execute()
            {
                //Empty function
            }

            public NodeBase()
            {
                next = null;
                id = (uint)Interlocked.Increment(ref list.ListCounter);
            }
        }

        //<summary>This is a lock-free queue supporting concurrent enqueuing and dequeuing
        //as well as concurrent random removal from the queue.
        //
        // This queue is based on a linked list. This causes pushes to take the
        // longest amount of time as they have to iterate through the entire list
        // (potentially multiple times) to enqueue an item.</summary>
        internal class InstructionList
        {
            internal int ListCounter = 0;
            private volatile NodeBase Head = null;

            public void Push(NodeBase node)
            {
                NodeBase current;
                node.id = (uint)Interlocked.Increment(ref ListCounter);

                for (; ; )
                {
                    current = Head;

                    while (current != null && current.next != null)
                    {
                        current = current.next;
                    }

                    if (current != null)
                    {
                        NodeBase none = null;
                        if (Interlocked.CompareExchange(ref current.next, node, none) == none)
                        {
                            break;
                        }
                    }
                    else
                    {
                        NodeBase none = null;
                        if (Interlocked.CompareExchange(ref Head, node, none) == none)
                        {
                            break;
                        }
                    }

                    Event.Set();
                }
            }
            public NodeBase Pop()
            {
                for (; ; )
                {
                    NodeBase current = Head;

                    if (current == null)
                        //The list is empty return null
                        return null;

                    NodeBase next = current.next;

                    uint id = current.id;
                    //Try and remove the first item from the list using CAS
                    while (Interlocked.CompareExchange(ref Head, current, current.next) != current.next)
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
                for (; ; )
                {
                    prev = Head;
                    ptr = prev.next;
                    while (!(ptr == node || ptr == null))
                    {
                        //Not the one we want, contiue on
                        prev = prev.next;
                        ptr = prev.next;
                    }

                    //Good, we've found the one we want or reached the end of the list
                    //Check to make sure the pointer isn't null, and if not, remove the node from the list using CAS
                    if (ptr != null && Interlocked.CompareExchange(ref prev.next, ptr.next, ptr) != ptr)
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
                Event.WaitOne(new TimeSpan(10));

                NodeBase background_task, task = list.Pop();
                if (!background_tasks.TryDequeue(out background_task))
                    background_task = null;

                while (task != null || background_task != null)
                {
                    if (task != null)
                    {
                        task.Execute();
                        task = list.Pop();
                    }
                    else if (background_task != null)
                    {
                        background_task.Execute();
                        if (!background_tasks.TryDequeue(out background_task))
                            background_task = null;
                    }
                }
            } while (!terminate.Value);
        }
        private static void ThreadExecutor()
        {
            do
            {
                Event.WaitOne(new TimeSpan(10));

                NodeBase task = list.Pop();

                while (task != null)
                {
                    task.Execute();
                    task = list.Pop();
                }
            } while (!terminate.Value);
        }

        //Initialization and termination functions

        /// <summary>
        /// Initializes the threadpool with the specified
        /// number of threads. Before this function is 
        /// called no tasks will be executed except when
        /// future<_Ty>.get() is called.
        /// </summary>
        /// <param name="num_threads"> The number of threads to initialize the thread pool with. </param>
        public static void Init(uint num_threads)
        {
            list = new InstructionList();
            background_tasks = new ConcurrentQueue<NodeBase>();
            terminate = new AtomicBoolean(false);
            threads = new List<Thread>();
            terminate.TrueToFalse();
            Thread thr = new Thread(BackgroundThreadExecutor);
            threads.Add(thr);
            thr.Start();
            for (uint i = 1; i < num_threads; i++)
            {
                thr = new Thread(ThreadExecutor);
                threads.Add(thr);
                thr.Start();
            }
        }
        //Terminates the threadpool, all tasks remaining
        //in the task queue will be executed before this 
        //function is finished, any tasks enqueued after
        //this function will not be executed until init
        //is called again
        public static void Terminate()
        {
            terminate.FalseToTrue();
            
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
            NodeBase task = list.Pop();
            if (task != null)
            {
                task.Execute();
                return true;
            }
            return false;
        }

        //Creates a task from the given function and enqueues it
        //to the task queue.
        public static Future<T> QueueAsync<T>(Func<T> func)
        {
            Promise<T> pr = new Promise<T>(func);
            list.Push(pr);
            return new Future<T>(pr);
        }
        //Directly takes the given promise and enqueues it to the
        //task queue. This can be used to do a bulk enqueue 
        //operation if the promise->next field points to another
        //promise.
        public static void QueueAsync<T>(Promise<T> promise)
        {
            list.Push(promise);
            Event.Set();
        }

        //Overload for void returns
        public static Future QueueAsync<T>(Action func)
        {
            Promise pr = new Promise(func);
            list.Push(pr);
            return new Future(pr);
        }

        //Directly takes the given promise and enqueues it to the
        //task queue. This can be used to do a bulk enqueue 
        //operation if the promise->next field points to another
        //promise. The task will run at a lower priority than
        //the other tasks.
        public static void QueueBackgroundAsync(Action func)
        {
            list.Push(new Promise(func));
            Event.Set();
        }

        //Creates a task that will execute automatically, the
        //task will delete itself when it is completed. Use like
        //QueueBackgroundAsync() but with normal priority.
        public static void QueueAsyncAuto(Action func)
        {
            list.Push(new Promise(func));
            Event.Set();
        }
    }
}