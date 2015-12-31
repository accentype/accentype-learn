using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Utility
{
    public class AccentConverter
    {
        private Dictionary<char, int> rawAlphabetMap;
        private Dictionary<char, int> accentToAsciiMap;
        private Dictionary<int, char> asciiToAccentMap;
        private Dictionary<char, int> rawCharMap;

        private Dictionary<char, char> accentToRawCharMap;

        private Dictionary<char, char> accentToCodeMap;

        /// <summary>
        /// Map containing all raw letters in the alphabets.
        /// </summary>
        public Dictionary<char, int> RawAlphabetMap
        {
            get { return rawAlphabetMap; }
        }

        /// <summary>
        /// Map containing only letters with accents.
        /// </summary>
        public Dictionary<char, int> AccentToAsciiMap
        {
            get { return accentToAsciiMap; }
        }

        /// <summary>
        /// Mapping from ascii code to accent.
        /// </summary>
        public Dictionary<int, char> AsciiToAccentMap
        {
            get { return asciiToAccentMap; }
        }

        /// <summary>
        /// Map containing all raw letters that could have accents.
        /// </summary>
        public Dictionary<char, int> RawCharMap
        {
            get { return rawCharMap; }
            set { rawCharMap = value; }
        }

        /// <summary>
        /// Map from accented letters to raw letters.
        /// </summary>
        public Dictionary<char, char> AccentToRawCharMap
        {
            get { return accentToRawCharMap; }
        }

        /// <summary>
        /// Map from accents to accent codes.
        /// </summary>
        public Dictionary<char, char> AccentToCodeMap
        {
            get { return accentToCodeMap; }
        }

        public AccentConverter()
        {
            string rawChars = this.GetRawChars();

            var lettersTup = this.GetAccentToRawMapping();
            int count = lettersTup.Count;
            for (int i = 0; i < count; i++)
            {
                lettersTup.Add(lettersTup[i].Item1.ToUpper(), lettersTup[i].Item2.ToString().ToUpper()[0]);
            }
            
            accentToRawCharMap = lettersTup.SelectMany(tup => tup.Item1
                .Select(letter => new Tuple<char, char>(letter, tup.Item2)))
                .ToDictionary(x => x.Item1, x => x.Item2);


            TupleList<string, char> accCodeTup = this.GetAccentToCodeMapping();
#if DEBUG
            // Verify that accent codes do not contain duplicate
            var accentCodeSet = new Dictionary<char, byte>();
            accCodeTup.ForEach(tup => accentCodeSet.Add(tup.Item2, 0));
#endif
            count = accCodeTup.Count;
            for (int i = 0; i < count; i++)
            {
                accCodeTup.Add(accCodeTup[i].Item1.ToUpper(), accCodeTup[i].Item2);
            }
            accentToCodeMap = accCodeTup.SelectMany(tup => tup.Item1
                .Select(letter => new Tuple<char, char>(letter, tup.Item2)))
                .ToDictionary(x => x.Item1, x => x.Item2);
            
#if DEBUG
            // Verify that number of accented letters only differ by the number of raw letters aeuioydAEUIOYD
            Debug.Assert(accentToCodeMap.Count - 14 == accentToRawCharMap.Count);
#endif

            accentToAsciiMap = new Dictionary<char, int>();
            asciiToAccentMap = new Dictionary<int, char>();

            List<char> accentCharList = accentToRawCharMap.Keys.Select(a => Char.ToLower(a)).Distinct().ToList();
            accentCharList.AddRange(accentToRawCharMap.Values.Select(a => Char.ToLower(a)).Distinct());
            accentCharList.Sort();
            for (int i = 0; i < accentCharList.Count; i++)
            {
                asciiToAccentMap.Add(i + 33, accentCharList[i]);
                accentToAsciiMap.Add(accentCharList[i], i + 33);
            }

            rawCharMap = new Dictionary<char, int>();
            for (int iTup = 0; iTup < lettersTup.Count; iTup++)
            {
                rawCharMap.Add(lettersTup[iTup].Item2, iTup);
            }

            rawAlphabetMap = new Dictionary<char, int>();
            for (int iChar = 0; iChar < rawChars.Length; iChar++)
            {
                rawAlphabetMap.Add(rawChars[iChar], iChar);
            }
        }

        protected virtual string GetRawChars()
        {
            throw new NotImplementedException();
        }

        protected virtual TupleList<string, char> GetAccentToRawMapping()
        {
            throw new NotImplementedException();
        }

        protected virtual TupleList<string, char> GetAccentToCodeMapping()
        {
            throw new NotImplementedException();
        }

        public string Convert(string content)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var character in content)
            {
                if (accentToRawCharMap.ContainsKey(character))
                {
                    sb.Append(accentToRawCharMap[character]);
                }
                else
                {
                    sb.Append(character);
                }
            }
            return sb.ToString();
        }
    }

    public class VietConverter : AccentConverter
    {
        protected override string GetRawChars()
        {
            return "abcdeghiklmnopqrstuvxy";
        }

        protected override TupleList<string, char> GetAccentToRawMapping()
        {
            return new TupleList<string, char> 
            { 
                {"áàạảãăắằặẳẵâấầậẩẫ",'a'},
                {"éèẹẻẽêếềệểễ",'e'},
                {"íìịỉĩ",'i'},
                {"óòọỏõôốồộổỗơớờợởỡ",'o'},
                {"úùụủũưứừựửữ",'u'},
                {"ýỳỵỷỹ", 'y'},
                {"đ",'d'}
            };
        }
        protected override TupleList<string, char> GetAccentToCodeMapping()
        {
            return new TupleList<string, char> 
            { 
                {"aeiouyd",'|'},
                {"áéíóúý",'\''},
                {"àèìòùỳ",'`'},
                {"ạẹịọụỵ",'.'},
                {"ảẻỉỏủỷ",'?'},
                {"ãẽĩõũỹ",'~'},
                
                {"ă",'0'},
                {"ắ",'1'},
                {"ằ",'2'},
                {"ặ",'3'},
                {"ẳ",'4'},
                {"ẵ",'5'},
                
                {"âêô",'6'},
                {"ấếố",'7'},
                {"ầềồ",'8'},
                {"ậệộ",'9'},
                {"ẩểổ",'!'},
                {"ẫễỗ",'@'},

                {"ơư",'#'},
                {"ớứ",'$'},
                {"ờừ",'%'},
                {"ợự",'^'},
                {"ởử",'&'},
                {"ỡữ",'*'},

                {"đ",'-'},
            };
        }
    }

    public class FrenchConverter : AccentConverter
    {
        protected override string GetRawChars()
        {
            return "abcdefghijklmnopqrstuvxy";
        }

        protected override TupleList<string, char> GetAccentToRawMapping()
        {
            return new TupleList<string, char> 
            { 
                {"àâæä",'a'},
                {"ç", 'c'},
                {"éèêë",'e'},
                {"îï",'i'},
                {"ôœö",'o'},
                {"ùûü",'u'},
                {"ÿ", 'y'}
            };
        }

        protected override TupleList<string, char> GetAccentToCodeMapping()
        {
            return new TupleList<string, char> 
            { 
                {"aceiouy",'|'},
                {"é",'\''},
                {"àèù",'`'},
                {"ç",'.'},
                {"âêîôû",'?'},
                {"äëïöüÿ",'~'},
                {"æœ",'0'},
            };
        }
    }
}
