// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

[XmlSerializerFormat]
[ServiceContract]
internal interface ISimpleXmlSerializerService
{
    [OperationContract]
    string Echo(string echo);
}
