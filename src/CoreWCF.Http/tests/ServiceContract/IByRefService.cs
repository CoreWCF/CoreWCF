// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IByRefService
    {
        [OperationContract]
        void GetOutParam(string str, out Guid result, bool option);

        [OperationContract]
        bool ExchangeRefParam(ref Guid result);

        [OperationContract]
        void SelectParam(string input, bool selection, ref string optionA, out string optionB);
    }
}
