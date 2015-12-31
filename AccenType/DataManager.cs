using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace VietAccentor
{
    public class DataManager
    {
        public static string BaseDir = @"..\..\..\..\UnaccenType\App_Data\";
        public static string LogDir = @".\";
        public static string DictionaryFile = Path.Combine(BaseDir, "dictionary.txt");
        public static string SegmentFile(Utility.AccentConverter converter)
        {
            if (converter is Utility.VietConverter)
            {
                return Path.Combine(BaseDir, "segments.va");
            }
            else
            {
                return Path.Combine(BaseDir, "frenchsegments.va");
            }
        }

        /// <summary>
        /// Load binary files and generate train- and test- data according to the specified portion.
        /// </summary>
        /// <param name="trainPortion">The percentage of total dataset to generate training data.</param>
        private static void GenerateTrainTestData(double trainPortion)
        {
            string crawledFilePath = @"D:\Git\VietAccentor\VietCrawler\CrawledData\";
            string crawledFilePattern = "batch_{0}.textbin";
            var files = Directory.GetFiles(crawledFilePath, String.Format(crawledFilePattern, "*"));
            int numTrain = (int)(files.Length * trainPortion);

            StringBuilder sbTrain = new StringBuilder();
            StringBuilder sbTest = new StringBuilder();
            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine("Read file " + i);
                StringBuilder sbWork = (i <= numTrain) ? sbTrain : sbTest;

                string currentFile = Path.Combine(crawledFilePath, String.Format(crawledFilePattern, i));
                using (BinaryReader br = new BinaryReader(File.OpenRead(currentFile)))
                {
                    int batchSize = br.ReadInt32();
                    for (int j = 0; j < batchSize; j++)
                    {
                        int clLength = br.ReadInt32();
                        var clValue = br.ReadBytes(clLength);
                        string textContent = Encoding.UTF8.GetString(clValue);

                        sbWork.Append(textContent + "\r\n");
                    }
                }
            }
            File.WriteAllText("batches_train.txt", sbTrain.ToString(), Encoding.UTF8);
            File.WriteAllText("batches_test.txt", sbTest.ToString(), Encoding.UTF8);
        }

        public static void ParseFrenchXMLData()
        {
            string baseDir = @"C:\Users\lhoang\Documents\Git\VietAccentor\FrenchData";
            string[] frenchFiles = Directory.GetFiles(baseDir, "*.xml");
            using (StreamWriter sw = new StreamWriter(File.Create(Path.Combine(baseDir, "frenchdata.txt"))))
            {
                foreach (string file in frenchFiles)
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);
                    var pNodes = xmlDoc.SelectNodes("//P");
                    foreach (XmlNode pn in pNodes)
                    {
                        sw.WriteLine(pn.InnerText);
                    }
                }
            }
        }

        public static void CreateFrenchSegments()
        {
            List<List<string>> segments = Utility.TextParser.ParseData(new Utility.TupleList<string, string>() { 
                { @"C:\Users\lhoang\Documents\Git\VietAccentor\FrenchData", "*.txt" }
            });

            using (BinaryWriter bw = new BinaryWriter(File.Create(@"..\..\..\..\Model\frenchsegments.va")))
            {
                foreach (var segment in segments)
                {
                    bw.Write(String.Join(" ", segment));
                }
            }
        }
    }
}
