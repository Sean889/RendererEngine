using System;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using LodPlanet;
using System.Drawing;
using System.IO;
using OpenTK.Input;
using CppThreadPool;

namespace Test
{
    class Program
    {
        static void LoadTexture(string filename, TextureTarget target)
        {
            if (String.IsNullOrEmpty(filename))
                throw new ArgumentException(filename);

            Bitmap bmp = new Bitmap(filename);
            System.Drawing.Imaging.BitmapData bmp_data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(target, 0, PixelInternalFormat.Rgba, bmp_data.Width, bmp_data.Height, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmp_data.Scan0);

            bmp.UnlockBits(bmp_data);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sides"></param>
        /// <returns></returns>
        static int LoadCubemap(PixelInternalFormat internalformat, PixelFormat format, params string[] sides)
        {
            if(sides.Length < 6)
                throw new ArgumentException("Sides must be an array of at least 6 in length");

            int texture = GL.GenTexture();

            GL.BindTexture(TextureTarget.TextureCubeMap, texture);
        
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, OpenGL.GL.GL_LINEAR);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, OpenGL.GL.GL_LINEAR);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, OpenGL.GL.GL_CLAMP_TO_EDGE);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, OpenGL.GL.GL_CLAMP_TO_EDGE);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, OpenGL.GL.GL_CLAMP_TO_EDGE);

            LoadTexture(sides[0], TextureTarget.TextureCubeMapPositiveX);
            LoadTexture(sides[1], TextureTarget.TextureCubeMapNegativeX);
            LoadTexture(sides[2], TextureTarget.TextureCubeMapPositiveY);
            LoadTexture(sides[3], TextureTarget.TextureCubeMapNegativeY);
            LoadTexture(sides[4], TextureTarget.TextureCubeMapPositiveZ);
            LoadTexture(sides[5], TextureTarget.TextureCubeMapNegativeZ);

            return texture;
        }

        struct srot
        {
            public double yaw;
            public double pitch;

            public Quaterniond Rotation
            {
                get
                {
                    if (yaw == 0 && pitch == 0)
                        return Quaterniond.Identity;
                    Quaterniond y = Quaterniond.FromAxisAngle(new Vector3d(0, 0, 1), yaw);
                    Quaterniond p = Quaterniond.FromAxisAngle(new Vector3d(0, 1, 0), pitch);

                    return y * p;
                }
            }
        }

        static srot CamRot = new srot();

        static void Main(string[] args)
        {
            GameWindow Win = new GameWindow(1024, 720);
            Win.MakeCurrent();

            GLCommandQueue CommandQueue = new GLCommandQueue();

            Camera Cam = new Camera(new Vector3d(), CamRot.Rotation);

            int ColourMap = LoadCubemap(PixelInternalFormat.Rgb8, PixelFormat.Bgra, 
                Directory.GetCurrentDirectory() + "//surface_diff_pos_x.png",
		        Directory.GetCurrentDirectory() + "//surface_diff_neg_x.png",
		        Directory.GetCurrentDirectory() + "//surface_diff_pos_y.png",
		        Directory.GetCurrentDirectory() + "//surface_diff_neg_y.png",
		        Directory.GetCurrentDirectory() + "//surface_diff_pos_z.png",
		        Directory.GetCurrentDirectory() + "//surface_diff_neg_z.png");

            int NormalMap = LoadCubemap(PixelInternalFormat.Rgb8, PixelFormat.Bgra,
                Directory.GetCurrentDirectory() + "//surface_norm_pos_x.png",
                Directory.GetCurrentDirectory() + "//surface_norm_neg_x.png",
                Directory.GetCurrentDirectory() + "//surface_norm_pos_y.png",
                Directory.GetCurrentDirectory() + "//surface_norm_neg_y.png",
                Directory.GetCurrentDirectory() + "//surface_norm_pos_z.png",
                Directory.GetCurrentDirectory() + "//surface_norm_neg_z.png");

            int HeightMap = LoadCubemap(PixelInternalFormat.R32f, PixelFormat.Bgra,
                Directory.GetCurrentDirectory() + "//surface_bump_pos_x.png",
                Directory.GetCurrentDirectory() + "//surface_bump_neg_x.png",
                Directory.GetCurrentDirectory() + "//surface_bump_pos_y.png",
                Directory.GetCurrentDirectory() + "//surface_bump_neg_y.png",
                Directory.GetCurrentDirectory() + "//surface_bump_pos_z.png",
                Directory.GetCurrentDirectory() + "//surface_bump_neg_z.png");

            Planet Planet = new Planet(new Vector3d(0, 0, 0), Quaterniond.Identity, CommandQueue, Cam, 10000, ColourMap, HeightMap, NormalMap);

            bool[] keys = new bool[8];

            ThreadPool.Init(1);

            GL.ClearColor(Color.Black);

            CommandQueue.SwitchFrames();
            CommandQueue.ExecuteOldFrame();

            Win.UpdateFrame += (sender, e) =>
                {
                    Planet.CheckAndSubdivide();
                    Planet.GetRenderer().Update();
                };

            Win.KeyDown += (sender, e) =>
                {
                    Matrix4d mat = Matrix4d.CreateFromQuaternion(CamRot.Rotation);

                    Vector3d left = new Vector3d(mat[0,0], mat[0,1], mat[0,2]),
                        up = new Vector3d(mat[1, 0], mat[1, 1], mat[1, 2]),
                        front = new Vector3d(mat[2, 0], mat[2, 1], mat[2, 2]);

                    if (e.Key == Key.W)
                        Cam.Position += front * 2;
                    if (e.Key == Key.S)
                        Cam.Position -= front * 2;
                    if (e.Key == Key.D)
                        Cam.Position += left * 2;
                    if (e.Key == Key.A)
                        Cam.Position -= left * 2;
                    if (e.Key == Key.E)
                        Cam.Position += up * 2;
                    if (e.Key == Key.Q)
                        Cam.Position -= up * 2;

                    if (e.Key == Key.I)
                        CamRot.pitch += 2;
                    if (e.Key == Key.K)
                        CamRot.pitch -= 2;
                    if (e.Key == Key.L)
                        CamRot.yaw += 2;
                    if (e.Key == Key.J)
                        CamRot.yaw -= 2;

                    if (e.Key == Key.Space)
                    {
                        CamRot.pitch = 0;
                        CamRot.yaw = 0;
                        Cam.Position = new Vector3d();
                    }

                    Cam.Rotation = CamRot.Rotation;
                };

            Win.RenderFrame += (sender, e) =>
                {
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    CommandQueue.SwitchFrames();
                    Planet.Draw();
                    CommandQueue.ExecuteOldFrame();
                    Win.SwapBuffers();
                };

            Win.Run();
        }
    }
}
