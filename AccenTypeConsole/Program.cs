using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VietAccentor;

namespace AccenTypeConsole
{
    class Program
    {
        static string basePath = @"..\..\..\..\AccenTypeWeb\App_Data\";
        static string trainFile = @"C:\Users\lhoang\Dropbox\Personal\Projects\Git\AccenTypeModels\batches_train.txt";
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            UDPTest.TestServiceCommunication(false); return;

            int modelVersion = 2;
            string outModelFilePattern = basePath + "model_v" + modelVersion + "_{0}.at";
            Utility.AccentConverter converter = new Utility.VietConverter();
            Trainer.Train(outModelFilePattern, trainFile, modelVersion, minGram: 1, maxGram: 3, converter: converter, learnKnownWordsOnly: false);
            EvaluateModels(modelVersion, "model_v" + modelVersion + "*.at");
            
        }

        static void ProfilePredict()
        {
            var models = new List<ILanguageModel>();
            models.Add(ModelFactory.LoadModel(@"D:\Git\AccenType\AccenTypeCloudService\PredictRoleContent\model_v2_1.at", version: 2));
            models.Add(ModelFactory.LoadModel(@"D:\Git\AccenType\AccenTypeCloudService\PredictRoleContent\model_v2_2.at", version: 2));
            models.Add(ModelFactory.LoadModel(@"D:\Git\AccenType\AccenTypeCloudService\PredictRoleContent\model_v2_3.at", version: 2));

            var accentConverter = new Utility.VietConverter();

            var wordChoices = Trainer.Predict("tai sao", models, accentConverter);
            var expected = string.Join("_", wordChoices.SelectMany(a => a));

            Stopwatch sw = new Stopwatch();
            sw.Start();
            //Parallel.For(0, 1000000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            for (int i = 0; i < 1000; i++)
            {
                var pwc = Trainer.Predict("tai sao", models, accentConverter);
                var actual = string.Join("_", pwc.SelectMany(a => a));

                if (expected != actual)
                {
                    throw new Exception();
                }
            }
            sw.Stop();
            Console.WriteLine("Elapsed: {0} ms", sw.ElapsedMilliseconds);
        }

