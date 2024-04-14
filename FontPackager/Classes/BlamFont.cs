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

		public int MCCScale { get; set; }
		public int UnknownMCC { get; set; }//not read/used?

		public List<KerningPair> KerningPairs { get; set; }

		public List<BlamCharacter> Characters { get; set; }

		public BlamFont(string name)
		{
			_name = name;
			Characters = new List<BlamCharacter>();
			KerningPairs = new List<KerningPair>();//kernman00
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
		/// Tries to find the relevant kerning pair value for the given characters.
		/// </summary>
		/// <param name="sourceChar">The left character in the pair.</param>
		/// <param name="targetChar">The right character in the pair.</param>
		/// <param name="value">The resulting kering pair value, if it exists.</param>
		/// <returns>If a kerning pair was found.</returns>
		public bool TryFindKerningValue(ushort sourceChar, ushort targetChar, out int value)
		{
			value = 0;
			if (sourceChar > byte.MaxValue || targetChar > byte.MaxValue)
				return false;

			var kp = KerningPairs.Where(k => k.Character == (byte)sourceChar);
			foreach (KerningPair k in kp)
			{
				if (k.TargetCharacter == targetChar)
				{
					value = k.Value;
					return true;
				}	
			}
			return false;
		}

		/// <summary>
		/// Verifies this <see cref="BlamFont"/> against the given <see cref="FormatInformation"/>.
		/// </summary>
		/// <returns>Any found errors.</returns>
		public List<VerificationResult> Verify(FormatInformation info)
		{
			long compressedsize = 0;
			long decompressedsize = 0;
			List<VerificationResult> results = new List<VerificationResult>();

			foreach (BlamCharacter bc in Characters)
			{
				results.AddRange(bc.Verify(info));

				compressedsize += bc.CompressedSize;
				decompressedsize += bc.DecompressedSize / (info.Format == FileFormat.Table ? 4 : 2);
			}

			if (KerningPairs.Count > 0xFF)
				results.Add(new VerificationResult($"Header: Kerning Pair Count {KerningPairs.Count} is greater than the max of 255.", true));

			if (compressedsize > uint.MaxValue)
				results.Add(new VerificationResult($"Header: Sum of compressed character data {compressedsize:X} is greater than the max value of {uint.MaxValue:X}.", true));

			if (decompressedsize > uint.MaxValue)
				results.Add(new VerificationResult($"Header: Sum of decompressed character data {decompressedsize:X} is greater than the max value of {uint.MaxValue:X}", true));

			return results;
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
