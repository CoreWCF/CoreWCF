using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Configuration
{
    public interface IDispatcherBuilder
    {
        List<IServiceDispatcher> BuildDispatchers(Type serviceType);
    }
}
