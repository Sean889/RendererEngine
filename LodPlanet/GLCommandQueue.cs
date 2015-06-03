using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LodPlanet
{
    public class GLCommandQueue
    {
        private ConcurrentQueue<Action> CommandQueue = new ConcurrentQueue<Action>();

        public void EnqueueCommand(Action Command)
        {
            CommandQueue.Enqueue(Command);
        }

        public void ExecuteCommands()
        {
            Action Command;
            while(CommandQueue.TryDequeue(out Command))
            {
                Command();
            }
        }
    }
}
