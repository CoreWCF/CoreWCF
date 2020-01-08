using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
    public interface ITransportServiceBuilder
    {
        void Configure(IApplicationBuilder app);
    }
}
