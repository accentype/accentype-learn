using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Utility;
using VietAccentor;

namespace UnaccenType.Models
{
    public sealed class Predictor
    {
        private static volatile Predictor singletonInstance;
        private static object syncRoot = new Object();

        private Predictor()
        {

        }

        public string[][] Predict(Language language, string query)
        {
            switch (language)
            {
                case Language.Vietnamese:
                    return this.PredictFromService(query, "accentypeheader.cloudapp.net");
                case Language.French:
                    return this.PredictFromService(query, "accentypefrench.cloudapp.net");
                default:
                    return null;
            }
        }

        public static Predictor SingletonInstance
        {
            get
            {
                if (singletonInstance == null)
                {
                    lock (syncRoot)
                    {
                        if (singletonInstance == null)
                        {
                            singletonInstance = new Predictor();
                        }
                    }
                }

                return singletonInstance;
            }
        }

        private string[][] PredictFromService(string query, string serviceAddress)
        {
            try
            {
                var client = new UdpClient();

                IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                client.Connect(serviceAddress, 10100);

                List<byte> data = new List<byte>(new byte[] { 0, 1 });
                data.AddRange(System.Text.Encoding.ASCII.GetBytes(query));
                client.Send(data.ToArray(), data.Count);

                client.Client.ReceiveTimeout = 1000;
                byte[] receivedData = client.Receive(ref ep);

                using (var ms = new MemoryStream(receivedData))
                using (var br = new BinaryReader(ms))
                {
                    byte[] header = br.ReadBytes(2); // ignore header here
                    byte numWords = br.ReadByte();

                    string[][] wordChoices = new string[numWords][];
                    for (int i = 0; i < numWords; i++)
                    {
                        byte numChoices = br.ReadByte();
                        wordChoices[i] = new string[numChoices];
                        for (int j = 0; j < numChoices; j++)
                        {
                            byte choiceByteLength = br.ReadByte();
                            byte[] choiceBytes = br.ReadBytes(choiceByteLength);

                            wordChoices[i][j] = Encoding.UTF8.GetString(choiceBytes);
                        }
                    }

                    return wordChoices;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return null;
            }
        }
    }
}