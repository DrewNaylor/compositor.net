
using System;
using Wayland.Server;
using Wayland.Server.Protocol;
using System.Threading;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Runtime.InteropServices;

namespace Starfury
{
    public class SfSurface : WlSurface, ISurface
    {
		public int X { get; set; } = 0;
		public int Y { get; set; } = 0;
		public int Width { get; set; }
		public int Height { get; set; }
		public int originX { get; set; } = 0;
		public int originY { get; set; } = 0;
		public IntPtr buffer = IntPtr.Zero;
		public WlCallback callback { get; set; }
		//public WlCallback callback = new WlCallback(client, 0, false);
		public bool callbackNeeded = false;
		// public Int32 width { get; set; } = 0;
		// public Int32 height { get; set; } = 0;
		public bool committed = false;
		public int texture = -1;

		public SfSurface(IntPtr client, UInt32 id) : base(client, id)
		{
			// Call the base class constructor
		}

		public override string ToString()
		{
			return String.Format("SfSurface@{0:X8}", resource.ToInt32());
		}

		public override void Attach(IntPtr client, IntPtr resource, IntPtr buffer, Int32 x, Int32 y)
		{			
			this.buffer = buffer;
		}
		
		public override void Frame(IntPtr client, IntPtr resource, UInt32 callbackId)
		{

			if (callbackNeeded)
			{
				callback.SendDone((UInt32) Starfury.Time.ElapsedMilliseconds); // Don't need remove because we pass in false
				callback.Remove();
			}

			// Instead of making a new WlCallback for every Frame just create a resource on the libwayland side
			if (callback == null)
			{
				callback = new WlCallback(client, callbackId, false);
			}
			else
			{
				callback.resource = Resource.Create(client, WaylandInterfaces.wlCallbackInterface.ifaceNative, 1, callbackId);
			}
			callbackNeeded = true;
		}

		public override void Commit(IntPtr client, IntPtr resource)
		{
			committed = true;
			this.CreateTexture();
			Starfury.renderNeeded = true;
		}

		public override void SetInputRegion(IntPtr client, IntPtr resource, IntPtr region)
		{
			
		}

		public override void SetOpaqueRegion(IntPtr client, IntPtr resource, IntPtr region)
		{
			
		}

		public override void Damage(IntPtr client, IntPtr resource, Int32 x, Int32 y, Int32 width, Int32 height)
		{

		}

		public override void Delete(IntPtr resource)
		{
			Starfury.RemoveSurface(this.resource);
			Starfury.renderNeeded = true;
		}

		public SfSurface GetSurface()
		{
			return this;
		}

		public void Render()
		{
			if (callback != null)
			{
				callback.SendDone(0);
				callback = null;
			}
		}

		public void Activate()
		{

		}

		public void Deactivate()
		{

		}

		public void SendMouseButton(uint time, uint button, uint state)
		{
			client.pointer?.SendButton(0, time, button, state);
		}

		public void SendMouseEnter(int x, int y)
		{
			client.pointer?.SendEnter(0, this.resource, x, y);
		}

		public void SendMouseLeave()
		{
			client.pointer?.SendLeave(0, this.resource);
		}

		public void SendMouseMove(uint time, int x, int y)
		{
			client.pointer?.SendMotion(time, 256 * x, 256 * y);
		}

		public void SendKeyboardEnter()
		{
			WlArray array = new WlArray();
			client.keyboard?.SendEnter(0, this.resource, array.array);
		}
		
		public void SendKeyboardLeave()
		{
			client.keyboard?.SendLeave(0, this.resource);
		}

		public void SendKey(uint time, uint key, uint state)
		{
			client.keyboard?.SendKey(0, time, key, state);
		}

		public void SendDone()
		{
			if (callbackNeeded)
			{
				callback.SendDone((UInt32) Starfury.Time.ElapsedMilliseconds);
				callback.Remove();
			}
			callbackNeeded = false;
		}

		public int Texture()
		{
			return texture;
		}

		public bool PointInSurface(int x, int y)
		{
			return x >= this.X && x <= (this.X + this.Width) && y >= this.Y && y <= (this.Y + this.Height);
		}

		public void Geometry(int vertAttrib, int texcoordAttrib)
		{			  
			Vector3[] verts = new Vector3[] { new Vector3(0.0f, 0.0f, 0.0f),
							  	new Vector3(0.0f, this.Height, 0.0f),
							  	new Vector3(this.Width, this.Height, 0.0f),
							  	new Vector3(this.Width, 0.0f, 0.0f) };

			Vector2[] texcoords = new Vector2[] { new Vector2(0.0f, 0.0f),
									new Vector2(0.0f, 1.0f),
									new Vector2(1.0f, 1.0f),
									new Vector2(1.0f, 0.0f) };

			uint[] indices = new uint[] { 0,1,2, 2,3,0 };
			//int vao;
			//GL.GenVertexArrays(1, out vao);
			//GL.BindVertexArray(vao);

			int vertbo;
			GL.GenBuffers(1, out vertbo);
			GL.BindBuffer(BufferTarget.ArrayBuffer, vertbo);
			GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, (IntPtr)(verts.Length * Vector3.SizeInBytes), verts, BufferUsageHint.StaticDraw);
			GL.VertexAttribPointer(vertAttrib, 3, VertexAttribPointerType.Float, false, 0, 0);

			int texcoordbo;
			GL.GenBuffers(1, out texcoordbo);
			GL.BindBuffer(BufferTarget.ArrayBuffer, texcoordbo);
			GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(texcoords.Length * Vector2.SizeInBytes), texcoords, BufferUsageHint.StaticDraw);
			GL.VertexAttribPointer(texcoordAttrib, 2, VertexAttribPointerType.Float, false, 0, 0);

			int ebo;
			GL.GenBuffers(1, out ebo);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
			GL.BufferData<uint>(BufferTarget.ElementArrayBuffer, (IntPtr)(indices.Length * Marshal.SizeOf(typeof(uint))), indices, BufferUsageHint.StaticDraw);

			
		}

		public void CreateTexture()
		{
			if (buffer != IntPtr.Zero)
			{
				SHMBuffer shmBuffer = new SHMBuffer(buffer);
				Width = shmBuffer.GetWidth();
				Height = shmBuffer.GetHeight();
				IntPtr data = shmBuffer.GetData();

				if (texture >= 0)
				{
					GL.DeleteTextures(1, ref texture);
				}

				GL.Enable(EnableCap.Texture2D);
				GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
				GL.GenTextures(1, out texture);
            	GL.BindTexture(TextureTarget.Texture2D, texture);
            	GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            	GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

            	GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
				Utils.ReleaseBuffer(buffer);
				buffer = IntPtr.Zero;
			}
		}
    }
}
