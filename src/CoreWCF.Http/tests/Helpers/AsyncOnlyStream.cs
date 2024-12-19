// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.Http.Tests.Helpers
{
    internal class AsyncOnlyStream : DelegatingStream
    {
        public AsyncOnlyStream(Stream inner)
            : base(inner)
        {
        }

        public override void Flush() => throw new NotSupportedException("Synchronous IO is not allowed");

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous IO is not allowed");

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous IO is not allowed");
    }
}
