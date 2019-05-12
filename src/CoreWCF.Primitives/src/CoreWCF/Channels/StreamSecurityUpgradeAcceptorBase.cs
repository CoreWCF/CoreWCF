using CoreWCF.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("stream");
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

        protected abstract Task<(Stream,SecurityMessageProperty)> OnAcceptUpgradeAsync(Stream stream);
    }

}
