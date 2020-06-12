using CoreWCF;
using CoreWCF.Channels;
using ServiceContract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Helpers;

namespace Services.AsyncStreamingService
{
    [ServiceBehavior()]
    public class Service : IService
    {
        FileStream largeFileStream = null;

        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        public Stream GetStream()
        {
            //Log.Info("Service got request to GetStream");
            //if (Common.testImpersonation && !Common.IsImpersonatedUser(ServiceSecurityContext.Current.WindowsIdentity.Name))
            //{
            //    throw new ApplicationException("Test failed. Did not find expected username");
            //}
            return GetFileStream();
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        public Message GetMessage()
        {
            FileStream largeFileStream = new FileStream("LargeFile.txt", FileMode.Open, FileAccess.Read, FileShare.Read, Common.streamBufferSize, true);
            //Log.Info("Service got request to GetMessage");
            //if (Common.testImpersonation && !Common.IsImpersonatedUser(ServiceSecurityContext.Current.WindowsIdentity.Name))
            //{
            //    throw new ApplicationException("Test failed. Did not find expected username");
            //}
            return Message.CreateMessage(MessageVersion.None, "", GetFileStream());
        }

        public Stream GetSlowStream()
        {
            int messageSize = 300 * 1024;
            byte[] pattern = new byte[300];
            Common.FillPatternWithRandomBytes(ref pattern);
            CustomStream customStream = new CustomStream(pattern, messageSize, true);
            return customStream;
        }

        public void CloseStream()
        {
            if (largeFileStream != null)
            {
                largeFileStream.Close();
            }
        }

        public FileStream GetFileStream()
        {
            if (largeFileStream == null)
            {
                largeFileStream = new FileStream(@"Data\LargeFile.txt", FileMode.Open, FileAccess.Read, FileShare.Read, Common.streamBufferSize, true);
            }
            return largeFileStream;
        }
    }
}
