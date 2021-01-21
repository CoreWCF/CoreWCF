// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using Helpers;
using ServiceContract;

namespace Services
{
    public class VoidStreamService : IVoidStreamService
    {
        public void Operation(Stream input)
        {
            string value = ServiceHelper.GetStringFrom(input);
            return;
        }
    }

    public class StreamStreamSyncService : IStreamStreamSyncService
    {
        public Stream Operation(Stream input)
        {
            string value = ServiceHelper.GetStringFrom(input);
            return ServiceHelper.GetStreamWithStringBytes(value);
        }
    }

    public class RefStreamService : IRefStreamService
    {
        public void Operation(ref Stream input)
        {
            string value = ServiceHelper.GetStringFrom(input);
            input = ServiceHelper.GetStreamWithStringBytes(value + "/" + value);
        }
    }

    public class StreamInOutService : IStreamInOutService
    {
        public void Operation(Stream input, out Stream output)
        {
            string value = ServiceHelper.GetStringFrom(input);
            output = ServiceHelper.GetStreamWithStringBytes(value + "/" + value);
        }
    }

    public class StreamStreamAsyncService : IStreamStreamAsyncService
    {
        public async Task<Stream> TwoWayMethodAsync(Stream input)
        {
            return await Task.Run(() => ProcessTwoWayAsync(input));
        }

        public Stream ProcessTwoWayAsync(Stream input)
        {
            string value = ServiceHelper.GetStringFrom(input);
            return ServiceHelper.GetStreamWithStringBytes(value);
        }
    }

    public class InFileStreamService : IMessageContractStreamInReturnService
    {
        public MessageContractStreamOneIntHeader Operation(MessageContractStreamNoHeader input)
        {
            long size = 0;
            const int BUFFER = 1000;
            byte[] buffer = new byte[BUFFER];
            long read;
            do
            {
                read = input.stream.Read(buffer, 0, BUFFER);
                size += read;
            } while (read > 0);

            return ServiceHelper.GetMessageContractStreamOneIntHeader(size.ToString());
        }
    }

    public class ReturnFileStreamService : IMessageContractStreamInReturnService
    {
        public MessageContractStreamOneIntHeader Operation(MessageContractStreamNoHeader input)
        {
            FileStream file = File.OpenRead("temp.dat");
            MessageContractStreamOneIntHeader message = new MessageContractStreamOneIntHeader();
            message.input = file;
            return message;
        }
    }

    public class MessageContractStreamInOutService : IMessageContractStreamInReturnService
    {
        public MessageContractStreamOneIntHeader Operation(MessageContractStreamNoHeader input)
        {
            string value = ServiceHelper.GetStringFrom(input.stream);
            var msg = new MessageContractStreamOneIntHeader
            {
                input = ServiceHelper.GetStreamWithStringBytes(value)
            };
            return msg;
        }
    }

    public class MessageContractStreamMutipleOperationsService : IMessageContractStreamMutipleOperationsService
    {
        public MessageContractStreamNoHeader Operation1(MessageContractStreamOneStringHeader input)
        {
            string value = ServiceHelper.GetStringFrom(input.input);
            return ServiceHelper.GetMessageContractStreamNoHeader(value);
        }

        public MessageContractStreamTwoHeaders Operation2(MessageContractStreamOneIntHeader input)
        {
            string value = ServiceHelper.GetStringFrom(input.input);
            return ServiceHelper.GetMessageContractStreamTwoHeaders(value);
        }
    }
}
