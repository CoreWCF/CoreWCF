// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [DataContract(Name = "HelloData", Namespace = "")]
    public class SerializationData
    {
        [DataMember(Name = "Items", Order = 0)]
        public List<SerializationDatum> Items { get; set; }
    }

    [DataContract(Name = "HelloDatum", Namespace = "")]
    public class SerializationDatum
    {
        [DataMember(Name = "NumericField", Order = 0)]
        public int NumericField { get; set; }

        [DataMember(Name = "StringField", Order = 1)]
        public string StringField { get; set; }

        [DataMember(Name = "BooleanField", Order = 2)]
        public bool BooleanField { get; set; }

        [DataMember(Name = "DateTimeField", Order = 3)]
        public DateTime DateTimeField { get; set; }

        [DataMember(Name = "DateTimeOffsetField", Order = 4)]
        public DateTimeOffset DateTimeOffsetField { get; set; }

        [DataMember(Name = "TimeSpanField", Order = 5)]
        public TimeSpan TimeSpanField { get; set; }

        [DataMember(Name = "GuidField", Order = 6)]
        public Guid GuidField { get; set; }

        [DataMember(Name = "UriField", Order = 7)]
        public Uri UriField { get; set; }
    }
}
