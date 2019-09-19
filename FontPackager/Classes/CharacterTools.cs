using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace FontPackager.Classes
{
	/// <summary>
	/// Tools for interacting with and creating <see cref="BlamCharacter"/> data.
	/// </summary>
	public static class CharacterTools
	{
		/// <summary>
		/// Decompresses the given character's compressed data.
		/// </summary>
		/// <param name="bc">Character with the data to decompress.</param>
		/// <returns>Whether the decompression could complete.</returns>
		public static bool DecompressData(BlamCharacter bc)
		{
			if (bc.CompressedData == null || bc.CompressedData.Length == 0)
				return false;

			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					ushort baseColor = 0xFFF;

					for (int i = 0; i < bc.CompressedData.Length; i++)
					{
						byte Code = bc.CompressedData[i];

						byte codeFlags = (byte)(Code >> 6);
						byte codeValue = (byte)(Code & 0x3F);

						byte pixCount = 0;
						ushort newColor = 0;

						if (codeFlags < 2)
						{
							if (Code == 0)
							{
								newColor = (ushort)(((bc.CompressedData[i + 1] << 8) | bc.CompressedData[i + 2]));
								i += 2;

								bw.Write(CreateRun(1, newColor));
								baseColor = (ushort)(newColor & 0xFFF);
							}
							else
							{
								pixCount = codeValue;
								uint orVal = 0;

								if (codeFlags != 0)
									orVal = 0xF000;

								bw.Write(CreateRun(pixCount, (ushort)(baseColor | orVal)));
							}
							continue;
						}

						newColor = (ushort)((Code >> 3) & 7);
						newColor = (ushort)((newColor << 1) | (newColor & 1));
						newColor = (ushort)((newColor << 12) | baseColor);

						bw.Write(CreateRun(1, newColor));

						ushort threebitVal = (ushort)(Code & 7);
						if (codeFlags != 2)
						{
							newColor = (ushort)((threebitVal << 1) | (threebitVal & 1));
							newColor = (ushort)((newColor << 12) | baseColor);

							bw.Write(CreateRun(1, newColor));
						}
						else if (threebitVal == 0)
						{
							bw.BaseStream.Position += pixCount;
						}
						else
						{
							pixCount = 0;
							newColor = baseColor;

							if (threebitVal == 0)
								continue;

							if ((threebitVal & 4) == 0)
								newColor = (ushort)((baseColor) | 0xF000);

							pixCount = (byte)Math.Abs(5 - ((threebitVal & 3)));

							bw.Write(CreateRun(pixCount, newColor));

						}
					}

					bc.DecompressedData = ms.ToArray();
					return true;
				}
			}
		}

		internal static byte[] CreateRun(int count, ushort color)
		{
			byte[] output = new byte[4 * count];

			for (int i = 0; i < count; i++)
			{
				int offset = i * 4;
				output[offset + 0] = (byte)((color & 0x000F) * 255 / 15);
				output[offset + 1] = (byte)(((color & 0x00F0) >> 4) * 255 / 15);
				output[offset + 2] = (byte)(((color & 0x0F00) >> 8) * 255 / 15);
				output[offset + 3] = (byte)(((color & 0xF000) >> 12) * 255 / 15);
			}

			return output;

		}
		/// <summary>
		/// Compresses the given character's decompressed data.
		/// </summary>
		/// <param name="bc">Character with the data to compress.</param>
		/// <param name="tint">The tint to apply to the compressed pixels. Default is no tint.</param>
		/// <returns>Whether the compression could complete.</returns>
		public static bool CompressData(BlamCharacter bc, CharTint tint = CharTint.None)
		{
			if (bc.DecompressedData == null || bc.DecompressedData.Length < 4)
				return false;

			ushort[] shrunkpixels = new ushort[bc.DecompressedData.Length / 4];
			for (int i = 0; i < bc.DecompressedData.Length / 4; i++)
			{
				int offset = i * 4;
				byte AR = (byte)(((bc.DecompressedData[offset + 3] & 0xF0)) | ((bc.DecompressedData[offset + 2] >> 4)));
				byte GB = (byte)(((bc.DecompressedData[offset + 1] & 0xF0)) | ((bc.DecompressedData[offset + 0] >> 4)));

				ushort result = (ushort)((AR << 8) | GB);

				switch (tint)
				{
					case CharTint.Cool:
						if ((result & 0xFFF) == 0x000) result += 1;
						else if (((result & 0xFFF) % 0x111) == 0) result -= 0x100;
						break;

					case CharTint.Warm:
						if ((result & 0xFFF) == 0x000) result += 0x100;
						else if (((result & 0xFFF) % 0x111) == 0) result -= 1;
						break;
				}

				shrunkpixels[i] = result;
			}

			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					ushort basepixel = 0xFFF;

					if (bc.Width > 1)
						for (int i = 0; i < shrunkpixels.Length; i++)
						{
							byte run = 0;

							ushort currentpixel = shrunkpixels[i];
							byte currentalpha = (byte)((currentpixel & 0xF000) >> 12);

							if ((currentpixel & 0xFFF) != (basepixel & 0xFFF) && currentalpha != 0)
							{
								bw.Write((byte)0);
								bw.Write((byte)((currentpixel & 0xFF00) >> 8));
								bw.Write((byte)currentpixel);

								basepixel = currentpixel;
								continue;
							}
							else
							{
								if (currentalpha == 0 || currentalpha == 0xF)
								{
									while ((i + run) < shrunkpixels.Length && shrunkpixels[i + run] == currentpixel && run < 0x3F)
										run++;
								}
								if (run > 1)
								{
									bw.Write((byte)(((currentalpha & 4) << 4) | run));
									i += run - 1;
									continue;
								}

								run = 0;
								byte codeL = 0;

								codeL = (byte)((currentalpha & 0xE) << 2);
								codeL |= 0x80;

								byte codeR = 0;

								ushort? nextpixel = 0;
								byte nextalpha = 0;

								if (i + 1 < shrunkpixels.Length)
								{
									nextpixel = shrunkpixels[i + 1];
									nextalpha = (byte)((nextpixel & 0xF000) >> 12);
								}

								if (nextpixel.HasValue)
								{
									while ((i + 1 + run) < shrunkpixels.Length && (shrunkpixels[i + 1 + run]) == (nextpixel) && run < 5)
										run++;
									if (run > 1 && nextalpha != 0 && nextalpha != 0xF)
										run = 1;
								}

								if (run == 0)
								{
									bw.Write(codeL);
									continue;
								}
								else if (run == 1)
								{
									codeR = (byte)(nextalpha >> 1);

									bw.Write((byte)((codeL | codeR) | 0x40));
									i += run;
									continue;
								}
								else
								{
									if (nextalpha != 0xF)
										codeR = 4;
									else if (nextalpha == 0xF && run == 5)
										run--;

									codeR |= (byte)(5 - run);
								}

								bw.Write((byte)(codeL | codeR));
								i += run;
							}
						}
					else
						bw.Write((byte)0x15);

					bc.CompressedData = ms.ToArray();
				}
			}

			return true;
		}
		
		/// <summary>
		/// Returns a <see cref="Rectangle"/> that represents the actual pixel bounds without any empty space.
		/// </summary>
		public static Rectangle GetImageBounds(Image image)
		{
			int stride;
			int width;
			int height;
			byte[] data;

			using (Bitmap bm = new Bitmap(image))
			{
				BitmapData bd = bm.LockBits(
					new Rectangle(new Point(0, 0), bm.Size),
					ImageLockMode.ReadOnly,
					bm.PixelFormat);
				int length = Math.Abs(bd.Stride) * bd.Height;
				data = new byte[length];
				System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, data, 0, length);

				stride = bd.Stride;
				width = bd.Width;
				height = bd.Height;
				bm.UnlockBits(bd);
			}

			int left = int.MaxValue;
			int right = 0;
			int top = int.MaxValue;
			int bottom = 0;

			for (int i = 0; i < height; i++)
				for (int ii = 0; ii < width; ii++)
				{
					var a = data[i * stride + 4 * ii + 3];
					if (a > 0)
					{
						if (ii < left)
							left = ii;
						if (ii > right)
							right = ii;

						if (i < top)
							top = i;
						if (i > bottom)
							bottom = i;
					}
				}

			if (left > width)
				left = 1;
			if (right < left)
				right = left + 1;

			if (top > height)
				top = 1;
			if (bottom < top)
				bottom = top + 1;

			return Rectangle.FromLTRB(left, top, right, bottom);
		}

		/// <summary>
		/// Creates a new <see cref="BlamCharacter"/> from an image.
		/// </summary>
		/// <param name="unicindex">The unicode index of the new character.</param>
		/// <param name="image">The image to convert.</param>
		/// <param name="tint">The tint to apply to the character. Default is no tint.</param>
		/// <param name="crop">Trims empty space of either side of the given image. Default is true.</param>
		/// <returns>A BlamCharacter</returns>
		public static BlamCharacter CreateCharacter(ushort unicindex, Image image, CharTint tint, bool crop)
		{
			Rectangle bounds;
			return CreateCharacter(unicindex, image, tint, crop, out bounds);
		}

		/// <summary>
		/// Creates a new <see cref="BlamCharacter"/> from an image.
		/// </summary>
		/// <param name="unicindex">The unicode index of the new character.</param>
		/// <param name="image">The image to convert.</param>
		/// <param name="tint">The tint to apply to the character. Default is no tint.</param>
		/// <param name="crop">Trims empty space of either side of the given image. Default is true.</param>
		public static BlamCharacter CreateCharacter(ushort unicindex, Image image, CharTint tint, bool crop, out Rectangle bounds)
		{
			bounds = new Rectangle(0, 0, image.Width, image.Height);
			if (image.Width > ushort.MaxValue || image.Height > ushort.MaxValue || image.PixelFormat != PixelFormat.Format32bppArgb)
				return null;

			Bitmap workingimage = new Bitmap(image);
			bounds = GetImageBounds(image);

			if (crop)
			{
				Rectangle nopad = Rectangle.FromLTRB(bounds.Left, 0, bounds.Right + 1, image.Height);
				Rectangle rect = new Rectangle(0, 0, nopad.Width, nopad.Height);

				workingimage.Dispose();
				workingimage = new Bitmap(nopad.Width, image.Height);
				using (Graphics g = Graphics.FromImage(workingimage))
				{
					g.DrawImage(image, rect, nopad, GraphicsUnit.Pixel);
				}
			}

			BitmapData bd = workingimage.LockBits(
				new Rectangle(new Point(0, 0), workingimage.Size),
				ImageLockMode.ReadOnly,
				workingimage.PixelFormat);
			int length = Math.Abs(bd.Stride) * bd.Height;
			byte[] data = new byte[length];
			System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, data, 0, length);
			workingimage.UnlockBits(bd);

			BlamCharacter bc = new BlamCharacter(unicindex);

			if (crop)
			{
				bc.OriginX = (short)(bounds.Left);
				bc.DisplayWidth = (uint)(image.Width - bounds.Left);
			}
			else
				bc.DisplayWidth = (uint)workingimage.Width;

			bc.Width = (ushort)workingimage.Width;
			bc.Height = (ushort)workingimage.Height;

			bc.OriginY = (short)(bounds.Bottom + 1);

			workingimage.Dispose();

			bc.DecompressedData = data;
			CompressData(bc, tint);
			DecompressData(bc);
			
			return bc;
		}
		
	}
}
