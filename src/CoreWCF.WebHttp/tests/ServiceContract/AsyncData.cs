// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace ServiceContract
{
    [DataContract(Name = "AsyncData", Namespace = "")]
    public class AsyncData
    {
        [DataMember(Name = "string", Order = 0)]
        public string Data { get; set; }
    }
}
