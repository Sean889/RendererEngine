using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace LodPlanet
{
    /*
    	Quadrant info

		 _______________
		|nwc	|		|nec
		|	0	|	1	|
		|_______|_______|
		|		|		|
		|	2	|	3	|
		|_______|_______|
		swc				 sec
	*/

    public class Patch
    {
        //Const values, not for use outside of the assembly
        //Use the accessors if not inside the dll
        //Use these if inside the dll
        internal static const uint I_SIDE_LEN = 64u;
        internal static const uint I_NUM_INDICES = (I_SIDE_LEN - 1u) * (I_SIDE_LEN - 1u) * 6u + 24u * (I_SIDE_LEN - 1u);
        internal static const uint I_NUM_VERTICES = I_SIDE_LEN * I_SIDE_LEN + I_SIDE_LEN * 4u;
        internal static const uint I_SKIRT_DEPTH = 5000u;

        //Accessors to const values
        public static uint SIDE_LEN
        {
            get
            {
                return I_SIDE_LEN;
            }
        }
        public static uint NUM_INDICES
        {
            get
            {
                return I_NUM_INDICES;
            }
        }
        public static uint NUM_VERTICES
        {
            get
            {
                return I_NUM_VERTICES;
            }
        }
        public static uint SKIRT_DEPTH
        {
            get
            {
                return I_SKIRT_DEPTH;
            }
        }

        public static readonly uint[] INDICES = GetIndices();

        public static uint[] GetIndices()
        {
           uint[] indices = new uint[NUM_INDICES];

			uint idx = 0;

			for (uint y = 0; y < (SIDE_LEN - 1); y++)
			{
				for (uint x = 0; x < (SIDE_LEN - 1); x++)
				{
					//First triangle
					indices[idx++] = y * SIDE_LEN + x;
					indices[idx++] = y * SIDE_LEN + x + 1;
					indices[idx++] = (y + 1) * SIDE_LEN + x;

					//Second triangle
					indices[idx++] = y * SIDE_LEN + x + 1;
					indices[idx++] = (y + 1) * SIDE_LEN + x + 1;
					indices[idx++] = (y + 1) * SIDE_LEN + x;
				}
			}

			//Generate indices for skirt

			for (uint i = 0; i < SIDE_LEN - 1; i++)
			{
				//Top side
				indices[idx++] = SIDE_LEN * SIDE_LEN + i;
				indices[idx++] = SIDE_LEN * SIDE_LEN + i + 1;
				indices[idx++] = i;

				indices[idx++] = SIDE_LEN * SIDE_LEN + i + 1;
				indices[idx++] = i + 1;
				indices[idx++] = i;

				//Right side
				indices[idx++] = SIDE_LEN * (i + 1) - 1;
				indices[idx++] = SIDE_LEN * SIDE_LEN + SIDE_LEN + i;
				indices[idx++] = SIDE_LEN * (i + 2) - 1;

				indices[idx++] = SIDE_LEN * SIDE_LEN + SIDE_LEN + i;
				indices[idx++] = SIDE_LEN * SIDE_LEN + SIDE_LEN + i + 1;
				indices[idx++] = SIDE_LEN * (i + 2) - 1;

				//Bottom side
				indices[idx++] = (SIDE_LEN - 1) * SIDE_LEN + i;
				indices[idx++] = (SIDE_LEN - 1) * SIDE_LEN + i + 1;
				indices[idx++] = SIDE_LEN * (SIDE_LEN + 2) + i;

				indices[idx++] = (SIDE_LEN - 1) * SIDE_LEN + i + 1;
				indices[idx++] = SIDE_LEN * (SIDE_LEN + 2) + i + 1;
				indices[idx++] = SIDE_LEN * (SIDE_LEN + 2) + i;

				//Left side
				indices[idx++] = SIDE_LEN * (SIDE_LEN + 3) + i;
				indices[idx++] = SIDE_LEN * i;
				indices[idx++] = SIDE_LEN * (SIDE_LEN + 3) + i + 1;

				indices[idx++] = SIDE_LEN * i;
				indices[idx++] = SIDE_LEN * (i + 1);
				indices[idx++] = SIDE_LEN * (SIDE_LEN + 3) + i + 1;
			}
			return indices;
        }

        public Patch parent = null;

        public Patch nw = null; //Quadrant 0
        public Patch ne = null; //Quadrant 1
        public Patch sw = null; //Quadrant 2
        public Patch se = null; //Quadrant 3

        //Corners of the patch on the base cube
        public Vector3d nwc, nec, swc, sec;

        public Vector3d pos;               //Position on the sphere

        public uint level;              //Level within the quadtree
        public double side_len;         //Side length of the patch
        public double planet_radius;    //Radius of the planet to wich this patch belongs

        public float[,] mesh_data = null;

        private PlanetRenderer executor;

        bool IsSubdivided()
        {
            return nw != null;
        }

        Patch(Vector3d nwc, Vector3d nec, Vector3d swc, Vector3d sec, double planet_radius, double side_len, PlanetRenderer manager, Patch parent = null, uint level = 0)
        {
            this.nwc = nwc;
            this.nec = nec;
            this.swc = swc;
            this.sec = sec;
            this.planet_radius = planet_radius;
            this.side_len = side_len;
            this.executor = manager;
            this.parent = parent;
            this.level = level;
        }
    }
}
