using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Configuration
{
    public interface IServiceDispatcher
    {
        Uri BaseAddress { get; }
        Binding Binding { get; }
        Task DispatchAsync(RequestContext request, IChannel channel, CancellationToken token);
    }
}
