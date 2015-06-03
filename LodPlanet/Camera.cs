using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace LodPlanet
{
    public class Camera
    {
        public double NearZ, FarZ;
        public double FovY;
        public double Aspect;

        public Vector3d Position;
        public Quaterniond Rotation;
    }
}
