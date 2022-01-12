// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public interface IPolicyExportExtension
    {
        void ExportPolicy(MetadataExporter exporter, PolicyConversionContext context);
    }
}
