using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using CppThreadPool;

namespace LodPlanet
{
    /*
    	Quadrant info

		 _______________
		|nwc    |       |nec
		|   0   |   1   |
		|_______|_______|
		|       |       |
		|   2   |   3   |
		|_______|_______|
		swc              sec
	*/

    public class Patch
    {
        //Const values, not for use outside of the assembly
        //Use the accessors if not inside the dll
        internal const uint I_SIDE_LEN = 64u;
        internal const uint I_NUM_INDICES = (I_SIDE_LEN - 1u) * (I_SIDE_LEN - 1u) * 6u + 24u * (I_SIDE_LEN - 1u);
        internal const uint I_NUM_VERTICES = I_SIDE_LEN * I_SIDE_LEN + I_SIDE_LEN * 4u;
        internal const uint I_SKIRT_DEPTH = 5000u;

        private static double Distance(Vector3d lhs, Vector3d rhs)
        {
            return (lhs - rhs).Length;
        }

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

        //Prefer getting the value of INDICES instead of using this
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

        public volatile Patch nw = null; //Quadrant 0
        public volatile Patch ne = null; //Quadrant 1
        public volatile Patch sw = null; //Quadrant 2
        public volatile Patch se = null; //Quadrant 3

        //Corners of the patch on the base cube
        public Vector3d nwc, nec, swc, sec;

        public Vector3d pos;       //Position on the sphere

        public uint level;              //Level within the quadtree
        public double side_len;         //Side length of the patch
        public double planet_radius;    //Radius of the planet to wich this patch belongs

        public Vector3d Position
        {
            get
            {
                return pos;
            }
            set
            {
                pos = value;
            }
        }
        public double SideLength
        {
            get
            {
                return side_len;
            }
            set
            {
                side_len = value;
            }
        }
        public double PlanetRadius
        {
            get
            {
                return planet_radius;
            }
            set
            {
                planet_radius = value;
            }
        }

        public volatile float[,] mesh_data = null;

        private PlanetRenderer executor;

        private DrawData RenderData = null;

        public bool IsSubdivided
        {
            get
            {
                return nw != null;
            }
        }

        public Patch(Vector3d nwc, Vector3d nec, Vector3d swc, Vector3d sec, double planet_radius, double side_len, PlanetRenderer manager, Patch parent = null, uint level = 0)
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

        public void MergeChildren()
        {
            nw = null;
            ne = null;
            sw = null;
            se = null;
        }

