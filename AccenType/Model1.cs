using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utility;

namespace VietAccentor
{
    public class Model1 : ILanguageModel
    {
        public int Version { get { return 1; } }

        public Dictionary<int, Dictionary<string, int>> Map { get { return m_Map; } }
        private Dictionary<int, Dictionary<string, int>> m_Map;

        public Model1()
        {
            m_Map = new Dictionary<int, Dictionary<string, int>>();
        }

        public void Add(string key, string value)
        {
            int hashCode = key.GetHashCode();

            if (!m_Map.ContainsKey(hashCode))
            {
                m_Map.Add(hashCode, new Dictionary<string, int>());
            }
            if (!m_Map[hashCode].ContainsKey(value))
            {
                m_Map[hashCode].Add(value, 1);
            }
            else
            {
                m_Map[hashCode][value]++;
            }
        }

        public Dictionary<string, int> GetAccents(string rawSegment)
        {
            int hashCode = rawSegment.GetHashCode();
            if (m_Map.ContainsKey(hashCode))
            {
                return m_Map[hashCode];
            }
            else
            {
                return null;
            }
        }

        public void WriteToBinary(BinaryWriter bw)
        {
            var converter = new Utility.VietConverter();

            var uniqueAccentStrings = new HashSet<string>();
            var uniqueAccentCodeCount = new HashSet<int>();
            int maxHashEntryCount = 0;
            foreach (Dictionary<string, int> accCodeToCount in m_Map.Values)
            {
                foreach (string accString in accCodeToCount.Keys)
                {
                    uniqueAccentStrings.Add(accString);
                    uniqueAccentCodeCount.Add(accCodeToCount[accString]);
                }

                maxHashEntryCount = Math.Max(maxHashEntryCount, accCodeToCount.Count);
            }
            int numBitPerAccStringIndex = GetRepresentativeBitCount(uniqueAccentStrings.Count);
            int numBitPerAccCountIndex = GetRepresentativeBitCount(uniqueAccentCodeCount.Count);
            int numBitPerHashEntryCount = GetRepresentativeBitCount(maxHashEntryCount);

            bw.Write(Version); // Write version number
            bw.Write(numBitPerAccStringIndex); // How many bits to represent an accent code
            bw.Write(numBitPerAccCountIndex); // How many bits to represent an accent code's count
            bw.Write(numBitPerHashEntryCount); // How many bits to represent the count of elements in each hash entry

            var accStringList = new List<string>();
            var accStringToIndex = new Dictionary<string, int>();
            int index = 0;
            foreach (string accString in uniqueAccentStrings)
            {
                accStringList.Add(accString);
                accStringToIndex.Add(accString, index);
                index++;
            }
            int numBitPerAccStringLength = GetRepresentativeBitCount(accStringList.Max(s => s.Length));
            bw.Write(numBitPerAccStringLength);

            // Write ascii accent codes lookup table
            bw.Write(accStringList.Count);
            foreach (string accString in accStringList)
            {
                string asciiString = String.Join(String.Empty, accString.Select(c => (char)converter.AccentToAsciiMap[c]));
                byte[] asciiBytes = System.Text.Encoding.ASCII.GetBytes(asciiString);

                bw.Write((byte)asciiBytes.Length);
                bw.Write(asciiBytes);
            }

            var accCodeCountList = new List<int>();
            var accCodeCountToIndex = new Dictionary<int, int>();
            index = 0;
            foreach (int accCodeCount in uniqueAccentCodeCount)
            {
                accCodeCountList.Add(accCodeCount);
                accCodeCountToIndex.Add(accCodeCount, index);
                index++;
            }
            // Write unique accent code counts lookup table
            bw.Write(accCodeCountList.Count);
            foreach (int accCodeCount in accCodeCountList)
            {
                bw.Write(accCodeCount);
            }

            // Write map
            bw.Write(m_Map.Count);

            var orderedMap = m_Map.OrderBy(kvp => kvp.Key);

            foreach (var kvp in orderedMap)
            {
                bw.Write(kvp.Key);
            }

            var bitList = new List<bool>();

            foreach (var kvp in orderedMap)
            {
                // Write number of entries, using the minimal number of bits
                for (int ib = numBitPerHashEntryCount - 1; ib >= 0; ib--)
                {
                    bitList.Add(((kvp.Value.Count >> ib) & 1) == 1);
                }

                foreach (string accString in kvp.Value.Keys)
                {
                    // Write accent code's index in the lookup table, using the minimal number of bits
                    for (int ib = numBitPerAccStringIndex - 1; ib >= 0; ib--)
                    {
                        bitList.Add(((accStringToIndex[accString] >> ib) & 1) == 1);
                    }

                    // Write accent code count's index in the lookup table, using the minimal number of bits
                    for (int ib = numBitPerAccCountIndex - 1; ib >= 0; ib--)
                    {
                        bitList.Add(((accCodeCountToIndex[kvp.Value[accString]] >> ib) & 1) == 1);
                    }
                }

                // Write from queue to disk byte-aligned
                while (bitList.Count % 8 != 0)
                {
                    bitList.Add(false);
                }

                BitArray ba = new BitArray(bitList.ToArray());
                byte[] bytes = new byte[bitList.Count / 8];
                ba.CopyTo(bytes, 0);

                bw.Write(bytes);

                bitList.Clear();
            }
        }

