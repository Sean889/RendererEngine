using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace LodPlanet
{
    namespace Internal
    {
        public class PlanetImpl
        {
            public Patch Top, Bottom, Right, Left, Front, Back;

            public Vector3d Position;
            public Quaterniond Rotation;

            public double Radius;

            public IEnumerable<Patch> GetSides()
            {
                yield return Top;
                yield return Bottom;
                yield return Right;
                yield return Left;
                yield return Front;
                yield return Back;
            }

            public PlanetImpl(Vector3d pos, Quaterniond rot, double radius, PlanetRenderer renderer)
            {
                Position = pos;
                Rotation = rot;
                Radius = radius;

                Top = new Patch(new Vector3d(radius, radius, -radius), new Vector3d(-radius, radius, -radius), new Vector3d(radius, radius, radius), new Vector3d(-radius, radius, radius), radius, radius * 2, renderer);
                Bottom = new Patch(new Vector3d(-radius, -radius, radius), new Vector3d(-radius, -radius, -radius), new Vector3d(radius, -radius, radius), new Vector3d(radius, -radius, -radius), radius, radius * 2, renderer);
                Left = new Patch(new Vector3d(-radius, -radius, -radius), new Vector3d(-radius, radius, -radius), new Vector3d(radius, -radius, -radius), new Vector3d(radius, radius, -radius), radius, radius * 2, renderer);
                Right = new Patch(new Vector3d(radius, radius, radius), new Vector3d(-radius, radius, radius), new Vector3d(radius, -radius, radius), new Vector3d(-radius, -radius, radius), radius, radius * 2, renderer);
                Front = new Patch(new Vector3d(-radius, radius, radius), new Vector3d(-radius, radius, -radius), new Vector3d(-radius, -radius, radius), new Vector3d(-radius, -radius, -radius), radius, radius * 2, renderer);
                Back = new Patch(new Vector3d(radius, -radius, -radius), new Vector3d(radius, radius, -radius), new Vector3d(radius, -radius, radius), new Vector3d(radius, radius, radius), radius, radius * 2, renderer);

                foreach (Patch p in GetSides())
                {
                    p.GenData();
                    p.GenRenderData();
                }
            }

            public bool CheckAndSubdivide(Vector3d CamPos)
            {
                bool outval = false;
                Matrix4d mat = Matrix4d.CreateFromQuaternion(Rotation);
                Vector3d ObjectCamPos = Vector3d.TransformVector(CamPos - Position, mat);
                foreach (Patch p in GetSides())
                {
                    outval =  p.CheckAndSubdivide(ObjectCamPos) || outval;
                }
                return outval;
            }
            public void SubdivideComplete(Vector3d CamPos)
            {
                Vector3d ObjectCamPos = Vector3d.TransformVector(CamPos - Position, Matrix4d.CreateFromQuaternion(Rotation));
                foreach (Patch p in GetSides())
                {
                    p.ForceSubdivide(ObjectCamPos);
                }
            }
        }
    }
}
