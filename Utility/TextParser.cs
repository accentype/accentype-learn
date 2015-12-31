using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utility
{
    public class TextParser
    {
        /// <summary>
        /// Parses text files and generate a list of list of words.
        /// </summary>
        /// <returns>A list of segment which is in turn a list of words.</returns>
        public static List<List<string>> ParseData(TupleList<string, string> dataFiles, bool writeOut = true)
        {
            // The parsing algorithm is as follows:
            // 1. Read 1 line at a time and ignore all whitespace or skip if it's a white space only line
            // 2. If this line contains more than a certain number of html characters such as &# then skip it
            // 3. If present, extract groups inside parentheses out as separate sentences since they often are self-sufficient
            // 4. Break the line into different segments through characters . , ; : ! ?
            //      - An issue with the , . characters is that numbers use them as well, e.g. 7,5 or 40.000.
            //      - Another issue with the dot character is that it can be used as ellipsis during part of a sentence. For example: Là ngày... em sẽ xa ta luôn.
            //      - Characters such as ! and ? don't really affect word meanings and often signal end of phrase
            // 5. Remove empty or white-space segments
            // 6. Break each segment into a list of words with white-space separators.
            //      - Note that white-space separators here include more than just the space character (ascii code 32)
            //        but can also include \t or html white-space character such as &nbsp; etc...
            // 7. Remove words that are only characters such as * >
            // 8. Remove quote characters ' " ” (8221) “ (8220) from words since they normally only serve as emphasis functions

            string[] ignores = { "&#" };
            char[] quotes = { '\'', '"', '“', '”' };
            char[] segmentSeparators = { ',', ';', ':', '.', '!', '?' };
            HashSet<string> removeSet = new HashSet<string>(new string[] { "*", ">" });

            VietConverter converter = new VietConverter();
            List<List<string>> globalSegmentList = new List<List<string>>();

            foreach (var tuple in dataFiles)
            {
                var textFiles = Directory.EnumerateFiles(tuple.Item1, tuple.Item2, SearchOption.AllDirectories);
                foreach (var textFile in textFiles)
                {
                    Console.WriteLine(textFile);
                    using (StreamReader sr = new StreamReader(File.OpenRead(textFile)))
                    {
                        while (sr.EndOfStream == false)
                        {
                            string line = sr.ReadLine().Trim();
                            // Ignore white-space strings
                            if (!String.IsNullOrWhiteSpace(line))
                            {
                                bool ignore = false;
                                // Ignore strings that contain invalid characters such as html characters
                                foreach (string ignorePattern in ignores)
                                {
                                    if (line.Contains(ignorePattern))
                                    {
                                        ignore = true;
                                        break;
                                    }
                                }
                                if (ignore)
                                {
                                    continue;
                                }

                                // Extract parentheses groups from current string
                                var groups = TextParser.ExtractParentheses(line);

                                foreach (string group in groups)
                                {
                                    if (!String.IsNullOrWhiteSpace(group)) // Make sure once again that the groups aren't white-space only
                                    {
                                        // Break each group into segments
                                        string[] segmentArray = group.Split(segmentSeparators, StringSplitOptions.RemoveEmptyEntries);
                                        foreach (string segment in segmentArray)
                                        {
                                            List<int> wordList = new List<int>();

                                            bool skipSegment = false;

                                            // Break each segment into words
                                            List<string> normSegment = new List<string>();

                                            string[] wordArray = segment.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                                            if (wordArray.Length > 1)
                                            {
                                                foreach (string word in wordArray)
                                                {
                                                    string normWord = word;

                                                    // Make sure the word is not white-space only
                                                    if (!removeSet.Contains(normWord))
                                                    {
                                                        // Remove quote characters
                                                        foreach (var quote in quotes)
                                                        {
                                                            normWord = normWord.Replace(quote.ToString(), "");
                                                        }
                                                        normWord = normWord.Trim().ToLower();

                                                        if (!String.IsNullOrWhiteSpace(normWord))
                                                        {
                                                            normSegment.Add(normWord);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        skipSegment = true;
                                                        break;
                                                    }
                                                }
                                            }

                                            if (!skipSegment && normSegment.Count > 1)
                                            {
                                                globalSegmentList.Add(normSegment);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return globalSegmentList;
        }

        private static IEnumerable<string> ExtractParentheses(string s)
        {
            int count = 0;

            List<StringBuilder> results = new List<StringBuilder>();
            results.Add(new StringBuilder());

            foreach (char c in s)
            {
                switch (c)
                {
                    case '(':
                        count++;
                        results.Add(new StringBuilder());
                        break;
                    case ')':
                        if (count > 0)
                        {
                            yield return results[count].ToString();
                            results[count] = new StringBuilder();
                            count--;
                        }
                        break;
                    default:
                        results[count].Append(c);
                        break;
                }
            }

            for (int i = 1; i <= count; i++)
            {
                results[0].Append(results[i].ToString());
            }

            yield return results[0].ToString();
        }

        private static void TestExtractParentheses()
        {
            string[] lines = 
            { 
                ") ) () ) (my code) is doing (fine hehehe (just kidding)), however (i am (not sure (whether) it works properly))",
                ") test my code (hehehe)",
                ") ahsdasdka (",
                "",
                "test (adkas (jkaj99900) akfskfk) my code",
                "test (adkasjkaj akfskfk) my code (hahaha one more)",
                "()",
                "(",
                ")",
                "(what is (my code) doing)",
                "( hahaha ( whatever",
                ")heheh ( hahah",
                "my code is doing this (all the time), but also doing that (less frequently), anyway I'm off (at least until 6) ok."
            };

            foreach (string l in lines)
            {
                var result = ExtractParentheses(l);
                foreach (var s in result)
                {
                    Console.Write(s + " --- ");
                }
                Console.WriteLine();
            }
        }
    }
}
