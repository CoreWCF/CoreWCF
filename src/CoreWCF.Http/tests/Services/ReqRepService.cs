// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using ServiceContract;
using Xunit;

namespace Services
{
    public class ReqRepService : IStream
    {

        public Stream Echo(Stream input)
        {
            long num2 = 0;
            long messageSize = 20000;
            int num3 = 0;
            byte[] buffer = new byte[5000];
            int num4;
            while ((num4 = input.Read(buffer, 0, 370)) != 0)
            {
                num3 = num4 + num3;
            }
            Assert.Equal(num2, (long)num3);
            return new MyStream(messageSize);
        }
    }


    internal class MyStream : Stream
    {
        private long messageSize;

        public MyStream(long messageSize)
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
            if (this.numberoftotalbytes < this.messageSize)
            {
                long num;
                if (this.numberoftotalbytes < this.messageSize - 5000L)
                {
                    num = 5000L;
                }
                else
                {
                    num = (long)((int)(this.messageSize - this.numberoftotalbytes));
                }
                num = Math.Min((long)count, num);
                int num2 = 0;
                while ((long)num2 < num)
                {
                    buffer[num2] = (byte)(num2 % 255);
                    num2++;
                }
                this.numberoftotalbytes += num;
                return (int)num;
            }
            return 0;
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

    }
}
