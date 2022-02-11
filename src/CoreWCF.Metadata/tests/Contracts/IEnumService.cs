// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = Constants.NS, Name = "EnumService")]
    public interface IEnumService
    {
        [OperationContract] void AcceptWrapped(TestWrappedEnum accept);
        [OperationContract] TestWrappedEnum RequestWrapped();
    }

    [DataContract]
    public enum TestEnum
    {
        [EnumMember] One = 1,
        [EnumMember] Two = 2,
        [EnumMember] Three = 3,
        [EnumMember] Five = 5,
    }

    [DataContract]
    public class TestWrappedEnum
    {
        [DataMember] public TestEnum Enum { get; set; }
    }
}
