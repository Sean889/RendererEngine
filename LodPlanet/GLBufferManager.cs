using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using OpenTK.Graphics.OpenGL;
using System.Threading;

namespace LodPlanet
{
    class VertexBuffer
    {
        internal uint BufferID = 0;

        internal void RemoveSelf()
        {
            GLBufferManager.Global.RemoveBuffer(this);
        }

        public void Bind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferID);
        }
        public bool Valid()
        {
            return BufferID != 0;
        }

        ~VertexBuffer()
        {
            GLBufferManager.Global.DeleteBuffer(new GLBufferManager.VertexBufferInst(BufferID));
        }
    }

    //Global buffer manager
    public class GLBufferManager : IEnumerable<VertexBuffer>
    {
        internal class VertexBufferInst
        {
            internal uint BufferID;

            internal VertexBufferInst(uint buf)
            {
                BufferID = buf;
            }
        }

        private struct Pair
        {
            internal VertexBuffer buffer;
            internal float[,] vertices;

            internal Pair(VertexBuffer buf, float[,] verts)
            {
                buffer = buf;
                vertices = verts;
            }
        }

        private ConcurrentQueue<VertexBufferInst> ToDelete;
        private ConcurrentQueue<Pair> ToCreate;
        private ConcurrentQueue<VertexBuffer> ToRemove;

        public class Iterator : IEnumerator<VertexBuffer>
        {
            private VertexBuffer buf;

            public VertexBuffer Current
            {
                get
                {
                    return buf;
                }
            }

            public Iterator(VertexBuffer buffer)
            {
                buf = buffer;
            }
        }

        List<WeakReference<VertexBuffer>> RenderList;

        internal static GLBufferManager Global;

        internal void DeleteBuffer(VertexBufferInst buf)
        {
            ToDelete.Enqueue(buf);
        }
        internal void CreateBuffer(VertexBuffer buf, float[,] mesh_data)
        {
            ToCreate.Enqueue(new Pair(buf, mesh_data));
        }
        internal void RemoveBuffer(VertexBuffer buf)
        {
            ToRemove.Enqueue(buf);
        }

        public IEnumerator<VertexBuffer> GetEnumerator()
        {
            Stack<int> RemoveQueue = new Stack<int>();

            int size = RenderList.Count;
            for(int i = 0; i < size; i++)
            {
                VertexBuffer buf;
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
                    RenderList.Add(new WeakReference<VertexBuffer>(pair.buffer));
                }

                VertexBuffer buf;
                while (ToRemove.TryDequeue(out buf))
                {
                    RenderList.Remove(new WeakReference<VertexBuffer>(buf));
                }

                VertexBufferInst inst;
                while (ToDelete.TryDequeue(out inst))
                {
                    GL.DeleteBuffer(inst.BufferID);
                }
            }
        }

        public void DrawBuffers()
        {
            foreach(VertexBuffer buf in this)
            {
                
            }
        }
    }
}
