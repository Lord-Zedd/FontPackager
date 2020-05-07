using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FontPackager.Classes
{
	/// <summary>
	/// Defines a font.
	/// </summary>
	public class BlamFont : INotifyPropertyChanged
	{
		string _name;
		public string Name
		{
			get { return _name; }
			set { _name = value; NotifyPropertyChanged("Name"); }
		}

		public short AscendHeight { get; set; }
		public short DescendHeight { get; set; }
		public short LeadHeight { get; set; }
		public short LeadWidth { get; set; }

		public int CharacterCount
		{
			get { return Characters.Count; }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public int Unknown6 { get; set; }
		public int Unknown6b { get; set; }
		public int Unknown7 { get; set; }
		public int Unknown8 { get; set; }

		public int UnknownL1 { get; set; }
		public int UnknownL2 { get; set; }

		public int MCCScale { get; set; }
		public int UnknownMCC2 { get; set; }

		public List<KerningPair> KerningPairs { get; set; }

		public List<BlamCharacter> Characters { get; set; }

		public BlamFont(string name)
		{
			_name = name;
			Characters = new List<BlamCharacter>();
			KerningPairs = new List<KerningPair>();//kernman00
			Unknown6 = -1;
			Unknown6b = -1;
			MCCScale = 1;
		}

		/// <summary>
		/// A filename-safe version of the font name, with bad characters replaced with underscores.
		/// </summary>
		public string SanitizedName
		{
			get
			{
				string output = Name;
				foreach (char c in Path.GetInvalidFileNameChars())
					output = output.Replace(c, '_');

				output = output.Replace('.', '_');

				return output;
			}
		}

		/// <summary>
		/// Sorts the characters of a font. Called automatically by <see cref="AddCharacter(BlamCharacter, bool)"/>. 
		/// </summary>
		public void SortCharacters()
		{
			Characters = Characters.OrderBy(c => c.UnicIndex).ToList();
		}

		/// <summary>
		/// Returns the index into the Character list of the given unicode index.
		/// </summary>
		public int FindCharacter(ushort unicindex)
		{
			return Characters.FindIndex(c => c.UnicIndex.Equals(unicindex));
		}

		/// <summary>
		/// Adds the given <see cref="BlamCharacter"/> to the font.
		/// </summary>
		public void AddCharacter(BlamCharacter character)
		{
			int existingchar = FindCharacter(character.UnicIndex);

			if (existingchar != -1)
			{
				character.OriginY = Characters[existingchar].OriginY;
				Characters[existingchar] = character;
			}
			else
			{
				character.OriginY = AscendHeight;
				Characters.Add(character);
				NotifyPropertyChanged("CharacterCount");
				SortCharacters();
			}

		}

		/// <summary>
		/// Removes the character assigned to the given unicode index, then runs <see cref="RemoveKerningPairs(ushort)"/> on it.
		/// </summary>
		/// <returns>If the given character was removed.</returns>
		public bool RemoveCharacter(ushort unicindex)
		{
			int ind = FindCharacter(unicindex);

			if (ind != -1)
			{
				Characters.RemoveAt(ind);
				NotifyPropertyChanged("CharacterCount");
				RemoveKerningPairs(unicindex);

				return true;
			}
			else
				return false;

		}

		/// <summary>
		/// Removes any reference to the given unicode index from any related kerning pairs.
		/// </summary>
		/// <returns>If anything was removed.</returns>
		public bool RemoveKerningPairs(ushort unicindex)
		{
			if (unicindex > byte.MaxValue)
				return false;

			int oldcount = KerningPairs.Count;

			var kp = KerningPairs.Where(k => k.Character == (byte)unicindex);
			foreach (KerningPair k in kp)
				KerningPairs.Remove(k);

			var rkp = KerningPairs.Where(k => k.TargetCharacter == (byte)unicindex);
			foreach (KerningPair k in rkp)
				KerningPairs.Remove(k);

			if (KerningPairs.Count != oldcount)
				return true;

			return false;
		}

		/// <summary>
		/// Verifies this <see cref="BlamFont"/> against the given <see cref="FileFormat"/> and translates the results to a readable format.
		/// </summary>
		/// <returns>Any found errors, or an empty string.</returns>
		public string Verify(FileFormat format)
		{
			using (StringWriter sw = new StringWriter())
			{
				long compressedsize = 0;
				long decompressedsize = 0;

				foreach (BlamCharacter bc in Characters)
				{
					string cr = bc.Verify(format);

					compressedsize += bc.CompressedSize;
					decompressedsize += bc.DecompressedSize / (format.HasFlag(FileFormat.Table) ? 4 : 2);

					if (!string.IsNullOrEmpty(cr))
						sw.Write("Character: " + cr);
				}

				if (KerningPairs.Count > 0xFF)
					sw.WriteLine("Header: Kerning pair count " + KerningPairs.Count + " is greater than 255.");

				if (compressedsize > uint.MaxValue)
					sw.WriteLine("Header: Sum of compressed character data " + compressedsize.ToString() + " is greater than " + uint.MaxValue.ToString() + ".");

				if (decompressedsize > uint.MaxValue)
					sw.WriteLine("Header: Sum of decompressed character data " + decompressedsize.ToString() + " is greater than " + uint.MaxValue.ToString() + ".");


				return sw.ToString();
			}
		}

	}

	/// <summary>
	/// Defines an individual kerning pair for a character.
	/// </summary>
	public class KerningPair
	{
		public byte Character { get; set; }
		public byte TargetCharacter { get; set; }
		public short Value { get; set; }

		public KerningPair(byte character, byte targetcharacter, short value)
		{
			Character = character;
			TargetCharacter = targetcharacter;
			Value = value;
		}
	}

}
