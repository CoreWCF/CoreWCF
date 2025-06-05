// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.Xml
{
    internal static class XmlDictionaryWriterExtensions
    {
        private static readonly bool s_frameworkSupportsAsyncWriteOperations = Environment.Version.Major >= 6;

        internal static bool SupportsAsync(this XmlDictionaryWriter writer)
        {
            // Unfortunately not every XmlWriter supports asynchronous IO.
            // And there's no easy API to check for this.
            //
            // writer.Settings.Async is often null, so also not reliable
            return s_frameworkSupportsAsyncWriteOperations &&
                (writer is IAsyncXmlWriter || (writer.Settings?.Async ?? false));
        }
    }
}
