using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
   public static class TestConfiguration
    {
        public static IWebHostBuilder UseBiroj(this IWebHostBuilder webHostBuilder)
        {
            webHostBuilder.ConfigureServices(services =>
            {

            });
            return webHostBuilder;
        }
    }
}
