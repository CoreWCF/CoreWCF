using System;
using System.IO;
using Helpers;

namespace Services
{
    public class EchoForwardService : ServiceContract.IEcho,ServiceContract.IForward
    {
        public Stream Echo(Stream stream)
        {
            RequestStream result = null;
            byte[] buffer = new byte[2000];
            int num = 0;
            int num2;
            while ((num2 = stream.Read(buffer, 0, 2000)) != 0)
            {
                num += num2;
            }
            if (num != -1)
            {
                result = new RequestStream((long)num);
            }
            return result;
        }

        public Stream Forward(Stream stream)
        {
            System.ServiceModel.Channels.CustomBinding binding = ClientHelper.GetBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEcho>(binding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfServiceEcho/StreamingTest1.svc")));
            ClientContract.IEcho echo = factory.CreateChannel();
            Stream result = null;
            try
            {
                result = echo.Echo(stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return result;
        }
    }

    internal class RequestStream : Stream
    {
        public RequestStream(long messageSize)
        {
            this.messageSize = messageSize;
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        public override long Position
        {
            get
            {
                throw new Exception("The method or operation is not implemented.");
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long num;
            if (this.numberoftotalbytes < this.messageSize - 12000L)
            {
                num = 12000L;
            }
            else
            {
                if (this.numberoftotalbytes >= this.messageSize)
                {
                    return 0;
                }
                num = this.messageSize - this.numberoftotalbytes;
            }
            num = Math.Min((long)count, num);
            int num2 = 0;
            while ((long)num2 < num)
            {
                buffer[num2] = (byte)(num2 % 21212);
                num2++;
            }
            this.numberoftotalbytes += num;
            return (int)num;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void SetLength(long value)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private long numberoftotalbytes;
        private long messageSize;
    }
}

