using CoreWCF;
using CoreWCF.Channels;
using Helpers;
using ServiceContract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class RequestReplyService : IRequestReplyService
    {
        static List<string> log = new List<string>();
        static int seed = DateTime.Now.Millisecond;
        static Random rand = new Random(seed);
        FlowControlledStream localStream;

        public void UploadData(string data)
        {
            if (data.Contains("ContentToReplace") || data.Contains("ReplacedContent") || data.Contains("ResponseReplaceThisContent"))
            {
                log.Add(string.Format("UploadData received {0}", data));
            }
            else
            {
                log.Add(string.Format("UploadData received {0} length string.", data.Length));
            }

            // Access the RemoteEndpointMessageProperty
            RemoteEndpointMessageProperty remp = OperationContext.Current.IncomingMessageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
            IPHostEntry hostEntry = Dns.GetHostEntry(Environment.MachineName);
            bool success = false;
            foreach (IPAddress address in hostEntry.AddressList)
            {
                if (remp.Address == address.ToString())
                {
                    success = true;
                    break;
                }
            }

            if (!success)
            {
                log.Add("RemoteEndpointMessageProperty did not contain the address of this machine.");
            }
        }

        public string DownloadData()
        {
            string data = CommonUtility.CreateInterestingString(rand.Next(512, 4096));
            log.Add(string.Format("DownloadData returning {0} length string.", data.Length));
            return data;
        }

        public void UploadStream(Stream stream)
        {
            int readResult;
            int bytesRead = 0;
            byte[] buffer = new byte[1000];
            do
            {
                readResult = stream.Read(buffer, 0, buffer.Length);
                bytesRead += readResult;
            }
            while (readResult != 0);

            stream.Close();

            log.Add(string.Format("UploadStream read {0} bytes from the client's stream.", bytesRead));
        }

        public Stream DownloadStream()
        {
            log.Add("DownloadStream");
            localStream = new FlowControlledStream();
            localStream.ReadThrottle = TimeSpan.FromMilliseconds(500);
            // Setting to 900ms instead of 1 second because sometimes 3 reads occur and the read buffer grows with
            // each read by a factor of 16 up to 64KB and this is causing the client to exceed it's MaxReceivedMessageSize
            localStream.StreamDuration = TimeSpan.FromMilliseconds(900);

            return localStream;
        }

        public Stream DownloadCustomizedStream(TimeSpan readThrottle, TimeSpan streamDuration)
        {
            log.Add("DownloadStream");
            localStream = new FlowControlledStream();
            localStream.ReadThrottle = readThrottle;
            localStream.StreamDuration = streamDuration;

            return localStream;
        }

        public void ThrowingOperation(Exception exceptionToThrow)
        {
            log.Add("ThrowingOperation");
            throw exceptionToThrow;
        }

        public string DelayOperation(TimeSpan delay)
        {
            log.Add("DelayOperation");
            Thread.CurrentThread.Join(delay);
            return "Done with delay.";
        }

        public List<string> GetLog()
        {
            return log;
        }
    }
}