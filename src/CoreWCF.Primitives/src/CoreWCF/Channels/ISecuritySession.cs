using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    public interface ISecuritySession : ISession
    {
        EndpointIdentity RemoteIdentity { get; }
    }
}
