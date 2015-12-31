using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class Clocker
    {
        private static Stopwatch s_Watch = new Stopwatch();

        public static void Tick()
        {
            s_Watch.Restart();
        }

        public static void Tock()
        {
            Console.WriteLine("{0} secs", s_Watch.ElapsedMilliseconds / 1000.0);
        }

        public static long Milisecs()
        {
            return s_Watch.ElapsedMilliseconds;
        }

        public static double Seconds()
        {
            return s_Watch.ElapsedMilliseconds / 1000.0;
        }

        public static void Clock(int nCold, int nWarm, string methodDescription, Action method)
        {
            if (!String.IsNullOrWhiteSpace(methodDescription))
            {
                Console.WriteLine(methodDescription);
            }
            Console.WriteLine("Cold runs: " + nCold);
            Console.WriteLine("Warm runs: " + nWarm);

            for (int i = 0; i < nCold; i++)
            {
                Console.Write("Cold run: " + (i + 1) + "\r");
                method();
            }

            Console.Write("Warm runs ...\r");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < nWarm; i++)
            {
                method();
            }
            double elapsedTotal = sw.ElapsedMilliseconds;
            double elapsedAverage = elapsedTotal / nWarm;
            sw.Stop();

            Console.WriteLine("Total time: " + elapsedTotal + " ms, " + elapsedTotal / 1000 + " secs.");
            Console.WriteLine("Average time: " + elapsedAverage + " ms, " + elapsedAverage / 1000 + " secs.");
        }
    }
}
