using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LodPlanet
{
    public interface GLCommandManager
    {
        void ExecuteCommand(Action GLCommand);
    }
}
