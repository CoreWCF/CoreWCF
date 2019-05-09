using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Channels
{
    public interface ISecuritySession : ISession
    {
        EndpointIdentity RemoteIdentity { get; }
    }
}
