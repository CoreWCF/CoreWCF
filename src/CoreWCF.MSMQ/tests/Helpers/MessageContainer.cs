// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;

namespace CoreWCF.MSMQ.Tests.Helpers
{
    internal class MessageContainer
    {
        public static Stream GetTestMessage()
        {
             string currentDirectory = AppDomain.CurrentDomain.BaseDirectory ?? throw new Exception("AppDomain current directroy is empty");
              string path = Path.Combine(currentDirectory, "Resources/msmqTestMessage.bin");
              return File.OpenRead(path);
        }

        public static Stream GetBadTestMessage()
        {
            var bytes = new byte[] { 0, 1, 0, 1, 4, 2, 37, 110, };
            return new MemoryStream(bytes);
        }
    }
}
