// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    abstract class StreamSecurityUpgradeAcceptorBase : StreamSecurityUpgradeAcceptor
    {
        SecurityMessageProperty remoteSecurity;
        bool securityUpgraded;
        string upgradeString;

        protected StreamSecurityUpgradeAcceptorBase(string upgradeString)
        {
            this.upgradeString = upgradeString;
        }

        public override async Task<Stream> AcceptUpgradeAsync(Stream stream)
        {
            if (stream == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            }

            (stream, remoteSecurity) = await OnAcceptUpgradeAsync(stream);
            return stream;
        }

        public override bool CanUpgrade(string contentType)
        {
            if (securityUpgraded)
            {
                return false;
            }

            return (contentType == upgradeString);
        }

        public override SecurityMessageProperty GetRemoteSecurity()
        {
            // this could be null if upgrade not completed.
            return remoteSecurity;
        }

        protected abstract Task<(Stream, SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream);
    }

}
