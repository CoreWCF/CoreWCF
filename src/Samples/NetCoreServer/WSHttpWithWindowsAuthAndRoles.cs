// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace NetCoreServer
{
    class WSHttpWithWindowsAuthAndRoles
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            WSHttpBinding wSHttpBinding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            wSHttpBinding.Security.Message.ClientCredentialType = MessageCredentialType.Windows;
            app.UseServiceModel(builder =>
            {
                builder.AddService<ServerLogic.EchoService>();
                builder.AddServiceEndpoint<ServerLogic.EchoService, Contract.IEchoService>(wSHttpBinding, "/wsHttp");
                builder.AddServiceEndpoint<ServerLogic.EchoService, Contract.IEchoService>(new NetTcpBinding(), "/nettcp");
                Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                builder.ConfigureServiceHostBase<ServerLogic.EchoService>(serviceHost);
            });
        }

        public void ChangeHostBehavior(ServiceHostBase host)
        {
            var srvCredentials = new CoreWCF.Description.ServiceCredentials();
            var _ldapSettings = new LdapSettings("yourownserver.mscore.local", "mscore.local", "yourowntoporg");
            srvCredentials.WindowsAuthentication.LdapSetting = _ldapSettings;
            host.Description.Behaviors.Add(srvCredentials);
        }
    }
}
