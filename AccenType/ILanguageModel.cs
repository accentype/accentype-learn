using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VietAccentor
{
    public interface ILanguageModel
    {
        void Add(string key, string value);
        void WriteToBinary(BinaryWriter bw);
        void ReadFromBinary(BinaryReader br);
        void ReadFromPriorBinary(BinaryReader br);
        void Initialize(string fileName);
        Dictionary<string, int> GetAccents(string rawSegment);
    }

    public static class ModelFactory
    {
        public static ILanguageModel CreateModelByVersion(int version)
        {
            switch (version)
            {
                case 0:
                    return new Model0();
                case 1:
                    return new Model1();
                case 2:
                    return new Model2();
                case 3:
                    return new Model2French();
            }
            return null;
        }

        public static ILanguageModel LoadModel(string file, int version)
        {
            ILanguageModel model = ModelFactory.CreateModelByVersion(version);
            using (BinaryReader br = new BinaryReader(File.OpenRead(file)))
            {
                model.ReadFromBinary(br);
            }
            return model;
        }
    }
}
