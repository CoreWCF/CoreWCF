// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests;

public class BinaryFormatterDeprecationTests
{
    private readonly ITestOutputHelper _output;

    [System.ServiceModel.ServiceContract]
    public interface IMyService
    {
        [System.ServiceModel.OperationContract]
        string Echo(string value);
    }

    public class MyService : IMyService
    {
        public string Echo(string value) => value;
    }

    public BinaryFormatterDeprecationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public class GetBinaryFormatterCodePathTestVariations : TheoryData<Type, Func<System.ServiceModel.Channels.Binding>>
    {
        public GetBinaryFormatterCodePathTestVariations()
        {
            Add(typeof(StartupWithCustomBinding), () =>
            {
                System.ServiceModel.Channels.BindingElementCollection elements = new();
                System.ServiceModel.Channels.SecurityBindingElement security = System.ServiceModel.Channels.SecurityBindingElement.CreateUserNameOverTransportBindingElement();
                security = System.ServiceModel.Channels.SecurityBindingElement.CreateSecureConversationBindingElement(security);
                security.EndpointSupportingTokenParameters.Endorsing
                    .OfType<System.ServiceModel.Security.Tokens.SecureConversationSecurityTokenParameters>().Single()
                    .RequireCancellation = false;
                elements.Add(security);
                elements.Add(new System.ServiceModel.Channels.TextMessageEncodingBindingElement());
                elements.Add(new System.ServiceModel.Channels.HttpsTransportBindingElement());
                var binding = new System.ServiceModel.Channels.CustomBinding(elements);
                return binding;
            });
            Add(typeof(StartupWithWSHttpBinding), () =>
            {
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(nameof(WSHttpBinding), System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                System.ServiceModel.Channels.CustomBinding customBinding = new (wsHttpBinding);
                var security = customBinding.Elements.Find<System.ServiceModel.Channels.SecurityBindingElement>();
                security.EndpointSupportingTokenParameters.Endorsing.OfType<System.ServiceModel.Security.Tokens.SecureConversationSecurityTokenParameters>()
                    .Single().RequireCancellation = false;
                return customBinding;
            });
        }
    }

    [Theory(Skip = "Dependent feature not fully implemented yet")]
    [ClassData(typeof(GetBinaryFormatterCodePathTestVariations))]
    public void BinaryFormatterCodePathTests(Type startupType, Func<System.ServiceModel.Channels.Binding> clientBindingFactory)
    {
        string testString = new('a', 3000);
        IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder(_output, startupType).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.Channels.Binding binding = clientBindingFactory.Invoke();
            var factory = new System.ServiceModel.ChannelFactory<IMyService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
            System.ServiceModel.Description.ClientCredentials clientCredentials = (System.ServiceModel.Description.ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(System.ServiceModel.Description.ClientCredentials)];
            clientCredentials.UserName.UserName = "testuser@corewcf";
            clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
            factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
            };
            IMyService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            var result = channel.Echo(testString);
            Assert.Equal(testString, result);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
        }
    }

    internal class StartupWithWSHttpBinding : Startup
    {
        protected override Binding OnCreateBinding()
        {
            WSHttpBinding wsHttpBinding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            wsHttpBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            CustomBinding customBinding = new CustomBinding(wsHttpBinding);
            var security = customBinding.Elements.Find<CoreWCF.Channels.SecurityBindingElement>();
            security.EndpointSupportingTokenParameters.Endorsing
                .OfType<CoreWCF.Security.Tokens.SecureConversationSecurityTokenParameters>().Single()
                .RequireCancellation = false;
            return customBinding;
        }
    }

    internal class StartupWithCustomBinding : Startup
    {
        protected override Binding OnCreateBinding()
        {
            BindingElementCollection elements = new();
            SecurityBindingElement security = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
            security = SecurityBindingElement.CreateSecureConversationBindingElement(security, false);
            security.EndpointSupportingTokenParameters.Endorsing
                .OfType<CoreWCF.Security.Tokens.SecureConversationSecurityTokenParameters>().Single()
                .RequireCancellation = false;
            elements.Add(security);
            elements.Add(new TextMessageEncodingBindingElement());
            elements.Add(new HttpsTransportBindingElement());
            return new CustomBinding(elements);
        }
    }

    internal abstract class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        protected abstract Binding OnCreateBinding();

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<MyService>();
                builder.AddServiceEndpoint<MyService, IMyService>(OnCreateBinding(), "/WSHttpWcfService/basichttp.svc");
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

    internal class CustomTestValidator : UserNamePasswordValidator
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
}
