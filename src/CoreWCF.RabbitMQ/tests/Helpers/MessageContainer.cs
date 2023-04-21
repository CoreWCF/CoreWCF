// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.RabbitMQ.Tests.Helpers
{
    internal class MessageContainer
    {
        public static Stream GetTestMessage()
        {
            string currentDirectory = Environment.CurrentDirectory ?? throw new Exception("Environment current directory is empty");
            string path = Path.Combine(currentDirectory, "MessageFiles/rabbitmqTestMessage.bin");
            return File.OpenRead(path);
        }
    }
}