        private static int GetRepresentativeBitCount(int count)
        {
            return (int)Math.Ceiling(Math.Log(count, 2));
        }

        // Can only read file of Model1 format for now
        public void ReadFromBinary(BinaryReader br)
        {
            int version = br.ReadInt32();
            if (version != 1)
            {
                throw new Exception();
            }

            var converter = new Utility.VietConverter();

            // Read number of bits for each number type
            int numBitPerAccStringIndex = br.ReadInt32();
            int numBitPerAccCountIndex = br.ReadInt32();
            int numBitPerHashEntryCount = br.ReadInt32();
            int numBitPerAccStringLength = br.ReadInt32();

            // Read look up table of accent codes
            var accStringList = new List<string>();
            int numAccString = br.ReadInt32();
            for (int i = 0; i < numAccString; i++)
            {
                byte stringLength = br.ReadByte();
                byte[] asciiBytes = br.ReadBytes(stringLength);

                string asciiString = System.Text.Encoding.ASCII.GetString(asciiBytes);

                accStringList.Add(String.Join(String.Empty,
                    asciiString.Select(c => converter.AsciiToAccentMap[c])));
            }

            // Read look up table of accent code counts
            var accCodeCountList = new List<int>();
            int numAccCodeCount = br.ReadInt32();
            for (int i = 0; i < numAccCodeCount; i++)
            {
                accCodeCountList.Add(br.ReadInt32());
            }

            // Read bytes for hash codes
            int numHashCodes = br.ReadInt32();
            byte[] hashCodes = br.ReadBytes(sizeof(int) * numHashCodes);

            for (int i = 0; i < numHashCodes; i++)
            {
                int hashCode = BitConverter.ToInt32(hashCodes, i * sizeof(int));
                m_Map.Add(hashCode, new Dictionary<string, int>());

                byte[] countByte = br.ReadBytes(1);
                BitArray baCount = new BitArray(countByte);
                int count = 0;
                for (int ib = 0; ib < numBitPerHashEntryCount; ib++)
                {
                    count += (baCount[ib] ? 1 : 0) << (numBitPerHashEntryCount - ib - 1);
                }

                br.BaseStream.Position = br.BaseStream.Position - 1;
                int numBitPerElement = numBitPerAccStringIndex + numBitPerAccCountIndex;
                int totalNumBits = numBitPerHashEntryCount + count * numBitPerElement;
                int numBytes = (int)Math.Ceiling(totalNumBits / 8.0);

                byte[] bytes = br.ReadBytes(numBytes);
                BitArray ba = new BitArray(bytes);

                int iWork = numBitPerHashEntryCount;
                for (int iElement = 0; iElement < count; iElement++)
                {
                    int accCodeIndex = 0;
                    for (int ib = 0; ib < numBitPerAccStringIndex; ib++)
                    {
                        accCodeIndex += (ba[iWork++] ? 1 : 0) << (numBitPerAccStringIndex - ib - 1);
                    }

                    int accCodeCountIndex = 0;
                    for (int ib = 0; ib < numBitPerAccCountIndex; ib++)
                    {
                        accCodeCountIndex += (ba[iWork++] ? 1 : 0) << (numBitPerAccCountIndex - ib - 1);
                    }

                    string accCode = accStringList[accCodeIndex];
                    int accCodeCount = accCodeCountList[accCodeCountIndex];

                    // TODO: convert accCode to accent string before inserting
                    m_Map[hashCode].Add(accCode, accCodeCount);
                }
            }
        }

        public void ReadFromPriorBinary(BinaryReader br)
        {
            Model0 model = new Model0();
            model.ReadFromBinary(br);
            m_Map = model.Map;
        }

        public void Initialize(string fileName) { }
    }
}
