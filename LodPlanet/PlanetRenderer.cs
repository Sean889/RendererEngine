using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Threading;

namespace LodPlanet
{

    public class DrawData
    {
        private PlanetRenderer Manager; //Buffer manager

        public Vector3d Offset;         //Point on the planet
        public uint BufferID = 0;       //OpenGL buffer id

        internal void RemoveSelf()
        {
            Manager.RemoveBuffer(this);
        }

        public void Bind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferID);
        }
        public bool Valid()
        {
            return BufferID != 0;
        }

        public DrawData(PlanetRenderer renderer, Vector3d offset)
        {
            Manager = renderer;
            Offset = offset;
        }

        ~DrawData()
        {
            Manager.DeleteBuffer(new PlanetRenderer.DrawDataInst(BufferID));
        }
    }

    //Global buffer manager
    public class PlanetRenderer : IEnumerable
    {
        //Class that holds the data necessary to clean up after a vertex buffer
        internal class DrawDataInst
        {
            internal uint BufferID;

            internal DrawDataInst(uint buf)
            {
                BufferID = buf;
            }
        }

        #region ShaderCode
        private const string VertShader = ""
            + "#version 430\n"

            + "layout(location = 0) in vec3 vert;"
            + "layout(location = 1) in vec3 norm;"
            + "layout(location = 2) in vec3 texcoord;"
            + "layout(location = 0) uniform mat4 mvp;"
            + "layout(location = 4) uniform mat4 m_rot;"
            + "layout(location = 25) uniform vec3 eye;"

            + "smooth out vec3 normal;"
            + "smooth out vec3 texcoord0;"
            + "smooth out vec3 eye_dir;"

            + "void main()"
            + "{"
	        + "    normal = (m_rot * vec4(normalize(norm), 1.0)).xyz;"
	        + "    texcoord0 = texcoord;"
	        + "    eye_dir = normalize(vert - eye);"
	        + "    gl_Position = mvp * vec4(vert, 1.0);"
            + "}";
        private const string FragShader = ""
            + "#version 430\n"

            + "in vec3 normal;"
            + "in vec3 texcoord0;"
            + "in vec3 eye_dir;"
            
            + "layout(location = 20) uniform float shininess;"
            + "layout(location = 21) uniform vec4 ambient;"
            + "layout(location = 22) uniform vec4 specular;"
            + "layout(location = 23) uniform samplerCube tex;"
            + "//World space light vector"
            + "layout(location = 24) uniform vec3 lightdir;"

            + "out vec4 colour;"

            + "void main()"
            + "{"
            + "    // set the specular term to black"
            + "    vec4 spec = vec4(0.0);"
 
            + "    // normalize both input vectors"
            + "    vec3 n = normalize(normal);"
            + "    vec3 e = eye_dir;"
 
             + "   float intensity = max(dot(n,lightdir), 0.0);"
 
             + "   // if the vertex is lit compute the specular colour"
             + "   if (intensity > 0.0) {"
             + "        vec3 h = normalize(lightdir + e);  "
             + "       // compute the specular term into spec"
             + "       float intSpec = max(dot(h,n), 0.0);"
             + "       spec = specular * pow(intSpec,shininess);"
             + "   }"
             + "   colour = max(intensity *  texture(tex, texcoord0) + spec / 4.0, ambient);"
	         + "   colour = vec4((dot(n, lightdir) + 0.2) * texture(tex, texcoord0));"
             + "}";
        #endregion

        private int IndexBuffer;
        private int Shader;

        private int ColourTexture;
        private int HeightMap;
        private int NormalMap;

        private GLCommandQueue CommandQueue;

        public Planet PlanetInst;
        public Camera CameraInst;
        
        private struct Pair
        {
            internal DrawData buffer;
            internal float[,] vertices;

            internal Pair(DrawData buf, float[,] verts)
            {
                buffer = buf;
                vertices = verts;
            }
        }

        private ConcurrentQueue<DrawDataInst> ToDelete = new ConcurrentQueue<DrawDataInst>();
        private ConcurrentQueue<Pair> ToCreate = new ConcurrentQueue<Pair>();
        private ConcurrentQueue<DrawData> ToRemove = new ConcurrentQueue<DrawData>();

        private List<WeakReference<DrawData>> RenderList = new List<WeakReference<DrawData>>();

        private static Matrix4 ConvertToMatrix(Matrix4d mat)
        {
            return new Matrix4(
                (float)mat.M11, (float)mat.M12, (float)mat.M13, (float)mat.M14,
                (float)mat.M21, (float)mat.M22, (float)mat.M23, (float)mat.M24,
                (float)mat.M31, (float)mat.M32, (float)mat.M33, (float)mat.M34,
                (float)mat.M41, (float)mat.M42, (float)mat.M43, (float)mat.M44);
        }
        private static Vector3 ConvertToVector(Vector3d vec)
        {
            return new Vector3((float)vec.X, (float)vec.Y, (float)vec.Z);
        }

        internal void DeleteBuffer(DrawDataInst buf)
        {
            ToDelete.Enqueue(buf);
        }
        internal void CreateBuffer(DrawData buf, float[,] mesh_data)
        {
            ToCreate.Enqueue(new Pair(buf, mesh_data));
        }
        internal void RemoveBuffer(DrawData buf)
        {
            ToRemove.Enqueue(buf);
        }

        public IEnumerator GetEnumerator()
        {
            Stack<int> RemoveQueue = new Stack<int>();

            int size = RenderList.Count;
            for(int i = 0; i < size; i++)
            {
                DrawData buf;
                if(!RenderList[i].TryGetTarget(out buf))
                {
                    RemoveQueue.Push(i);
                }
                else
                {
                    yield return buf;
                }
            }

            while(RemoveQueue.Count != 0)
            {
                RenderList.RemoveAt(RemoveQueue.Pop());
            }
        }

        public void Update()
        {
            CommandQueue.EnqueueCommand(new Action(delegate
            {
                Pair pair;
                while (ToCreate.TryDequeue(out pair))
                {
                    pair.buffer.BufferID = (uint)GL.GenBuffer();
                    GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(pair.vertices.LongLength * 6 * sizeof(float)), pair.vertices, BufferUsageHint.StaticDraw);
                    RenderList.Add(new WeakReference<DrawData>(pair.buffer));
                }

                DrawData buf;
                while (ToRemove.TryDequeue(out buf))
                {
                    RenderList.Remove(new WeakReference<DrawData>(buf));
                }

                DrawDataInst inst;
                while (ToDelete.TryDequeue(out inst))
                {
                    GL.DeleteBuffer(inst.BufferID);
                }
            }));
        }

        public void Draw()
        {
            CommandQueue.EnqueueCommand(new Action(delegate
            {
                GL.UseProgram(Shader);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.TextureCubeMap, ColourTexture);

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.TextureCubeMap, HeightMap);

                GL.ActiveTexture(TextureUnit.Texture2);
                GL.BindTexture(TextureTarget.TextureCubeMap, NormalMap);

                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);

                Matrix4d temp = Matrix4d.CreateFromQuaternion(PlanetInst.Rotation);
                Matrix4d trans =
                    Matrix4d.Perspective(CameraInst.FovY, CameraInst.Aspect, CameraInst.NearZ, CameraInst.FarZ)
                    * Matrix4d.CreateFromQuaternion(CameraInst.Rotation).Inverted()
                    * Matrix4d.CreateTranslation(CameraInst.Position).Inverted()
                    * (temp * Matrix4d.CreateTranslation(PlanetInst.Position));
                Matrix4 rtm = ConvertToMatrix(temp);

                Vector3d RCamPos = PlanetInst.Position - CameraInst.Position;

                foreach (DrawData data in this)
                {
                    temp = Matrix4d.CreateTranslation(data.Offset);
                    Matrix4 mat = ConvertToMatrix(temp);

                    GL.BindBuffer(BufferTarget.ArrayBuffer, data.BufferID);

                    GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, sizeof(float) * 6, 0);
                    GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, true, sizeof(float) * 6, sizeof(float) * 3);

                    GL.UniformMatrix4(0, false, ref mat);
                    GL.UniformMatrix4(0, false, ref rtm);

                    //Maximum mesh deformation
                    GL.Uniform1(9, 1024);

                    //Texture IDs
                    GL.Uniform1(10, 0);
                    GL.Uniform1(11, 1);
                    GL.Uniform1(12, 2);

                    //Light direction
                    GL.Uniform3(15, 0.0f, 0.0f, 1.0f);

                    //Eye position
                    GL.Uniform3(20, ConvertToVector(RCamPos + data.Offset));

                    //Draw call
                    GL.DrawElements(BeginMode.Triangles, (int)Patch.I_NUM_INDICES, DrawElementsType.UnsignedInt, 0);
                }
            }));
        }

        PlanetRenderer(GLCommandQueue CommandQueue, Camera cam, Planet planet, int ColourTexture, int HeightMap, int NormalMap)
        {
            this.CameraInst = cam;
            this.PlanetInst = planet;
            this.ColourTexture = ColourTexture;
            this.HeightMap = HeightMap;
            this.NormalMap = NormalMap;
            this.CommandQueue = CommandQueue;

            CommandQueue.EnqueueCommand(new Action(delegate
            {
                IndexBuffer = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, IndexBuffer);
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(sizeof(uint) * Patch.I_NUM_INDICES), Patch.INDICES, BufferUsageHint.StaticRead);

                Shader = GL.CreateProgram();
                int vrt = GL.CreateShader(ShaderType.VertexShader);
                int frg = GL.CreateShader(ShaderType.FragmentShader);

                GL.ShaderSource(vrt, VertShader);
                GL.ShaderSource(frg, FragShader);

                GL.CompileShader(vrt);
                GL.CompileShader(frg);

                GL.AttachShader(Shader, vrt);
                GL.AttachShader(Shader, frg);

                GL.LinkProgram(Shader);

                GL.DetachShader(Shader, vrt);
                GL.DetachShader(Shader, vrt);

                GL.DeleteShader(vrt);
                GL.DeleteShader(frg);
            }));
        }
        ~PlanetRenderer()
        {
            CommandQueue.EnqueueCommand(new Action(delegate
            {
                GL.DeleteProgram(Shader);
                GL.DeleteBuffer(IndexBuffer);
            }));
        }
    }
}
