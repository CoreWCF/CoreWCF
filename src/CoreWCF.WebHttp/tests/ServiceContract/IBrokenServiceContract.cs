// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF;
using CoreWCF.Web;

namespace ServiceContract;

[ServiceContract]
internal interface IBrokenServiceContract
{
    [OperationContract]
    [WebGet(UriTemplate = "JsonReference", ResponseFormat = WebMessageFormat.Json)]
    DtoWithReference GetWithReference();

    [OperationContract]
    [WebGet(UriTemplate = "JsonCircularGraph", ResponseFormat = WebMessageFormat.Json)]
    DtoWithCircularDependency GetCircularGraph();
}

[DataContract(IsReference = true)]
public class DtoWithReference
{
    public int Id { get; set; }
}

[DataContract(IsReference = true)]
public class DtoWithCircularDependency
{
    public DtoWithCircularDependency ReferenceTo { get; set; }
}
