// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal class FaultCodeConstants
    {
        public static class Namespaces
        {
            public const string NetDispatch = "http://schemas.microsoft.com/net/2005/12/windowscommunicationfoundation/dispatcher";
            //public const string Transactions = "http://schemas.microsoft.com/net/2005/12/windowscommunicationfoundation/transactions";
        }

        public static class Codes
        {
            public const string DeserializationFailed = "DeserializationFailed";
            public const string SessionTerminated = "SessionTerminated";
            public const string InternalServiceFault = "InternalServiceFault";

            // 'Transactions' feature fault codes 
            //public const string TransactionHeaderMalformed = "TransactionHeaderMalformed";
            //public const string TransactionHeaderMissing = "TransactionHeaderMissing";
            //public const string TransactionUnmarshalingFailed = "TransactionUnmarshalingFailed";
            //public const string TransactionIsolationLevelMismatch = "TransactionIsolationLevelMismatch";
            //public const string TransactionAborted = "TransactionAborted";
            //public const string IssuedTokenFlowNotAllowed = "IssuedTokenFlowNotAllowed";
        }

        public static class Actions
        {
            public const string NetDispatcher = "http://schemas.microsoft.com/net/2005/12/windowscommunicationfoundation/dispatcher/fault";
            //public const string Transactions = "http://schemas.microsoft.com/net/2005/12/windowscommunicationfoundation/transactions/fault";
        }
    }
}