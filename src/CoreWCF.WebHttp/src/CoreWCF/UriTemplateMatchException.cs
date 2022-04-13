// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    [Serializable]
    public class UriTemplateMatchException : SystemException
    {
        public UriTemplateMatchException()
        {
        }

        public UriTemplateMatchException(string message)
            : base(message)
        {
        }

        public UriTemplateMatchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected UriTemplateMatchException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
