using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace LodPlanet
{
    public class Planet
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

        Planet(Vector3d pos, Quaterniond rot, double radius, PlanetRenderer renderer)
        {
            Position = pos;
            Rotation = rot;
            Radius = radius;

            Top     = new Patch(new Vector3d(radius, radius, -radius), new Vector3d(-radius, radius, -radius), new Vector3d(radius, radius, radius), new Vector3d(-radius, radius, radius),		radius, radius * 2, renderer);
			Bottom  = new Patch(new Vector3d(-radius, -radius, radius), new Vector3d(-radius, -radius, -radius), new Vector3d(radius, -radius, radius), new Vector3d(radius, -radius, -radius),	radius, radius * 2, renderer);
			Left    = new Patch(new Vector3d(-radius, -radius, -radius), new Vector3d(-radius, radius, -radius), new Vector3d(radius, -radius, -radius), new Vector3d(radius, radius, -radius),	radius, radius * 2, renderer);
			Right   = new Patch(new Vector3d(radius, radius, radius), new Vector3d(-radius, radius, radius), new Vector3d(radius, -radius, radius), new Vector3d(-radius, -radius, radius),		radius, radius * 2, renderer);
			Front   = new Patch(new Vector3d(-radius, radius, radius), new Vector3d(-radius, radius, -radius), new Vector3d(-radius, -radius, radius), new Vector3d(-radius, -radius, -radius),    radius, radius * 2, renderer);
			Back    = new Patch(new Vector3d(radius, -radius, -radius), new Vector3d(radius, radius, -radius), new Vector3d(radius, -radius, radius), new Vector3d(radius, radius, radius),		radius, radius * 2, renderer);
        
            foreach(Patch p in GetSides())
            {
                p.GenData();
                p.GenRenderData();
            }
        }

        void CheckAndSubdivide(Vector3d CamPos)
        {
            Vector3d ObjectCamPos = Vector3d.TransformVector(CamPos - Position, Matrix4d.CreateFromQuaternion(Rotation));
            foreach(Patch p in GetSides())
            {
                p.CheckAndSubdivide(ObjectCamPos);
            }
        }
        void SubdivideComplete(Vector3d CamPos)
        {
            Vector3d ObjectCamPos = Vector3d.TransformVector(CamPos - Position, Matrix4d.CreateFromQuaternion(Rotation));
            foreach(Patch p in GetSides())
            {
                p.ForceSubdivide(ObjectCamPos);
            }
        }
    }
}
