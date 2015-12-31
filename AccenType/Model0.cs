using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utility;

namespace VietAccentor
{
    public class Model0 : ILanguageModel
    {
        public Dictionary<int, Dictionary<string, int>> Map { get { return m_Map; } }
        private Dictionary<int, Dictionary<string, int>> m_Map;
        
        public Model0()
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
            bw.Write(m_Map.Count);

            foreach (int hashCode in m_Map.Keys)
            {
                bw.Write(hashCode);
            }

            foreach (int hashCode in m_Map.Keys)
            {
                bw.Write(m_Map[hashCode].Count);
                foreach (string accents in m_Map[hashCode].Keys)
                {
                    bw.Write(accents);
                    bw.Write(m_Map[hashCode][accents]);
                }
            }
        }

        public void ReadFromBinary(BinaryReader br)
        {
            int numBins = br.ReadInt32();
            byte[] hashCodes = br.ReadBytes(sizeof(int) * numBins);

            for (int i = 0; i < numBins; i++)
            {
                int hashCode = BitConverter.ToInt32(hashCodes, i * sizeof(int));
                m_Map.Add(hashCode, new Dictionary<string, int>());

                int numAccents = br.ReadInt32();
                for (int j = 0; j < numAccents; j++)
                {
                    string accents = br.ReadString();
                    int count = br.ReadInt32();
                    m_Map[hashCode].Add(accents, count);
                }
            }
        }

        public void Initialize(string fileName) { }

        public void ReadFromPriorBinary(BinaryReader br) { }
    }
}
