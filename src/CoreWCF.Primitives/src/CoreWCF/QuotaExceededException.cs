// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    public class QuotaExceededException : Exception //SystemException
    {
        public QuotaExceededException()
            : base()
        {
        }

        public QuotaExceededException(string message)
            : base(message)
        {
        }

        public QuotaExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}