// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security.NegotiateInternal
{
    internal readonly struct NegotiateInternalSecurityStatusPal
    {
        public readonly NegotiateInternalSecurityStatusErrorCode ErrorCode;
        public readonly Exception? Exception;

        public NegotiateInternalSecurityStatusPal(NegotiateInternalSecurityStatusErrorCode errorCode, Exception? exception = null)
        {
            ErrorCode = errorCode;
            Exception = exception;
        }

        public override string ToString()
        {
            return Exception == null ?
                $"{nameof(ErrorCode)}={ErrorCode}" :
                $"{nameof(ErrorCode)}={ErrorCode}, {nameof(Exception)}={Exception}";
        }

    }
}
