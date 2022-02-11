// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Description
{
    public interface IWsdlExportExtension
    {
        void ExportContract(WsdlExporter exporter, WsdlContractConversionContext context);
        void ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext context);
    }
}
