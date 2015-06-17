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

        public Camera(Vector3d Pos, Quaterniond Rot) :
            this(Pos, Rot, 1.0, 1000.0, 60.0, 4.0 / 3.0)
        {

        }
        public Camera(Vector3d Pos, Quaterniond Rot, double NearZ, double FarZ, double FovY, double Aspect)
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
