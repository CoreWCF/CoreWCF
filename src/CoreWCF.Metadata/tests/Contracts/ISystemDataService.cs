// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Data;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = Constants.NS, Name = "SystemDataService")]
    public interface ISystemDataService
    {
        [OperationContract]
        DataSet GetDataSet();
    }
}