        /// <summary>
        /// Subdivides the patch and generates mesh data for the new children
        /// </summary>
        public void Subdivide()
        {
            Vector3d centre = (nwc + nec + swc + sec) * 0.25;

            nw = new Patch(nwc, (nwc + nec) * 0.5, (nwc + swc) * 0.5, centre, planet_radius, side_len * 0.5, executor, this, level + 1);
            ne = new Patch((nwc + nec) * 0.5, nec, centre, (nec + sec) * 0.5, planet_radius, side_len * 0.5, executor, this, level + 1);
			sw = new Patch((nwc + swc) * 0.5, centre, swc, (swc + sec) * 0.5, planet_radius, side_len * 0.5, executor, this, level + 1);
			se = new Patch(centre, (nec + sec) * 0.5, (swc + sec) * 0.5, sec, planet_radius, side_len * 0.5, executor, this, level + 1);

            nw.GenData();
            ne.GenData();
            sw.GenData();
            se.GenData();
        }
        /// <summary>
        /// Subdivides the patch but doesn't generate mesh data for the new children.
        /// </summary>
        public void Split()
        {
            Vector3d centre = (nwc + nec + swc + sec) * 0.25;

            nw = new Patch(nwc, (nwc + nec) * 0.5, (nwc + swc) * 0.5, centre, planet_radius, side_len * 0.5, executor, this, level + 1);
            ne = new Patch((nwc + nec) * 0.5, nec, centre, (nec + sec) * 0.5, planet_radius, side_len * 0.5, executor, this, level + 1);
            sw = new Patch((nwc + swc) * 0.5, centre, swc, (swc + sec) * 0.5, planet_radius, side_len * 0.5, executor, this, level + 1);
            se = new Patch(centre, (nec + sec) * 0.5, (swc + sec) * 0.5, sec, planet_radius, side_len * 0.5, executor, this, level + 1);
        }
        /// <summary>
        /// Generates the mesh data from the position data.
        /// </summary>
        public void GenData()
        {
            //Interpolation constant
            const double INTERP = 1.0 / (I_SIDE_LEN - 1);

			//Avoid using a atomic value before the array is initialized completely, use temporary instead
			float[,] mesh_data_ptr = new float[I_NUM_VERTICES, 6];

			for (uint x = 0; x < SIDE_LEN; x++)
			{
				//Calcualte horizontal position
				double interp = INTERP * (double)x;
				Vector3d v1 = Vector3d.Lerp(nwc, nec, interp);
				Vector3d v2 = Vector3d.Lerp(swc, sec, interp);
				for (uint y = 0; y < SIDE_LEN; y++)
				{
					//Calculate vertical position
					Vector3d vtx = Vector3d.Lerp(v1, v2, INTERP * (double)y);
					Vector3d nvtx = vtx.Normalized();
					//Map to sphere
					vtx = nvtx * planet_radius;
					//Assign vertex position
                    vtx = vtx - pos;

					mesh_data_ptr[x * SIDE_LEN + y, 0] = (float)vtx.X;
                    mesh_data_ptr[x * SIDE_LEN + y, 1] = (float)vtx.Y;
                    mesh_data_ptr[x * SIDE_LEN + y, 2] = (float)vtx.Z;
					//Texcoord is normal as well, data compactness
					mesh_data_ptr[x * SIDE_LEN + y, 3] = (float)nvtx.X;
                    mesh_data_ptr[x * SIDE_LEN + y, 4] = (float)nvtx.Y;
                    mesh_data_ptr[x * SIDE_LEN + y, 5] = (float)nvtx.Z;
				}
			}

			//Skirt generation code

			/*
				Skirt is the position of the surface, but SKIRT_DEPTH units lower

				Calculate position on the sphere, then subtract SKIRT_DEPTH units
				Texture coordinate is still just normalized position
			*/

			//Vertex normal releative to planet centre
			Vector3d vnrm, final;
			//Sizeof base surface data
			uint data_size = SIDE_LEN * SIDE_LEN;
			for (uint i = 0; i < SIDE_LEN; i++)
			{
				
				vnrm = Vector3d.Lerp(nwc, swc, INTERP * (double)i).Normalized();
                final = ((vnrm * planet_radius - vnrm * SKIRT_DEPTH) - pos);

				mesh_data_ptr[data_size + i, 0] = (float)final.X;
                mesh_data_ptr[data_size + i, 1] = (float)final.Y;
                mesh_data_ptr[data_size + i, 2] = (float)final.Z;

				mesh_data_ptr[data_size + i, 3] = (float)vnrm.X;
                mesh_data_ptr[data_size + i, 4] = (float)vnrm.Y;
                mesh_data_ptr[data_size + i, 5] = (float)vnrm.Z;
			}
			data_size += SIDE_LEN;
			for (uint i = 0; i < SIDE_LEN; i++)
			{
				vnrm = Vector3d.Lerp(swc, sec, INTERP * (double)i).Normalized();
                final = ((vnrm * planet_radius - vnrm * SKIRT_DEPTH) - pos);

				mesh_data_ptr[data_size + i, 0] = (float)final.X;
                mesh_data_ptr[data_size + i, 1] = (float)final.Y;
                mesh_data_ptr[data_size + i, 2] = (float)final.Z;

				mesh_data_ptr[data_size + i, 3] = (float)vnrm.X;
                mesh_data_ptr[data_size + i, 4] = (float)vnrm.Y;
                mesh_data_ptr[data_size + i, 5] = (float)vnrm.Z;
			}
			data_size += SIDE_LEN;
			for (uint i = 0; i < SIDE_LEN; i++)
			{
				vnrm = Vector3d.Lerp(nec, sec, INTERP * (double)i).Normalized();
                final = ((vnrm * planet_radius - vnrm * SKIRT_DEPTH) - pos);

				mesh_data_ptr[data_size + i, 0] = (float)final.X;
                mesh_data_ptr[data_size + i, 1] = (float)final.Y;
                mesh_data_ptr[data_size + i, 2] = (float)final.Z;

				mesh_data_ptr[data_size + i, 3] = (float)vnrm.X;
                mesh_data_ptr[data_size + i, 4] = (float)vnrm.Y;
                mesh_data_ptr[data_size + i, 5] = (float)vnrm.Z;
			}
			data_size += SIDE_LEN;
			for (uint i = 0; i < SIDE_LEN; i++)
			{
				vnrm = Vector3d.Lerp(nwc, nec, INTERP * (double)i).Normalized();
                final = ((vnrm * planet_radius - vnrm * SKIRT_DEPTH) - pos);

				mesh_data_ptr[data_size + i, 0] = (float)final.X;
                mesh_data_ptr[data_size + i, 1] = (float)final.Y;
                mesh_data_ptr[data_size + i, 2] = (float)final.Z;

				mesh_data_ptr[data_size + i, 3] = (float)vnrm.X;
                mesh_data_ptr[data_size + i, 4] = (float)vnrm.Y;
                mesh_data_ptr[data_size + i, 5] = (float)vnrm.Z;
			}

            mesh_data = mesh_data_ptr;
        }

