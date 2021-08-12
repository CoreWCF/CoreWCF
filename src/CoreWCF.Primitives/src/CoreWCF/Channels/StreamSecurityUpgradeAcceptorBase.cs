// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    internal abstract class StreamSecurityUpgradeAcceptorBase : StreamSecurityUpgradeAcceptor
    {
        private SecurityMessageProperty _remoteSecurity;
        private bool _securityUpgraded;
        private readonly string _upgradeString;

        protected StreamSecurityUpgradeAcceptorBase(string upgradeString)
        {
            _upgradeString = upgradeString;
        }

        public override async Task<Stream> AcceptUpgradeAsync(Stream stream)
        {
            if (stream == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            }

            (stream, _remoteSecurity) = await OnAcceptUpgradeAsync(stream);
            _securityUpgraded = true;
            return stream;
        }

        public override bool CanUpgrade(string contentType)
        {
            if (_securityUpgraded)
            {
                return false;
            }

            return (contentType == _upgradeString);
        }

        public override SecurityMessageProperty GetRemoteSecurity()
        {
            // this could be null if upgrade not completed.
            return _remoteSecurity;
        }

        protected abstract Task<(Stream, SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream);
    }
}
