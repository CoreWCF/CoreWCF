// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using ServiceContract;

namespace Services
{
    internal class SimpleEchoService : IEchoService
    {
        public string EchoString(string echo) => echo;
        public Stream EchoStream(Stream echo)
        {
            var stream = new MemoryStream();
            echo.CopyTo(stream);
            stream.Position = 0;
            return stream;
        }

        public async Task<string> EchoStringAsync(string echo)
        {
            await Task.Yield();
            return echo;
        }

        public async Task<Stream> EchoStreamAsync(Stream echo)
        {
            var stream = new MemoryStream();
            await echo.CopyToAsync(stream);
            stream.Position = 0;
            return stream;
        }

        public string EchoToFail(string echo)
        {
            return echo;
        }
    }
}
