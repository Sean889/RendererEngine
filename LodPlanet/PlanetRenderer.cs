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

        DrawData(PlanetRenderer renderer, Vector3d offset)
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

        List<WeakReference<DrawData>> RenderList = new List<WeakReference<DrawData>>();

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
            }
        }

        public void DrawBuffers()
        {
            foreach(DrawData buf in this)
            {
                
            }
        }
    }
}
