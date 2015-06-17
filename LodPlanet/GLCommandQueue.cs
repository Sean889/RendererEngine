using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LodPlanet
{
    public class GLCommandQueue : ICommandQueue
    {
        private ConcurrentQueue<Action> CommandQueue1 = new ConcurrentQueue<Action>();
        private ConcurrentQueue<Action> CommandQueue2 = new ConcurrentQueue<Action>();

        private AtomicBoolean Identifier;

        /// <summary>
        /// Adds a command to be executed on the rendering thread.
        /// </summary>
        /// <param name="Command"> The command to be executed. </param>
        public void EnqueueCommand(Action Command)
        {
            if(Identifier.Value)
            {
                CommandQueue1.Enqueue(Command);
            }
            else
            {
                CommandQueue2.Enqueue(Command);
            }
        }

        /// <summary>
        /// Executes all the commands that were enqueued during the last frame.
        /// </summary>
        public void ExecuteOldFrame()
        {
            Action Command;
            ConcurrentQueue<Action> CommandQueue = Identifier.Value ? CommandQueue1 : CommandQueue2;

            while(CommandQueue.TryDequeue(out Command))
            {
                Command();
            }
        }
        /// <summary>
        /// Changes which queue commands are enqueued in.
        /// </summary>
        public void SwitchFrames()
        {
            Identifier.Switch();
        }

        public GLCommandQueue()
        {

        }
    }
}
