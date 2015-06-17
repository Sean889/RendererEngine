using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LodPlanet
{
    public interface ICommandQueue
    {
        void EnqueueCommand(Action Command);
    }
}
