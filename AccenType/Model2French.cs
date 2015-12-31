using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utility;
using PerfectHash.MPH;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;

namespace VietAccentor
{
    public class Model2French : ILanguageModel
    {
        public int Version { get { return 1; } }

        private MinPerfectHash m_HashFunction;

        // Used for training
        private Dictionary<string, Dictionary<string, int>> m_Map;
        private List<Dictionary<string, int>> m_HashTable;

        // Used for scoring
        private byte[][] m_HashTableBytes;
        private byte[][] m_PredictionAsciiBytes;
        private int[] m_PredictionCounts;

        private int m_NumBitPerAccStringIndex;
        private int m_NumBitPerAccCountIndex;
        private int m_NumBitPerHashEntryCount;
        private int m_NumBitPerAccStringLength;

        private AccentConverter m_Converter = new FrenchConverter();

        public void Add(string key, string value)
        {
            if (m_Map == null)
            {
                m_Map = new Dictionary<string, Dictionary<string, int>>();
            }
            if (!m_Map.ContainsKey(key))
            {
                m_Map.Add(key, new Dictionary<string, int>());
            }
            if (!m_Map[key].ContainsKey(value))
            {
                m_Map[key].Add(value, 1);
            }
            else
            {
                m_Map[key][value]++;
            }
        }

        public Dictionary<string, int> GetAccents(string rawSegment)
        {
            Dictionary<string, int> predictionToCount = null;

            int hashCode = (int)m_HashFunction.Search(Encoding.UTF8.GetBytes(rawSegment.GetHashCode().ToString()));
            if (hashCode >= 0 && hashCode < m_HashFunction.N)
            {
                byte[] asciiBytes = m_HashTableBytes[hashCode];

                if (asciiBytes != null)
                {
                    byte[] countByte = new byte[] { asciiBytes[0] };
                    BitArray baCount = new BitArray(countByte);
                    int count = 0;
                    for (int ib = 0; ib < m_NumBitPerHashEntryCount; ib++)
                    {
                        count += (baCount[ib] ? 1 : 0) << (m_NumBitPerHashEntryCount - ib - 1);
                    }

                    BitArray ba = new BitArray(asciiBytes);

                    predictionToCount = new Dictionary<string, int>();
                    int iWork = m_NumBitPerHashEntryCount;
                    for (int iElement = 0; iElement < count; iElement++)
                    {
                        int accCodeIndex = 0;
                        for (int ib = 0; ib < m_NumBitPerAccStringIndex; ib++)
                        {
                            accCodeIndex += (ba[iWork++] ? 1 : 0) << (m_NumBitPerAccStringIndex - ib - 1);
                        }

                        int accCodeCountIndex = 0;
                        for (int ib = 0; ib < m_NumBitPerAccCountIndex; ib++)
                        {
                            accCodeCountIndex += (ba[iWork++] ? 1 : 0) << (m_NumBitPerAccCountIndex - ib - 1);
                        }

                        string accString = Encoding.ASCII.GetString(m_PredictionAsciiBytes[accCodeIndex]);
                        accString = String.Join(String.Empty, accString.Select(c => m_Converter.AsciiToAccentMap[c]));

                        int accStringCount = m_PredictionCounts[accCodeCountIndex];

                        predictionToCount.Add(accString, accStringCount);
                    }

                    return predictionToCount;
                }
            }
            return predictionToCount;
        }

