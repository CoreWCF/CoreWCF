using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Configuration
{
    public interface IServiceChannelDispatcher
    {
        Task DispatchAsync(RequestContext request, CancellationToken token);
        Task DispatchAsync();
    }
}
