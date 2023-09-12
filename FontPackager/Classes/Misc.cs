﻿using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace FontPackager.Classes
{
	public enum IOError
	{
		None = 0,
		BadVersion = 1,
		UnknownBlock = 2,
		Empty = 3,
	}

	public enum CharTint
	{
		None,
		Cool,
		Warm
	}

	public static class ReaderExtensions
	{
		public static string ReadStringToNull(this BinaryReader br, int maxlength = -1)
		{
			string output = "";
			char c;

			int maximum = maxlength;

			if (maximum == -1)
				maximum = (int)br.BaseStream.Length - (int)br.BaseStream.Position;

			for (int j = 0; j < maximum; j++)
			{
				c = (char)br.ReadByte();
				if (c == 0)
				{
					if (maxlength != -1)
						br.BaseStream.Position += maximum - 1 - j;
					break;
				}

				output += c.ToString();
			}

			return output;
		}

	}

	public static class DPIHelper
	{
		public static readonly int DPI;
		public static readonly float DPIScale;

		static DPIHelper()
		{
			Matrix dpim = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice;
			DPIScale = (float)dpim.M11;
			DPI = (int)(DPIScale * 96);
		}
	}

	public class EngineOrderItem
	{
		public BlamFont Font { get; set; }

		public EngineOrderItem(BlamFont font)
		{
			Font = font;
		}
	}
}
