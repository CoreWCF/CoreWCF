// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract, XmlSerializerFormat]
    public interface SCInterfaceA_1144850
    {
        [OperationContract]
        string StringMethodA(string str);
    }
}
