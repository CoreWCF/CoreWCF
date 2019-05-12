using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
    public interface IDispatcherBuilder
    {
        List<IServiceDispatcher> BuildDispatchers(Type serviceType);
    }
}