        /// <summary>
        /// Checks whether the patch meets the requirements for subdivision.
        /// </summary>
        /// <param name="CamPos"> The current camera position. </param>
        /// <returns></returns>
        public bool ShouldSubdivide(Vector3d CamPos)
        {
            double dis = Distance(pos, CamPos) - side_len;
            return side_len >= (I_SIDE_LEN - 1) * 16 && dis < I_SIDE_LEN;
        }
        /// <summary>
        /// Checks whether the current patch meets the requirements for merging.
        /// </summary>
        /// <param name="CamPos"> The current camera position. </param>
        /// <returns></returns>
        public bool ShouldMerge(Vector3d CamPos)
        {
            double dis = Distance(pos, CamPos) - side_len;
            return dis > side_len;
        }

        public bool CheckAndSubdivide(Vector3d CamPos)
        {
			//If the patch is subdivided
			if (IsSubdivided)
			{
				//If the patch is far enough away that it should be merged
				if (ShouldMerge(CamPos))
				{
					//Add the patch to the renderer's list to render
					RenderData = new DrawData(executor, pos);

					//Delete the patch's children
					MergeChildren();
					//Indicate that a change occured
					return true;
				}
				else
				{
					//If the patch should not be merged, continue recursive check
					bool r1 = nw.CheckAndSubdivide(CamPos);
					bool r2 = ne.CheckAndSubdivide(CamPos);
					bool r3 = sw.CheckAndSubdivide(CamPos);
					bool r4 = se.CheckAndSubdivide(CamPos);

					//Return results
					return r1 || r2 || r3 || r4;
				}
			}
			//If the patch should be subdivided
			else if (ShouldSubdivide(CamPos))
			{
				//Split but don't generate data
				Split();
				//Do that on another thread
				//Also put the patch in the remove list
                ThreadPool.QueueAsyncAuto(new Action(delegate{
                    nw.GenData();
                    ne.GenData();
                    sw.GenData();
                    se.GenData();

                    nw.RenderData = new DrawData(executor, nw.pos);
                    ne.RenderData = new DrawData(executor, ne.pos);
                    sw.RenderData = new DrawData(executor, sw.pos);
                    se.RenderData = new DrawData(executor, se.pos);

                    executor.CreateBuffer(nw.RenderData, nw.mesh_data);
                    executor.CreateBuffer(ne.RenderData, ne.mesh_data);
                    executor.CreateBuffer(sw.RenderData, sw.mesh_data);
                    executor.CreateBuffer(se.RenderData, se.mesh_data);
                }));
				//Indicate somthing happened
				return true;
			}
			//Nothing happened
			return false;
        }
        public bool ForceSubdivide(Vector3d cam_pos)
        {
            if (IsSubdivided)
            {
                if (ShouldMerge(cam_pos))
                {
                    MergeChildren();
                    return true;
                }
                else
                {
                    bool r1 = nw.ForceSubdivide(cam_pos);
                    bool r2 = ne.ForceSubdivide(cam_pos);
                    bool r3 = sw.ForceSubdivide(cam_pos);
                    bool r4 = se.ForceSubdivide(cam_pos);

                    return r1 || r2 || r3 || r4;
                }
            }
            else if (ShouldSubdivide(cam_pos))
            {
                Split();

                nw.ForceSubdivide(cam_pos);
                ne.ForceSubdivide(cam_pos);
                sw.ForceSubdivide(cam_pos);
                se.ForceSubdivide(cam_pos);

                return true;
            }

            if (mesh_data == null)
                GenData();

            return false;
        }

        internal void GenRenderData()
        {
            nw.RenderData = new DrawData(executor, nw.pos);
            ne.RenderData = new DrawData(executor, ne.pos);
            sw.RenderData = new DrawData(executor, sw.pos);
            se.RenderData = new DrawData(executor, se.pos);

            executor.CreateBuffer(nw.RenderData, nw.mesh_data);
            executor.CreateBuffer(ne.RenderData, ne.mesh_data);
            executor.CreateBuffer(sw.RenderData, sw.mesh_data);
            executor.CreateBuffer(se.RenderData, se.mesh_data);
        }
    }
}
