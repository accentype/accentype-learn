using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace VietAccentor
{
    /// <summary>
    /// Train by building conditional prob model similar to n-gram but considers surrounding context.
    /// </summary>
    public static class Trainer
    {
        static string g_LogFile = Path.Combine(DataManager.LogDir, "logs_ngram.txt");
        static string g_ModelFile = Path.Combine(DataManager.BaseDir, "ngram{0}.va");
        static string g_FrenchModelFile = Path.Combine(DataManager.BaseDir, "ngramfrench{0}.va");

        private static readonly int MaxChoiceCount1 = 10;
        private static readonly int MaxChoiceCount2 = 10;
        private static readonly int MaxChoiceCount3 = 3;

        public static List<ILanguageModel> LoadModel(string[] modelFiles, int version)
        {
            List<ILanguageModel> models = new List<ILanguageModel>();

            for (int i = 0; i < Math.Min(modelFiles.Length, 3); i++)
            {
                models.Add(ModelFactory.LoadModel(modelFiles[i], version));
            }

            return models;
        }

        public static void Train(string outModelFilePattern, string trainingFile, int modelVersion, int minGram, int maxGram, AccentConverter converter, bool learnKnownWordsOnly = true)
        {
            TupleList<string, string> files = new TupleList<string, string> 
            { 
                { Path.GetDirectoryName(trainingFile), Path.GetFileName(trainingFile) }
            };
            Train(outModelFilePattern, files, modelVersion, minGram, maxGram, converter, learnKnownWordsOnly);
        }

        public static void Train(string outModelFilePattern, TupleList<string, string> inputTrainingFiles, int modelVersion, int minGram, int maxGram, AccentConverter converter, bool learnKnownWordsOnly = true)
        {
            List<int> grams = new List<int>();
            for (int n = minGram; n <= maxGram; n++)
            {
                if (!File.Exists(String.Format(outModelFilePattern, n)))
                {
                    grams.Add(n);
                }
            }
            if (grams.Count == 0)
            {
                return;
            }

            // Load dictionary of raw words
            Dictionary<string, int> dictionary = learnKnownWordsOnly ? LazyTrainer.ReadDictionary(DataManager.DictionaryFile) : null;

            // Load segments from training data
            List<List<string>> segments = TextParser.ParseData(inputTrainingFiles);

            StringBuilder sbRaw = new StringBuilder();
            StringBuilder sbAcc = new StringBuilder();

            foreach(int n in grams)
            {
                int iG = n - 1;
                Console.WriteLine("Building {0}-gram ...", iG + 1);

                Clocker.Tick();

                using (BinaryWriter bwModel = new BinaryWriter(File.Create(String.Format(outModelFilePattern, iG + 1))))
                {
                    ILanguageModel igGram = ModelFactory.CreateModelByVersion(modelVersion);
                    for (int iS = 0; iS < segments.Count; iS++)
                    {
                        List<string> words = segments[iS];
                        for (int i = 0; i < words.Count - iG; i++)
                        {
                            sbRaw.Clear();
                            sbAcc.Clear();

                            bool shouldProceed = true;
                            if (learnKnownWordsOnly)
                            {
                                for (int g = 0; g <= iG; g++)
                                {
                                    string accWord = words[i + g];
                                    string rawWord = converter.Convert(accWord);

                                    if (!dictionary.ContainsKey(rawWord))
                                    {
                                        shouldProceed = false;
                                        break;
                                    }

                                    sbAcc.Append(accWord);
                                    sbRaw.Append(rawWord);
                                    if (g < iG)
                                    {
                                        sbRaw.Append(" ");
                                    }
                                }
                            }
                            else
                            {
                                for (int g = 0; g <= iG; g++)
                                {
                                    sbAcc.Append(words[i + g]);
                                    sbRaw.Append(converter.Convert(words[i + g]));
                                    if (g < iG)
                                    {
                                        sbRaw.Append(" ");
                                    }
                                }
                            }
                            
                            if (shouldProceed)
                            {
                                string accents = ExtractAccents(sbAcc.ToString(), converter);

                                igGram.Add(sbRaw.ToString(), accents);
                            }
                        }
                    }

                    igGram.WriteToBinary(bwModel);
                }

                Clocker.Tock();
            }
        }

        public static void Test(AccentConverter converter, int modelVersion)
        {
            string modelFile = (converter is VietConverter) ? g_ModelFile : g_FrenchModelFile;
            string[] modelFiles = Directory.GetFiles(DataManager.BaseDir, Path.GetFileName(String.Format(modelFile, "*")));

            List<ILanguageModel> models = new ILanguageModel[5].ToList();
            
            for (int n = 1; n <= 3; n++)
            {
                string fileName = String.Format(modelFile, n);
                if (!File.Exists(fileName))
                {
                    continue;
                }

                Console.WriteLine("Loading {0}-gram model...", n);

                Clocker.Tick();

                models[n - 1] = ModelFactory.LoadModel(modelFiles[n - 1], modelVersion);

                Clocker.Tock();
            }

            TestUserQuery(models, converter);
        }

        public static void TestUserQuery(List<ILanguageModel> models, AccentConverter converter)
        {
            while (true)
            {
                Console.Write("Enter a phrase: ");

                string data = Console.ReadLine();
                if (data.Contains("quit"))
                {
                    break;
                }

                File.AppendAllText(g_LogFile, data + "\r\n");

                Clocker.Tick();

                string predictedWords = String.Join(" ", Predict(data, models, converter).Select(wc => wc[0]));

                string predicted = String.Format("Predicted: {0} - {1} seconds", predictedWords, Clocker.Seconds());
                Console.WriteLine(predicted);
                Console.WriteLine();

                File.AppendAllText(g_LogFile, predicted + "\r\n\r\n");
            }
        }

        private static void TestTrainingSet(List<ILanguageModel> models, VietConverter converter)
        {
            List<string> segments = new List<string>();
            using (BinaryReader br = new BinaryReader(File.OpenRead(DataManager.SegmentFile(converter))))
            {
                while (br.BaseStream.Position != br.BaseStream.Length)
                {
                    segments.Add(br.ReadString());
                }
            }

            string logFile = Path.Combine(DataManager.LogDir, "logs_ngram_trainset.txt");
            TestDataSet(segments, models, converter, logFile);
        }

        public static void TestTestingSet(List<ILanguageModel> models, VietConverter converter)
        {
            string testFile = String.Empty;
            switch (Environment.MachineName.ToLower())
            {
                case "lhoang-surface":
                case "lhoang-pc7":
                case "lhoang2":
                    testFile = @"C:\Users\lhoang\Dropbox\Personal\Projects\Git\AccenTypeModels\batches_test.txt";
                    break;
                default:
                    testFile = @"..\..\..\..\Data\batches_test.txt";
                    break;
            }

            var parsedData = TextParser.ParseData(new TupleList<string, string>
            {
                { Path.GetDirectoryName(testFile), Path.GetFileName(testFile) }
            });
            List<string> segments = parsedData.Select(wordList => String.Join(" ", wordList).ToLower()).ToList();

            string logFile = Path.Combine(DataManager.LogDir, "logs_testset.txt");
            TestDataSet(segments, models, converter, logFile);
        }

        private static void TestDataSet(List<string> segments, List<ILanguageModel> models, VietConverter converter, string logFile)
        {
            Clocker.Tick();

            Console.WriteLine("{0} expected total segments", segments.Count);

            using (StreamWriter sw = new StreamWriter(File.Create(logFile)))
            {
                int nCorrectSegments = 0;
                int nTotalSegments = 0;

                long nCorrectWords = 0;
                long nTotalWords = 0;

                var logs = new ConcurrentBag<string>();

                Parallel.ForEach(
                    segments,
                    new ParallelOptions { MaxDegreeOfParallelism = 2 * Environment.ProcessorCount },
                    actualData =>
                    {
                        string rawData = converter.Convert(actualData);
                        string predicted = String.Join(" ", Predict(rawData, models, converter).Select(wc => wc[0]));

                        string[] wQuery = actualData.Split(new char[0]);
                        string[] wPredicted = predicted.Split(new char[0]);

                        bool match = true;

                        if (wPredicted.Length != wQuery.Length)
                        {
                            match = false;
                        }
                        else
                        {
                            for (int i = 0; i < wQuery.Length; i++)
                            {
                                if (wQuery[i] != wPredicted[i])
                                {
                                    match = false;
                                    wQuery[i] = wQuery[i].ToUpper();
                                    wPredicted[i] = wPredicted[i].ToUpper();
                                }
                                else
                                {
                                    Interlocked.Increment(ref nCorrectWords);
                                }
                            }
                        }

                        if (!match)
                        {
                            logs.Add(String.Format("{0}\r\n{1}\r\n--------------------",
                                String.Join(" ", wQuery), String.Join(" ", wPredicted)));
                        }
                        else
                        {
                            Interlocked.Increment(ref nCorrectSegments);
                        }
                        Interlocked.Increment(ref nTotalSegments);
                        if (nTotalSegments % 10000 == 0)
                        {
                            Console.WriteLine(nTotalSegments);
                        }

                        Interlocked.Add(ref nTotalWords, wQuery.Length);
                    }
                );

                foreach (string log in logs)
                {
                    sw.WriteLine(log);
                }

                Console.WriteLine("{0} total segments, {1}% segment accuracy", nTotalSegments, ((double)nCorrectSegments / nTotalSegments) * 100.0);
                Console.WriteLine("{0} total words, {1}% word accuracy", nTotalWords, ((double)nCorrectWords / nTotalWords) * 100.0);

                sw.WriteLine("{0} total segments, {1}% segment accuracy", nTotalSegments, ((double)nCorrectSegments / nTotalSegments) * 100.0);
                sw.WriteLine("{0} total words, {1}% word accuracy", nTotalWords, ((double)nCorrectWords / nTotalWords) * 100.0);
            }

            Clocker.Tock();
        }

        public static string[][] Predict(string data, List<ILanguageModel> models, AccentConverter converter)
        {
            string[][] wordChoices = null;

            if (String.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            string[] queryWords = data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] words = queryWords.Select(w => w.ToLower()).ToArray();

            List<List<int>> upperCases = new List<List<int>>();
            for (int w = 0; w < queryWords.Length; w++)
            {
                upperCases.Add(new List<int>());
                for (int c = 0; c < queryWords[w].Length; c++)
                {
                    if (Char.IsUpper(queryWords[w][c]))
                    {
                        upperCases[w].Add(c);
                    }
                }
            }

            double beta3 = 0.2;
            double beta2 = 0.15;
            double beta1 = 0.1;

            wordChoices = new string[words.Length][];

            int maxChoices = 0;
            if (wordChoices.Length == 1)
            {
                maxChoices = MaxChoiceCount1;
            }
            else if (wordChoices.Length == 2)
            {
                maxChoices = MaxChoiceCount2;
            }
            else
            {
                maxChoices = MaxChoiceCount3;
            }

            var predictedWords = new string[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                var accScoreMap = new Dictionary<string, double>();

                ComputeAccentScore(words, i, converter, beta3, models[2], 3, accScoreMap);
                ComputeAccentScore(words, i, converter, beta2, models[1], 2, accScoreMap);
                ComputeAccentScore(words, i, converter, beta1, models[0], 1, accScoreMap);

                if (accScoreMap != null && accScoreMap.Count > 0)
                {
                    wordChoices[i] = new string[Math.Min(maxChoices, accScoreMap.Count)];

                    var orderedChoices = accScoreMap.OrderByDescending(item => item.Value);
                        
                    int j = 0;
                    foreach (var item in orderedChoices)
                    {
                        if (upperCases[i].Count > 0)
                        {
                            char[] choiceChars = item.Key.ToArray();
                            foreach (int upperCaseLocation in upperCases[i])
                            {
                                choiceChars[upperCaseLocation] = Char.ToUpper(choiceChars[upperCaseLocation]);
                            }
                            wordChoices[i][j] = new string(choiceChars);
                        }
                        else
                        {
                            wordChoices[i][j] = item.Key;
                        }
                        j++;

                        if (j >= wordChoices[i].Length)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    wordChoices[i] = new string[] { queryWords[i] };
                }
            }
            return wordChoices;
        }

        private static void ComputeAccentScore(string[] words, int iW, 
            AccentConverter converter, double weight, 
            ILanguageModel model, int n, Dictionary<string, double> accScoreMap)
        {
            int g = n - 1;

            // compute accent probability for this word
            int g3Start = Math.Max(iW - g, 0);
            int g3End = Math.Min(iW + g, words.Length - 1);
            for (int jW = g3Start; jW <= g3End - g; jW++)
            {
                string segment = 
                    (g == 2) ? String.Format("{0} {1} {2}", words[jW], words[jW + 1], words[jW + 2]) :
                    (g == 1) ? String.Format("{0} {1}", words[jW], words[jW + 1]) : words[jW];


                Dictionary<string, int> accentsCountMap = model.GetAccents(segment);

                if (accentsCountMap == null)
                {
                    continue;
                }

                double count = accentsCountMap.Sum(item => item.Value);

                foreach (string accents in accentsCountMap.Keys)
                {
                    string accSegment = Accentify(segment, accents, converter);
                    if (accSegment != null)
                    {
                        string[] accWords = accSegment.Split(new char[0]);

                        string accentedWord = accWords[iW - jW];
                        double accScore = (accentsCountMap[accents] / count) * weight;

                        if (!accScoreMap.ContainsKey(accentedWord))
                        {
                            accScoreMap.Add(accentedWord, 0);
                        }
                        accScoreMap[accentedWord] += accScore;
                    }
                }
            }
        }

        public static string Accentify(string rawSentence, string accents, AccentConverter converter)
        {
            if (rawSentence == null || accents == null || converter == null)
            {
                return null;
            }

            char[] rawChars = rawSentence.ToArray();
            char[] accChars = converter.Convert(accents).ToArray();

            int ia = 0;
            for (int i = 0; i < rawChars.Length; i++)
            {
                if (converter.RawCharMap.ContainsKey(rawChars[i]))
                {
                    if (ia >= accChars.Length || rawChars[i] != accChars[ia])
                    {
                        return null;
                    }
                    rawChars[i] = accents[ia++];
                }
            }
            return new string(rawChars);
        }

        public static string ExtractAccents(string sentence, AccentConverter converter)
        {
            StringBuilder sb = new StringBuilder();

            string rawSentence = converter.Convert(sentence);
            for (int i = 0; i < rawSentence.Length; i++)
            {
                if (converter.RawCharMap.ContainsKey(rawSentence[i]))
                {
                    sb.Append(sentence[i]);
                }
            }
            return sb.ToString();
        }
    }
}
