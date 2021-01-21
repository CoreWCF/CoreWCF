// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    public interface IStream
    {
        [OperationContract]
        Stream Echo(Stream input);
    }
}