        public void WriteToBinary(BinaryWriter bw)
        {
            m_HashFunction = PerfectHash.MPH.MinPerfectHash.Create(new RawStringKeySource(m_Map.Keys.ToList()), c: 1.0);
            m_HashTable = Enumerable.Repeat<Dictionary<string, int>>(null, (int)m_HashFunction.N).ToList();

            foreach (string rawString in m_Map.Keys)
            {
                int hashCode = (int)m_HashFunction.Search(Encoding.UTF8.GetBytes(rawString));

                m_HashTable[hashCode] = m_Map[rawString];
            }

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
                string asciiString = String.Join(String.Empty, accString.Select(c => (char)m_Converter.AccentToAsciiMap[c]));
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

            // Serialize hash function
            var hashFunctionStream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(hashFunctionStream, m_HashFunction);
            byte[] hashFunctionBytes = hashFunctionStream.ToArray();
            bw.Write(hashFunctionBytes.Length);
            bw.Write(hashFunctionBytes);

            var bitList = new List<bool>();

            foreach (var kvp in m_HashTable)
            {
                // Write number of entries, using the minimal number of bits
                int count = kvp != null ? kvp.Count : 0;

                for (int ib = numBitPerHashEntryCount - 1; ib >= 0; ib--)
                {
                    bitList.Add(((count >> ib) & 1) == 1);
                }

                if (kvp != null)
                {
                    foreach (string accString in kvp.Keys)
                    {
                        // Write accent code's index in the lookup table, using the minimal number of bits
                        for (int ib = numBitPerAccStringIndex - 1; ib >= 0; ib--)
                        {
                            bitList.Add(((accStringToIndex[accString] >> ib) & 1) == 1);
                        }

                        // Write accent code count's index in the lookup table, using the minimal number of bits
                        for (int ib = numBitPerAccCountIndex - 1; ib >= 0; ib--)
                        {
                            bitList.Add(((accCodeCountToIndex[kvp[accString]] >> ib) & 1) == 1);
                        }
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
            int numBit = 0;
            while (true)
            {
                if (count < Math.Pow(2, numBit))
                {
                    break;
                }
                numBit++;
            }
            return numBit;
        }

        // Can only read file of Model1 format for now
        public void ReadFromBinary(BinaryReader br)
        {
            int version = br.ReadInt32();
            if (version != 1)
            {
                throw new Exception();
            }

            // Read number of bits for each number type
            m_NumBitPerAccStringIndex = br.ReadInt32();
            m_NumBitPerAccCountIndex = br.ReadInt32();
            m_NumBitPerHashEntryCount = br.ReadInt32();
            m_NumBitPerAccStringLength = br.ReadInt32();

            // Read look up table of accent codes
            int numAccString = br.ReadInt32();
            m_PredictionAsciiBytes = new byte[numAccString][];
            for (int i = 0; i < numAccString; i++)
            {
                byte stringLength = br.ReadByte();
                byte[] asciiBytes = br.ReadBytes(stringLength);
                m_PredictionAsciiBytes[i] = asciiBytes;
            }

            // Read look up table of accent code counts

            int numAccCodeCount = br.ReadInt32();
            m_PredictionCounts = new int[numAccCodeCount];
            for (int i = 0; i < numAccCodeCount; i++)
            {
                m_PredictionCounts[i] = br.ReadInt32();
            }

            // Deserialize hash function
            int hashFunctionBytesLength = br.ReadInt32();
            byte[] hashFunctionBytes = br.ReadBytes(hashFunctionBytesLength);
            var hashFunctionStream = new MemoryStream(hashFunctionBytes);
            var formatter = new BinaryFormatter();
            m_HashFunction = (MinPerfectHash)formatter.Deserialize(hashFunctionStream);

            // Read bytes for hash codes
            int hashSize = (int)m_HashFunction.N;
            m_HashTableBytes = new byte[m_HashFunction.N][];

            for (int i = 0; i < hashSize; i++)
            {
                byte[] countByte = br.ReadBytes(1);
                BitArray baCount = new BitArray(countByte);
                int count = 0;
                for (int ib = 0; ib < m_NumBitPerHashEntryCount; ib++)
                {
                    count += (baCount[ib] ? 1 : 0) << (m_NumBitPerHashEntryCount - ib - 1);
                }

                if (count == 0)
                {
                    continue;
                }

                br.BaseStream.Position = br.BaseStream.Position - 1;
                int numBitPerElement = m_NumBitPerAccStringIndex + m_NumBitPerAccCountIndex;
                int totalNumBits = m_NumBitPerHashEntryCount + count * numBitPerElement;
                int numBytes = (int)Math.Ceiling(totalNumBits / 8.0);

                m_HashTableBytes[i] = br.ReadBytes(numBytes);
            }
        }

        public void ReadFromPriorBinary(BinaryReader br)
        {
            Model0 model = new Model0();
            model.ReadFromBinary(br);

            m_Map = new Dictionary<string, Dictionary<string, int>>();
            foreach (int hashCode in model.Map.Keys)
            {
                m_Map.Add(hashCode.ToString(), model.Map[hashCode]);
            }
        }

        public void Initialize(string fileName) { }
    }
}
