//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------
namespace CoreWCF.Http.Tests
{
    using ClientContract;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.ServiceModel;
    using System.Threading;

    public class ClientReceiver : IPushCallback
    {
        public ManualResetEvent LogReceived { get; set; }
        public ManualResetEvent ReceiveDataInvoked { get; set; }
        public ManualResetEvent ReceiveDataCompleted { get; set; }
        public ManualResetEvent ReceiveStreamInvoked { get; set; }
        public ManualResetEvent ReceiveStreamCompleted { get; set; }
        public string Name { get; set; }

        public List<string> ServerLog { get; set; }

        public ClientReceiver()
        {
            LogReceived = new ManualResetEvent(false);
            ReceiveDataInvoked = new ManualResetEvent(false);
            ReceiveDataCompleted = new ManualResetEvent(false);
            ReceiveStreamInvoked = new ManualResetEvent(false);
            ReceiveStreamCompleted = new ManualResetEvent(false);
            Name = "ClientReceiver_" + DateTime.Now;
        }

        public void ReceiveData(string data)
        {
            Console.WriteLine("[{0}] ReceiveData received a message.", Name);
            ReceiveDataInvoked.Set();
            if (data == "LastMessage")
            {
                ReceiveDataCompleted.Set();
            }
        }

        public void ReceiveStream(Stream stream)
        {
            ReceiveStreamInvoked.Set();

            int readResult;
            byte[] buffer = new byte[1000];
            do
            {
                try
                {
                    readResult = stream.Read(buffer, 0, buffer.Length);
                    Console.WriteLine("[{0}] just read {1} bytes.{2}stream.Position = {3}", this.Name, readResult, Environment.NewLine, stream.Position);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Caught exception when trying to read: {0}", ex);
                    throw;
                }
            }
            while (readResult != 0);

            stream.Close();

            Console.WriteLine("[{0}] ReceiveStream invoked.", Name);
            ReceiveStreamCompleted.Set();
        }

        public void ReceiveStreamWithException(Stream stream)
        {
            Console.WriteLine("[{0}] ReceiveStreamWithException invoked.", Name);
            ReceiveStreamInvoked.Set();

            try
            {

                int bytesRead = 0;
                byte[] buffer = new byte[1000];
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    buffer = new byte[1000];
                }

            }
            catch (Exception ex)
            {
                if (ex.InnerException.GetType().FullName != typeof(CommunicationException).FullName)
                {
                    Console.WriteLine(ex);
                    throw;
                }

            }

            Console.WriteLine("[{0}] ReceiveStreamWithException completed.", Name);
            ReceiveStreamCompleted.Set();
        }

        public void ReceiveLog(List<string> log)
        {
            ServerLog = log;
            LogReceived.Set();
        }
    }
}
