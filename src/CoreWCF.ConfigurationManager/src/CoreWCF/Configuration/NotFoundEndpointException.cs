// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Configuration
{
    public class NotFoundEndpointException : Exception
    {
        public NotFoundEndpointException()
        {
        }

        public NotFoundEndpointException(string message)
            : base(message)
        {
        }

        public NotFoundEndpointException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
