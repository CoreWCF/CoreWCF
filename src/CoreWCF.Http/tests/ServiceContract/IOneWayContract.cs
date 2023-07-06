// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IOneWayContract
    {
        // Token: 0x06000864 RID: 2148
        [OperationContract(AsyncPattern = true, IsOneWay = true)]
        Task OneWay(string s);
    }
}
