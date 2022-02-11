// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    [ServiceContract(ConfigurationName = ServiceMetadataBehavior.MexContractName, Name = ServiceMetadataBehavior.MexContractName, Namespace = ServiceMetadataBehavior.MexContractNamespace)]
    public interface IMetadataExchange
    {
        [OperationContract(Action = MetadataStrings.WSTransfer.GetAction, ReplyAction = MetadataStrings.WSTransfer.GetResponseAction)]
        Message Get(Message request);

        [OperationContract(Action = MetadataStrings.WSTransfer.GetAction, ReplyAction = MetadataStrings.WSTransfer.GetResponseAction)]
        Task<Message> GetAsync(Message request);
    }
}
