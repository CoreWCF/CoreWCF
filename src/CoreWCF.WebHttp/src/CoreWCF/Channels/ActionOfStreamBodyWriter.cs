// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.Channels
{
    internal class ActionOfStreamBodyWriter : StreamBodyWriter
    {
        private readonly Action<Stream> _actionOfStream;

        public ActionOfStreamBodyWriter(Action<Stream> actionOfStream)
            : base(false)
        {
            _actionOfStream = actionOfStream;
        }

        protected override void OnWriteBodyContents(Stream stream)
        {
            _actionOfStream(stream);
        }

        internal static StreamBodyWriter CreateStreamBodyWriter(Action<Stream> streamAction)
        {
            if (streamAction == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(streamAction));
            }

            return new ActionOfStreamBodyWriter(streamAction);
        }
    }
}
