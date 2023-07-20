// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Authentication;

#if !NETFRAMEWORK
using Microsoft.AspNetCore.Authentication.Negotiate;
#endif

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests;

public class Issue995Tests
{
    private readonly ITestOutputHelper _output;

    public Issue995Tests(ITestOutputHelper _output)
    {
        this._output = _output;
    }


    [Fact]
    public void BasicHttpRequestReplyWithTransportMessageEchoStringUserValidationFailure()
    {
        string testString = new string('a', 3000);
        IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.TransportWithMessageCredential);
            BasicHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.BasicHttpMessageCredentialType.UserName;
            var factory = new System.ServiceModel.ChannelFactory<IMyService>(BasicHttpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicHttpWcfService/basichttp.svc")));
            factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
            };
            ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
            clientCredentials.UserName.UserName = "invalid-user@corewcf";
            clientCredentials.UserName.Password = "invalid-password";
            IMyService channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            var faultException = Assert.Throws<FaultException>(() =>
            {
                string result = channel.Echo(testString);
            });
            ((IChannel)channel).Close();
        }
    }

    private class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            CoreWCF.BasicHttpBinding serverBinding = new CoreWCF.BasicHttpBinding(Channels.BasicHttpSecurityMode.TransportWithMessageCredential);
            serverBinding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;
            app.UseServiceModel(builder =>
            {
                builder.AddService<MyService>();
                builder.AddServiceEndpoint<MyService, IMyService>(serverBinding, "/BasicHttpWcfService/basichttp.svc");
                builder.ConfigureServiceHostBase<MyService>(host =>
                {
                    var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                    srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
                        = CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                    srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                        = new CustomTestValidator();
                    host.Description.Behaviors.Add(srvCredentials);
                });
            });
        }
    }

    private class CustomTestValidator : UserNamePasswordValidator
    {
        public override ValueTask ValidateAsync(string userName, string password)
        {
            if (string.Compare(userName, "testuser@corewcf", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new ValueTask(Task.CompletedTask);
            }

            return new ValueTask(Task.FromException(new Exception("Permission Denied")));
        }
    }

    [System.ServiceModel.ServiceContract]
    private interface IMyService
    {
        [System.ServiceModel.OperationContract]
        string Echo(string value);
    }

    private class MyService : IMyService
    {
        public string Echo(string value) => value;
    }
}