        static void EvaluateModels(int modelVersion, string modelFilePattern)
        {
            var models = new List<ILanguageModel>();
            string[] modelFiles = Directory.GetFiles(basePath, modelFilePattern);
            for (int i = 0; i < modelFiles.Length; i++)
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(modelFiles[i])))
                {
                    ILanguageModel model = ModelFactory.CreateModelByVersion(modelVersion);
                    model.ReadFromBinary(br);
                    models.Add(model);
                }
            }
            var converter = new Utility.VietConverter();
            Trainer.TestTestingSet(models, converter);
            Trainer.TestUserQuery(models, converter);
        }

        private static void CustomBitQueue()
        {
            var rand = new Random((int)DateTime.Now.Ticks);

            var sw = new Stopwatch();
            int nIter = 10000000;
            for (int i = 0; i < nIter + 1; i++)
            {
                int x = rand.Next(100, 100000);
                int nbits = rand.Next(4, 16);

                int ib = 0;

                Queue<bool> bitQueue = new Queue<bool>();
                while (ib < nbits)
                {
                    bitQueue.Enqueue(((x >> ib++) & 1) == 1);
                }

                int y = 0;
                ib = 0;
                while (bitQueue.Count > 0)
                {
                    y += (bitQueue.Dequeue() ? 1 : 0) << ib++;
                }
                sw.Start();
            }

            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        private static void ConvertToModel(int modelVersion, string filePattern)
        {
            string[] modelFiles = Directory.GetFiles(basePath, filePattern);

            for (int i = 0; i < Math.Min(modelFiles.Length, 3); i++)
            {
                ILanguageModel model = null;
                switch (modelVersion)
                {
                    case 1:
                        model = new Model1();
                        break;
                    case 2:
                        model = new Model2();
                        break;
                    case 3:
                        model = new Model2French();
                        break;
                }
                using (BinaryReader br = new BinaryReader(File.OpenRead(modelFiles[i])))
                {
                    model.ReadFromPriorBinary(br);
                }

                int modelLevel = Convert.ToInt32(Path.GetFileNameWithoutExtension(modelFiles[i]).Where(c => Char.IsDigit(c)).First().ToString());
                using (BinaryWriter bw = new BinaryWriter(File.Create(Path.Combine(basePath, "model_v" + modelVersion + "_" + modelLevel + ".at"))))
                {
                    model.WriteToBinary(bw);
                }
            }
        }

        private static void GetUniqueAccentCodes(int modelVersion)
        {
            var converter = new Utility.VietConverter();
            Dictionary<char, char> accentToCodeMap = converter.AccentToCodeMap;

            List<ILanguageModel> models = Trainer.LoadModel(Directory.GetFiles(basePath, "ngram*"), modelVersion);

            for (int i = 0; i < models.Count; i++)
            {
                var uniqueCodes = new SortedList<string, int>();
                Model0 ni = models[i] as Model0;

                IEnumerable<string> codes = ni.Map.Select(he => he.Value
                    .Select(acc => String.Join("", acc.Key.Select(c => accentToCodeMap[c]))))
                    .SelectMany(s => s);

                foreach (string code in codes)
                {
                    if (!uniqueCodes.ContainsKey(code))
                    {
                        uniqueCodes.Add(code, 0);
                    }
                }
                File.WriteAllText("uniquecodes_" + (i + 1),
                    uniqueCodes.Count + "\r\n" + String.Join("\r\n", uniqueCodes.Keys));
            }
        }

        private static void GetModelStatistics(int modelVersion)
        {
            var converter = new Utility.VietConverter();
            Dictionary<char, char> mapLetterToCode = converter.AccentToCodeMap;

            List<ILanguageModel> models = Trainer.LoadModel(Directory.GetFiles(@"..\..\..\..\AccenTypeWeb\App_Data\", "ngram*"), modelVersion);

            var uniqueAccentStrings = new HashSet<string>();
            var uniqueAccentCodeCount = new HashSet<int>();

            for (int i = 0; i < models.Count; i++)
            {
                var perModelUniqueAccentStrings = new HashSet<string>();
                var perModelUniqueAccentCodes = new HashSet<string>();
                var perModelUniqueAccentCodeCount = new HashSet<int>();
                var perModelUniqueAccentRawStrings = new HashSet<string>();

                Model0 ni = models[i] as Model0;
                Dictionary<int, Dictionary<string, int>> map = ni.Map;
                foreach (Dictionary<string, int> mapStringToCount in map.Values)
                {
                    var mapCodeToStrings = new Dictionary<string, List<string>>();
                    foreach (string accString in mapStringToCount.Keys)
                    {
                        uniqueAccentStrings.Add(accString);
                        uniqueAccentCodeCount.Add(mapStringToCount[accString]);

                        perModelUniqueAccentStrings.Add(accString);
                        perModelUniqueAccentRawStrings.Add(String.Join(String.Empty, accString.Select(
                            l => converter.RawCharMap.ContainsKey(l) ? l : converter.AccentToRawCharMap[l]
                        )));

                        string accCode = String.Join(String.Empty, accString.Select(l => mapLetterToCode[l]));
                        perModelUniqueAccentCodes.Add(accCode);
                        perModelUniqueAccentCodeCount.Add(mapStringToCount[accString]);

                        if (!mapCodeToStrings.ContainsKey(accCode))
                        {
                            mapCodeToStrings.Add(accCode, new List<string>());
                        }
                        mapCodeToStrings[accCode].Add(accString);
                    }

                    foreach (string code in mapCodeToStrings.Keys)
                    {
                        if (mapCodeToStrings[code].Count > 1)
                        {
                            Console.WriteLine(String.Join(",", mapCodeToStrings[code]));
                        }
                    }
                }
                Console.WriteLine("MODEL {0}", i + 1);
                Console.WriteLine("# hashed entries: {0}", map.Count);
                Console.WriteLine("Average code length: {0}",
                    (double)map.Sum(he => (double)he.Value.Sum(acc => acc.Key.Length) / he.Value.Count) / map.Count
                );
                Console.WriteLine("Max code length: {0}", map.Max(he => he.Value.Max(acc => acc.Key.Length)));
                Console.WriteLine("Max # of codes per hash entry: {0}", map.Max(he => he.Value.Count));
                Console.WriteLine("# of codes: {0}", map.Sum(he => he.Value.Count));
                Console.WriteLine("# of unique strings: {0}", perModelUniqueAccentStrings.Count);
                Console.WriteLine("# of unique raw strings: {0}", perModelUniqueAccentRawStrings.Count);
                Console.WriteLine("# of unique codes: {0}", perModelUniqueAccentCodes.Count);
                Console.WriteLine("# of unique counts: {0}", perModelUniqueAccentCodeCount.Count);
                Console.WriteLine("Max count: {0}, min count: {1}",
                    perModelUniqueAccentCodeCount.Max(), perModelUniqueAccentCodeCount.Min());
            }

            Console.WriteLine("TOTAL");
            Console.WriteLine("Total # of codes: {0}", uniqueAccentStrings.Count);
            Console.WriteLine("Total # of counts: {0}", uniqueAccentCodeCount.Count);
            Console.WriteLine("Max count: {0}, min count: {1}", uniqueAccentCodeCount.Max(), uniqueAccentCodeCount.Min());

        }

        static void ConvertSegmentToText(string segmentFile)
        {
            string outFile = Path.Combine(Path.GetDirectoryName(segmentFile), Path.GetFileNameWithoutExtension(segmentFile) + ".txt");
            using (StreamWriter sw = new StreamWriter(File.Create(outFile)))
            using (BinaryReader br = new BinaryReader(File.OpenRead(segmentFile)))
            {
                while (br.BaseStream.Position != br.BaseStream.Length)
                {
                    sw.WriteLine(br.ReadString());
                }
            }
        }
    }
}