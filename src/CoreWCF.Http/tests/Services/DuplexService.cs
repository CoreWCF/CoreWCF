//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------
namespace Services
{
    using CoreWCF;
    using Helpers;
    using ServiceContract;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Xunit.Abstractions;

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public class DuplexService :  IDuplexService
    {
        private string exceptionstring = string.Empty;
        List<string> log = new List<string>();
        static bool continuePushingData;

        static int seed = DateTime.Now.Millisecond;
        static Random rand = new Random(seed);

        static FlowControlledStream localStream;

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
        }

        public string DownloadData()
        {
            string data = Common.CreateInterestingString(rand.Next(512, 4096));
            log.Add(string.Format("DownloadData returning {0} length string", data.Length));
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

            log.Add(string.Format("UploadStream read {0} bytes from client's stream", bytesRead));
        }

        // Not using the localStream because this is the request-reply operation.
        public Stream DownloadStream()
        {
            log.Add("DownloadStream");
            FlowControlledStream stream = new FlowControlledStream();
            stream.StreamDuration = TimeSpan.FromSeconds(1);
            stream.ReadThrottle = TimeSpan.FromMilliseconds(500);
            return stream;
        }

        public void StartPushingData()
        {
            log.Add("StartPushingData");
            continuePushingData = true;
            IPushCallback pushCallbackChannel = OperationContext.Current.GetCallbackChannel<IPushCallback>();
            ThreadPool.QueueUserWorkItem(new WaitCallback(PushData), pushCallbackChannel);
        }

        public void StopPushingData()
        {
            log.Add("StopPushingData");
            continuePushingData = false;
        }

        public void StartPushingStream()
        {
            log.Add("StartPushingStream");
            IPushCallback pushCallbackChannel = OperationContext.Current.GetCallbackChannel<IPushCallback>();
            ThreadPool.QueueUserWorkItem(new WaitCallback(PushStream), pushCallbackChannel);
        }

        public void StartPushingStreamLongWait()
        {
            log.Add("StartPushingStream");
            IPushCallback pushCallbackChannel = OperationContext.Current.GetCallbackChannel<IPushCallback>();
            ThreadPool.QueueUserWorkItem(new WaitCallback(PushStreamLongwait), pushCallbackChannel);
        }

        public void StopPushingStream()
        {
            log.Add("StopPushingStream");
            localStream.StopStreaming = true;
        }

        void PushData(object state)
        {
            IPushCallback pushCallbackChannel = state as IPushCallback;

            do
            {
                pushCallbackChannel.ReceiveData(Common.CreateInterestingString(rand.Next(4, 256)));
            }
            while (continuePushingData);

            pushCallbackChannel.ReceiveData("LastMessage");
        }

        void PushStream(object state)
        {
            IPushCallback pushCallbackChannel = state as IPushCallback;
            localStream = new FlowControlledStream();
            localStream.ReadThrottle = TimeSpan.FromMilliseconds(800);

            pushCallbackChannel.ReceiveStream(localStream);
        }

        void PushStreamLongwait(object state)
        {
            IPushCallback pushCallbackChannel = state as IPushCallback;
            localStream = new FlowControlledStream();
            localStream.ReadThrottle = TimeSpan.FromMilliseconds(3000);
            localStream.StreamDuration = TimeSpan.FromSeconds(2);

            try
            {
                pushCallbackChannel.ReceiveStreamWithException(localStream);
            }
            catch (Exception ex)
            {
                this.exceptionstring = ex.GetType().Name;
            }
        }

        /// <summary>
        /// This method was used to pass the exception message caught in the PushStreamLongwait method to the client
        /// </summary>
        /// <returns></returns>
        public string GetExceptionString()
        {
            return this.exceptionstring;
        }

        public void GetLog()
        {
            IPushCallback pushCallbackChannel = OperationContext.Current.GetCallbackChannel<IPushCallback>();
            pushCallbackChannel.ReceiveLog(log);
        }
    }
}