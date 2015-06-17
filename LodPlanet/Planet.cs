using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LodPlanet.Internal;
using OpenTK;

namespace LodPlanet
{
    public class Planet
    {
        private PlanetImpl PlanetInst;
        private PlanetRenderer RendererInst;

        /// <summary>
        /// Actual planet object.
        /// </summary>
        public PlanetImpl GetPlanet()
        {
            return PlanetInst;
        }

        /// <summary>
        /// Renderer object.
        /// </summary>
        public PlanetRenderer GetRenderer()
        {
            return RendererInst;
        }

        /// <summary>
        /// Planet position.
        /// </summary>
        public Vector3d Position
        {
            get
            {
                return PlanetInst.Position;
            }
            set
            {
                PlanetInst.Position = value;
            }
        }
        /// <summary>
        /// Planet rotation.
        /// </summary>
        public Quaterniond Rotation
        {
            get
            {
                return PlanetInst.Rotation;
            }
            set
            {
                PlanetInst.Rotation = value;
            }
        }
        /// <summary>
        /// Camera used for rendering.
        /// </summary>
        public Camera Camera
        {
            get
            {
                return RendererInst.CameraInst;
            }
            set
            {
                RendererInst.CameraInst = value;
            }
        }
        /// <summary>
        /// Planet radius. This is a read only field.
        /// </summary>
        public double Radius
        {
            get
            {
                return PlanetInst.Radius;
            }
            //A set value would be utterly pointless
        }

        /// <summary>
        /// Constructs a PlanetInst and a PlanetRenderer using the given input.
        /// </summary>
        /// <param name="Position"> The position of the planet. </param>
        /// <param name="Rotation"> The rotation of the planet. </param>
        /// <param name="Queue"> A command queue that OpenGL commands will go to. This can be shared between planets. </param>
        /// <param name="Cam"> The camera that will be used for rendering the planet. </param>
        /// <param name="radius"> The radius of the planet. </param>
        /// <param name="ColourMap"> A colour texture for the planet. </param>
        /// <param name="HeightMap"> A height map used to deform the planet. </param>
        /// <param name="NormalMap"> A normal map used for lighting calcualtion. </param>
        public Planet(Vector3d Position, Quaterniond Rotation, ICommandQueue Queue, Camera Cam, double radius, int ColourMap, int HeightMap, int NormalMap)
        {
            RendererInst = new PlanetRenderer(Queue, Cam, null, ColourMap, HeightMap, NormalMap);
            PlanetInst = new PlanetImpl(Position, Rotation, radius, RendererInst);
            RendererInst.PlanetInst = PlanetInst;
        }

        /// <summary>
        /// Runs a check and subdivide operation using the current camera position.
        /// Can only increase the quadtree depth by one level.
        /// </summary>
        /// <returns> A boolean representing whether a node was created or not. </returns>
        public bool CheckAndSubdivide()
        {
            return PlanetInst.CheckAndSubdivide(Camera.Position);
        }
        /// <summary>
        /// Forcefully subdivides the planet's quadtree down to the appropriate level.
        /// </summary>
        public void SubdivideComplete()
        {
            PlanetInst.SubdivideComplete(Camera.Position);
            RendererInst.Update();
        }

        public void Draw()
        {
            RendererInst.Draw();
        }


    }
}
