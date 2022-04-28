// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    [MessageContract(IsWrapped = false)]
    internal class GetResponse
    {
        internal GetResponse() { }
        internal GetResponse(MetadataSet metadataSet)
            : this()
        {
            Metadata = metadataSet;
        }

        [MessageBodyMember(Name = MetadataStrings.MetadataExchangeStrings.Metadata, Namespace = MetadataStrings.MetadataExchangeStrings.Namespace)]
        internal MetadataSet Metadata { get; set; }
    }
}
