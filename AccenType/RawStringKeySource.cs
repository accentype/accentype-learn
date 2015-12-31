using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerfectHash.MPH;
using Utility;

namespace VietAccentor
{
    class RawStringKeySource : IKeySource
    {
        List<string> m_KeyList;
        int m_CurrentIndex;

        public RawStringKeySource(List<string> rawToAccentList)
        {
            m_KeyList = rawToAccentList;
        }
        public uint NbKeys
        {
            get { return (uint)m_KeyList.Count; }
        }

        public byte[] Read()
        {
            return Encoding.UTF8.GetBytes(m_KeyList[m_CurrentIndex++]);
        }

        public void Rewind()
        {
            m_CurrentIndex = 0;
        }
    }
}
