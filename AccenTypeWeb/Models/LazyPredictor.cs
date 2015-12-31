using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Utility;
using VietAccentor;

namespace UnaccenType.Models
{
    public sealed class LazyPredictor
    {
        private static volatile LazyPredictor singletonInstance;
        private static object syncRoot = new Object();

        private Dictionary<string, int> dictionary = null;
        private Dictionary<int, TupleList<string, short>> model = null;
        private VietConverter unaccentor = new VietConverter();

        private LazyPredictor()
        {
            dictionary = LazyTrainer.ReadDictionary(
                HttpContext.Current.Server.MapPath("~/App_Data/dictionary.txt"));

            model = LazyTrainer.LoadModel(
                HttpContext.Current.Server.MapPath("~/App_Data/segments.va"), 
                HttpContext.Current.Server.MapPath("~/App_Data/words.va"));
        }

        public string Predict(string query)
        {
            List<string> prediction = new List<string>();
            string[] words = query.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (short iw = 0; iw < words.Length; iw++)
            {
                string w = words[iw];
                if (!dictionary.ContainsKey(w))
                {
                    prediction.Add(w);
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


                        ConcurrentBag<Tuple<short, List<string>, string>> results = new ConcurrentBag<Tuple<short, List<string>, string>>();

                        Parallel.For(0, locations.Count, new ParallelOptions() { MaxDegreeOfParallelism = 8 }, (int i) =>
                        {
                            short iWord = locations[i].Item2;

                            // The accented word list
                            List<string> actual = locations[i].Item1.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).ToList();

                            // The converted raw word list
                            List<string> rawActual = new List<string>();

                            foreach (string aw in actual)
                            {
                                rawActual.Add(unaccentor.Convert(aw));
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
                    }
                }
            }

            return String.Join(" ", prediction);
        }

        public static LazyPredictor SingletonInstance
        {
            get
            {
                if (singletonInstance == null)
                {
                    lock (syncRoot)
                    {
                        if (singletonInstance == null)
                        {
                            singletonInstance = new LazyPredictor();
                        }
                    }
                }

                return singletonInstance;
            }
        }
    }
}