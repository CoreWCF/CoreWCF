﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace CoreWCF.Channels
{
    public abstract class StreamUpgradeAcceptor
    {
        protected StreamUpgradeAcceptor()
        {
        }

        public abstract bool CanUpgrade(string contentType);

        public abstract Task<Stream> AcceptUpgradeAsync(Stream stream);

        public virtual IFeatureCollection Features { get; }= new FeatureCollection();
    }
}
