using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using VietAccentor;
using Utility;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text;

namespace PredictRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private List<ILanguageModel> models = new List<ILanguageModel>();
        private AccentConverter accentConverter;

        private UdpClient udpServer;

        private readonly IPEndPoint PredictEndPoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["PredictEndpoint"].IPEndpoint;

        public override void Run()
        {
            Trace.TraceInformation("PredictRole is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            udpServer = new UdpClient(PredictEndPoint);

            models.Add(ModelFactory.LoadModel("model_v2_1.at", version: 2));
            models.Add(ModelFactory.LoadModel("model_v2_2.at", version: 2));
            models.Add(ModelFactory.LoadModel("model_v2_3.at", version: 2));

            accentConverter = new VietConverter();

            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("PredictRole has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("PredictRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("PredictRole has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {

            await Task.Run(() => 
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var remoteEP = new IPEndPoint(IPAddress.Any, PredictEndPoint.Port);
                        byte[] data = udpServer.Receive(ref remoteEP);

                        Trace.WriteLine("Received data from " + remoteEP.ToString());

                        // include 16-bit header request id
                        const int numBytesPerRequestId = 2;

                        byte[] requestId = data.Take(numBytesPerRequestId).ToArray();

                        string query = System.Text.Encoding.ASCII.GetString(data.Skip(numBytesPerRequestId).ToArray());

                        var wordChoices = Trainer.Predict(query, models, accentConverter);

                        using (var ms = new MemoryStream())
                        using (var bw = new BinaryWriter(ms))
                        {
                            bw.Write(requestId); // write 16-bit request ID.
                            bw.Write((byte)wordChoices.Length);
                            for (int i = 0; i < wordChoices.Length; i++)
                            {
                                byte choiceCount = (byte)wordChoices[i].Length;
                                bw.Write(choiceCount);
                                for (int j = 0; j < choiceCount; j++)
                                {
                                    byte[] choiceBytes = Encoding.UTF8.GetBytes(wordChoices[i][j]);
                                    bw.Write((byte)choiceBytes.Length); // Unicode string length should be very small so casting to byte to save space
                                    bw.Write(choiceBytes);
                                }
                            }
                            byte[] predictionBytes = ms.ToArray();
                            udpServer.Send(predictionBytes, predictionBytes.Length, remoteEP);
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);
                    }
                    
                }
            });
           
        }
    }
}
