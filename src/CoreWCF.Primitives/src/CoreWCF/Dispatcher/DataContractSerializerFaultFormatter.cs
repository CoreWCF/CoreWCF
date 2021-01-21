// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    internal class DataContractSerializerFaultFormatter : FaultFormatter
    {
        internal DataContractSerializerFaultFormatter(Type[] detailTypes)
            : base(detailTypes)
        {
        }

        internal DataContractSerializerFaultFormatter(SynchronizedCollection<FaultContractInfo> faultContractInfoCollection)
            : base(faultContractInfoCollection)
        {
        }
    }
}