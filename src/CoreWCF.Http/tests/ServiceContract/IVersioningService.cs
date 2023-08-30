// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IVersioningServiceNew
    {
        [OperationContract()]
        void Method_NewContractA_out(NewContractA obj1, out NewContractA obj2);

        [OperationContract()]
        NewContractA Method_NewContractA_ref(ref NewContractA obj1);

        [OperationContract()]
        NewContractA Method_NewContractA(NewContractA obj1);
    }

    [ServiceContract]
    public interface IVersioningServiceOld
    {
        [OperationContract()]
        void Method_OldContractA_out(OldContractA obj1, out OldContractA obj2);

        [OperationContract()]
        OldContractA Method_OldContractA_ref(ref OldContractA obj1);

        [OperationContract()]
        OldContractA Method_OldContractA(OldContractA obj1);
    }

    [DataContract(Name = "ContractA")]
    public class OldContractA
    {
        [DataMember(IsRequired = false)]
        public int i = 9;
    }

    [DataContract(Name = "ContractA")]
    public class NewContractA
    {
        [DataMember(IsRequired = false)]
        public int j = 12;
    }
}