// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public interface IWsdlExportExtension
    {
        void ExportContract(WsdlExporter exporter, WsdlContractConversionContext context);
        void ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext context);
    }
}
