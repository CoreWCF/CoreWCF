// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Configuration
{
    public class NetTcpBindingCollectionElement : StandardBindingCollectionElement<NetTcpBinding, NetTcpBindingElement>
    {
        //public override Binding GetInstance()
        //{
        //    var binding = new NetTcpBinding()
        //    {
        //        Name = Name,
        //        MaxReceivedMessageSize = MaxReceivedMessageSize,
        //        MaxBufferSize = MaxBufferSize,
        //        ReceiveTimeout = ReceiveTimeout,
        //        ReaderQuotas = ReaderQuotas.Clone(),
        //    };

        //    return binding;
        //}
    }
}
