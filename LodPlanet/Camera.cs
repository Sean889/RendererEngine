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

        Camera(Vector3d Pos, Quaterniond Rot, double NearZ = 1.0, double FarZ = 1000.0, double FovY = 60.0, double Aspect = 4.0 / 3.0)
        {
            this.Rotation = Rot;
            this.Position = Pos;
            this.NearZ = NearZ;
            this.FarZ = FarZ;
            this.FovY = FovY;
            this.Aspect = Aspect;
        }
    }
}
