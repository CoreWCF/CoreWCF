using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    public abstract class StreamUpgradeAcceptor
    {
        protected StreamUpgradeAcceptor()
        {
        }

        public abstract bool CanUpgrade(string contentType);

        public abstract Task<Stream> AcceptUpgradeAsync(Stream stream);
    }
}
