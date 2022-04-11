// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Serialization;
using CoreWCF.OpenApi.Attributes;

namespace WebHttp
{
    [DataContract(Name = "ExampleContract", Namespace = "http://example.com")]
    internal class ExampleContract
    {
        [DataMember(Name = "SimpleProperty", Order = 1)]
        [OpenApiProperty(Description = "SimpleProperty description.")]
        public string SimpleProperty { get; set; }

        [DataMember(Name = "ComplexProperty", Order = 2)]
        [OpenApiProperty(Description = "ComplexProperty description.")]
        public InnerExampleContract ComplexProperty { get; set; }

        [DataMember(Name = "SimpleCollection", Order = 3)]
        [OpenApiProperty(Description = "SimpleCollection description.")]
        public List<string> SimpleCollection { get; set; }

        [DataMember(Name = "ComplexCollection", Order = 4)]
        [OpenApiProperty(Description = "ComplexCollection description.")]
        public List<InnerExampleContract> ComplexCollection { get; set; }
    }

    [DataContract(Name = "InnerExampleResponse", Namespace = "http://example.com")]
    internal class InnerExampleContract
    {
        [DataMember(Name = "Name", Order = 1)]
        [OpenApiProperty(Description = "Name description.")]
        public string Name { get; set; }
    }
}
