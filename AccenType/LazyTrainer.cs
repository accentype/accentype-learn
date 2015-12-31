using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace VietAccentor
{
    /// <summary>
    /// Train by mapping words to segments, at prediction time search through segments and find
    /// maximal matching.
    /// </summary>
    public class LazyTrainer
    {
        static string g_DictionaryFile = @"..\..\..\..\VietCrawler\data\vdict-words-raw.txt";
        static string g_SegmentModelFile = @"..\..\..\..\Model\segments.va";
        static string g_WordModelFile = @"..\..\..\..\Model\words.va";
        static string g_LogFile = @"..\..\..\..\Data\logs.txt";

        public static void Run(bool conserveMemory)
        {
            if (!File.Exists(g_SegmentModelFile) || !File.Exists(g_WordModelFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(g_SegmentModelFile));
                LazyTrainer.Train(g_DictionaryFile, g_SegmentModelFile, g_WordModelFile);
            }
            LazyTrainer.Test(g_DictionaryFile, g_SegmentModelFile, g_WordModelFile, conserveMemory);
        }

        private static void Train(string dictionaryFile, string segmentModelFile, string wordModelFile)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            List<List<string>> accentSegmentWordList = TextParser.ParseData(new TupleList<string, string>
            {
                { @"..\..\..\..\Data", "batches_train.txt" },
                { @"..\..\..\..\Data\downloaded", "*.txt" }
            });

            Console.Write("Generating segments... ");

            // List of segment offsets in the binary file
            List<long> offsets = LazyTrainer.WriteSegmentModel(accentSegmentWordList, segmentModelFile);

            Console.WriteLine(sw.ElapsedMilliseconds);
            sw.Restart();

            Console.Write("Loading dictionary of raw words... ");

            // load dictionary of raw words
            Dictionary<string, int> dictionary = LazyTrainer.ReadDictionary(dictionaryFile);

            Console.WriteLine(sw.ElapsedMilliseconds);
            sw.Restart();

            Console.Write("Mapping words to segments... ");

            VietConverter tc = new VietConverter();

            Dictionary<int, TupleList<long, short>> model = new Dictionary<int, TupleList<long, short>>();

            // pass through all segments, recording where each word appears in the segment
            for (int iSegment = 0; iSegment < accentSegmentWordList.Count; iSegment++)
            {
                List<string> wordSegment = accentSegmentWordList[iSegment];
                for (short iWord = 0; iWord < wordSegment.Count; iWord++)
                {
                    string rawWord = tc.Convert(wordSegment[iWord]);

                    // If word is in dictionary
                    if (dictionary.ContainsKey(rawWord))
                    {
                        int wordKey = dictionary[rawWord];
                        if (!model.ContainsKey(wordKey))
                        {
                            model.Add(wordKey, new TupleList<long, short>());
                        }
                        model[wordKey].Add(offsets[iSegment], iWord);
                    }
                }
            }

            LazyTrainer.WriteWordModel(model, wordModelFile);

            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        public static Dictionary<int, TupleList<string, short>> LoadModel(string segmentModelFile, string wordModelFile)
        {
            Dictionary<int, TupleList<long, short>> wordModel = LazyTrainer.ReadWordModel(wordModelFile);

            Dictionary<long, string> offsetToSegmentMap = new Dictionary<long, string>();
            using (BinaryReader brSegment = new BinaryReader(File.OpenRead(segmentModelFile)))
            {
                while (brSegment.BaseStream.Position != brSegment.BaseStream.Length)
                {
                    offsetToSegmentMap.Add(brSegment.BaseStream.Position, brSegment.ReadString());
                }
            }

            Dictionary<int, TupleList<string, short>> model = new Dictionary<int, TupleList<string, short>>();
            foreach (int wordIdx in wordModel.Keys)
            {
                model.Add(wordIdx, new TupleList<string, short>());
                foreach (Tuple<long, short> segmentLocation in wordModel[wordIdx])
                {
                    model[wordIdx].Add(offsetToSegmentMap[segmentLocation.Item1], segmentLocation.Item2);
                }
            }
            return model;
        }

        private static void Test(string dictionaryFile, string segmentModelFile, string wordModelFile, bool conserveMemory)
        {
            Console.OutputEncoding = Encoding.UTF8;

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            Console.Write("Starting up...");

            Dictionary<string, int> dictionary = LazyTrainer.ReadDictionary(dictionaryFile);
            Dictionary<int, TupleList<long, short>> model = LazyTrainer.ReadWordModel(wordModelFile);
            VietConverter tc = new VietConverter();

            using (BinaryReader brSegment = new BinaryReader(File.OpenRead(segmentModelFile)))
            {
                // If not conserving memory then preload everything
                Dictionary<long, string> offsetToSegmentMap = new Dictionary<long, string>();
                if (!conserveMemory)
                {
                    while (brSegment.BaseStream.Position != brSegment.BaseStream.Length)
                    {
                        offsetToSegmentMap.Add(brSegment.BaseStream.Position, brSegment.ReadString());
                    }
                }

                Console.WriteLine(" - {0} seconds", watch.ElapsedMilliseconds / 1000.0);
                Console.WriteLine();

                while (true)
                {
                    Console.Write("Enter a phrase: ");

                    string data = Console.ReadLine();
                    if (data.Contains("quit"))
                    {
                        break;
                    }

                    File.AppendAllText(g_LogFile, data + "\r\n");

                    watch.Restart();

                    List<string> prediction = new List<string>();
                    string[] words = data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for (short iw = 0; iw < words.Length; iw++)
                    {
                        string w = words[iw];
                        if (!dictionary.ContainsKey(w))
                        {
                            prediction.Add(w);

                            File.AppendAllText(g_LogFile, String.Format("{0} is not a known word, leaving as is\r\n", w));
                        }
                        else
                        {
                            int wKey = dictionary[w];
                            if (model.ContainsKey(wKey))
                            {
                                var locations = model[wKey];

                                short maxMatchCount = -1;
                                List<string> mostLikelySequence = new List<string>();
                                string mostLikelyWord = String.Empty;

                                List<string> segments = new List<string>();
                                if (conserveMemory)
                                {
                                    foreach (var loc in locations)
                                    {
                                        brSegment.BaseStream.Position = loc.Item1;
                                        segments.Add(brSegment.ReadString());
                                    }
                                }
                                else
                                {
                                    foreach (var loc in locations)
                                    {
                                        segments.Add(offsetToSegmentMap[loc.Item1]);
                                    }
                                }

                                ConcurrentBag<Tuple<short, List<string>, string>> results = new ConcurrentBag<Tuple<short, List<string>, string>>();

                                Parallel.For(0, locations.Count, new ParallelOptions() { MaxDegreeOfParallelism = 8 }, (int i) =>
                                {
                                    short iWord = locations[i].Item2;

                                    // The accented word list
                                    List<string> actual = segments[i].Split(new char[0], StringSplitOptions.RemoveEmptyEntries).ToList();

                                    // The converted raw word list
                                    List<string> rawActual = new List<string>();

                                    foreach (string aw in actual)
                                    {
                                        rawActual.Add(tc.Convert(aw));
                                    }

                                    short matchCount = 0;
                                    LazyTrainer.MatchSequence(words.ToList(), iw, rawActual, iWord, out matchCount);

                                    results.Add(new Tuple<short, List<string>, string>(matchCount, actual, actual[iWord]));
                                });

                                foreach (var item in results)
                                {
                                    if (item.Item1 >= maxMatchCount)
                                    {
                                        maxMatchCount = item.Item1;
                                        mostLikelySequence = item.Item2;
                                        mostLikelyWord = item.Item3;
                                    }
                                }

                                prediction.Add(mostLikelyWord);

                                File.AppendAllText(g_LogFile,
                                    String.Format("{0} has a {1}-gram match in: {2}\r\n", w, maxMatchCount, String.Join(" ", mostLikelySequence)));
                            }
                        }
                    }
                    string predicted = String.Format("Predicted: {0} - {1} seconds", String.Join(" ", prediction), watch.ElapsedMilliseconds / 1000.0);
                    Console.WriteLine(predicted);
                    Console.WriteLine();

                    File.AppendAllText(g_LogFile, predicted + "\r\n\r\n");
                }
            }
        }

        public static Dictionary<string, int> ReadDictionary(string inFile)
        {
            Dictionary<string, int> dictionary = new Dictionary<string, int>();

            using (StreamReader sr = new StreamReader(File.OpenRead(inFile)))
            {
                int idx = 0;

                while (!sr.EndOfStream)
                {
                    dictionary.Add(sr.ReadLine().Trim().ToLower(), idx);
                    idx++;
                }
            }
            return dictionary;
        }

        private static List<long> WriteSegmentModel(List<List<string>> accentSegmentWordList, string outFile)
        {
            List<long> offsets = new List<long>();

            using (BinaryWriter bwSegments = new BinaryWriter(File.Create(outFile)))
            {
                foreach (List<string> wordList in accentSegmentWordList)
                {
                    string segment = String.Join(" ", wordList);

                    offsets.Add(bwSegments.BaseStream.Position);
                    bwSegments.Write(segment);
                }
            }
            return offsets;
        }

        private static void WriteWordModel(Dictionary<int, TupleList<long, short>> model, string outFile)
        {
            using (BinaryWriter bwWordMap = new BinaryWriter(File.Create(outFile)))
            {
                bwWordMap.Write(model.Count);
                foreach (int wordIdx in model.Keys)
                {
                    TupleList<long, short> segmentInfo = model[wordIdx];

                    bwWordMap.Write(wordIdx);
                    bwWordMap.Write(segmentInfo.Count); // # of segments
                    for (int i = 0; i < segmentInfo.Count; i++)
                    {
                        bwWordMap.Write(segmentInfo[i].Item1); // offset to segment file
                        bwWordMap.Write(segmentInfo[i].Item2); // location of word in segment
                    }
                }
            }
        }

        private static Dictionary<int, TupleList<long, short>> ReadWordModel(string inFile)
        {
            using (BinaryReader brWordMap = new BinaryReader(File.OpenRead(inFile)))
            {
                Dictionary<int, TupleList<long, short>> model = new Dictionary<int, TupleList<long, short>>();

                int numWords = brWordMap.ReadInt32();

                for (int i = 0; i < numWords; i++)
                {
                    int wordIdx = brWordMap.ReadInt32();
                    int numSegments = brWordMap.ReadInt32();

                    TupleList<long, short> segmentInfo = new TupleList<long, short>();
                    for (int j = 0; j < numSegments; j++)
                    {
                        long offset = brWordMap.ReadInt64();
                        short location = brWordMap.ReadInt16();
                        segmentInfo.Add(offset, location);
                    }
                    model.Add(wordIdx, segmentInfo);
                }

                return model;
            }
        }

        public static void MatchSequence(List<string> query, short iQuery, List<string> actual, short iActual,
            out short matchCount)
        {
            matchCount = 1;

            int ia = iActual - 1;
            for (int iq = iQuery - 1; iq >= 0; iq--)
            {
                if (ia >= 0 && String.Compare(query[iq], actual[ia]) == 0)
                {
                    matchCount++;
                    ia--;
                }
                else
                {
                    break;
                }
            }

            ia = iActual + 1;
            for (int iq = iQuery + 1; iq < query.Count; iq++)
            {
                if (ia < actual.Count && String.Compare(query[iq], actual[ia]) == 0)
                {
                    matchCount++;
                    ia++;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
