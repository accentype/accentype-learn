using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace AccenTypeConsole
{
    class UDPTest
    {
        public static void TestServiceCommunication(bool useLocalServer)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var client = new UdpClient();

            IPEndPoint ep = null;
            if (useLocalServer)
            {
                ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 20001); // endpoint where server is listening
                client.Connect(ep);
            }
            else
            {
                ep = new IPEndPoint(IPAddress.Any, 0);
                client.Connect("accentypeheader.cloudapp.net", 10100);
            }
            
            string clientID = Guid.NewGuid().ToString();

            Console.WriteLine("Client ID: " + clientID);

            string query = "test";
            while (!String.IsNullOrWhiteSpace(query))
            {
                query = Console.ReadLine();

                List<byte> data = new List<byte>(new byte[] { 0, 1 });
                data.AddRange(System.Text.Encoding.ASCII.GetBytes(query));
                client.Send(data.ToArray(), data.Count);

                byte[] receivedData = client.Receive(ref ep);

                using (var ms = new MemoryStream(receivedData))
                using (var br = new BinaryReader(ms))
                {
                    byte[] header = br.ReadBytes(2);
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

                    Console.WriteLine(String.Join(" ", wordChoices.Select(c => c[0])));
                }
            }
        }
    }
}
