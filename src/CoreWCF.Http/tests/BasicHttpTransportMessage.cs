// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class SimpleBasicHttpTest
    {
        private readonly ITestOutputHelper _output;

        public SimpleBasicHttpTest(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> GetTestsVariations()
        {
            yield return new[] { typeof(BasicHttpTransportWithMessageCredentialWithUserName<CustomTestValidator>) };
            yield return new[] { typeof(BasicHttpTransportWithMessageCredentialWithUserName<CustomAsynchronousTestValidator>) };
        }

        [Theory, Description("transport-security-with-basic-authentication")]
        [MemberData(nameof(GetTestsVariations))]
        public void BasicHttpRequestReplyWithTransportMessageEchoString(Type startupType)
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder(_output, startupType).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.TransportWithMessageCredential);
                BasicHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.BasicHttpMessageCredentialType.UserName;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicHttpWcfService/basichttp.svc")));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.UserName.UserName = "testuser@corewcf";
                clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                Thread.Sleep(5000);

                ((IChannel)channel).Close();
                Console.WriteLine("read ");
            }
        }

        [Fact]
        public void BasicHttpsCustomBindingRequestReplyEchoStringWithHttps()
        {
            string testString = new string('a', 4000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<StartupCustomBinding>(_output).Build();
            using (host)
            {
                host.Start();
                string serviceUrl = $"https://localhost:{host.GetHttpsPort()}/BasicHttpWcfService/basichttp.svc";
                System.ServiceModel.BasicHttpBinding BasicHttpBinding =
                    ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.Transport);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri(serviceUrl)));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication =
                    new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };

                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((IChannel)channel).Close();
            }
        }

        [Fact]
        [UseCulture("en-US")]
        public void BasicHttpsCustomBindingRequestReplyEchoStringWithHttpThrow()
        {
            string testString = new string('a', 4000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<StartupCustomBinding>(_output).Build();
            using (host)
            {
                host.Start();
                string serviceUrl = $"http://localhost:{host.GetHttpsPort()}/BasicHttpWcfService/basichttp.svc";
                System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.Transport);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri(serviceUrl)));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                var exception = Assert.Throws<ArgumentException>(() =>
                {
                    ClientContract.IEchoService channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                });
                Assert.Contains("The provided URI scheme 'http' is invalid", exception.Message);
            }
        }

        // [Fact, Description("transport-security-with-certificate-authentication")]
        // TODO set up in container, tested locally and this works
        internal void BasicHttpRequestReplyWithTransportMessageCertificateEchoString()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<BasicHttpTransportWithMessageCredentialWithCertificate>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.TransportWithMessageCredential);
                BasicHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.BasicHttpMessageCredentialType.Certificate;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.ClientCertificate.SetCertificate(
                StoreLocation.CurrentUser,
                StoreName.My, X509FindType.FindBySerialNumber
                , "16437c0e611928da"
                );

                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((IChannel)channel).Close();
                Console.WriteLine("read ");
            }
        }

        [Fact]
        public void BasicHttpRequestReplyWithTransportMessageCertificateUsingServiceCredentialsEchoString()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<BasicHttpTransportWithMessageCredentialWithUserName>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.TransportWithMessageCredential);
                BasicHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.BasicHttpMessageCredentialType.UserName;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicHttpWcfService/basichttp.svc")));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                string clientCredentialsUserName = "testuser@corewcf";
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.UserName.UserName = clientCredentialsUserName;
                clientCredentials.UserName.Password = clientCredentialsUserName.Reverse().ToString();
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);                
                ((IChannel)channel).Close();
                factory.Close();
            }
        }

        [Fact]
        public void BasicHttpRequestReplyWithTransportMessageCertificateUsingServiceCredentialsEchoStringThrow()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<BasicHttpTransportWithMessageCredentialWithUserName>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.TransportWithMessageCredential);
                    BasicHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.BasicHttpMessageCredentialType.UserName;
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicHttpWcfService/basichttp.svc")));
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    string clientCredentialsUserName = "testuser@corewcf";
                    ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                    clientCredentials.UserName.UserName = clientCredentialsUserName;
                    clientCredentials.UserName.Password = new string('a', 8);
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var exception = Assert.Throws<System.ServiceModel.Security.MessageSecurityException>(() =>
                    {
                        string result = channel.EchoString(testString);
                    });
                    Assert.Contains("At least one security token in the message could not be validated.", exception.InnerException.Message);
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        private static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
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

        internal class CustomAsynchronousTestValidator : UserNamePasswordValidator
        {
            public override async ValueTask ValidateAsync(string userName, string password)
            {
                // simulate a DB / API roundtrip
                await Task.Delay(100);
                if (string.Compare(userName, "testuser@corewcf", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return;
                }

                throw new Exception("Permission Denied");
            }
        }

        internal class MyUserNameCredential : CoreWCF.Description.ServiceCredentials
        {
            public MyUserNameCredential() : base() { }

            protected MyUserNameCredential(MyUserNameCredential other) : base(other) { }

            protected override CoreWCF.Description.ServiceCredentials CloneCore()
            {
                return new MyUserNameCredential(this);
            }

            public override SecurityTokenManager CreateSecurityTokenManager()
            {
                return new MySecurityTokenManager(this);
            }
        }

        internal class MyTokenAuthenticator : UserNameSecurityTokenAuthenticator
        {
            protected override ValueTask<ReadOnlyCollection<CoreWCF.IdentityModel.Policy.IAuthorizationPolicy>> ValidateUserNamePasswordCoreAsync(string userName, string password)
            {
                string clientCredentialsUserName = "testuser@corewcf";
                if (string.Compare(userName, clientCredentialsUserName, StringComparison.OrdinalIgnoreCase) != 0 ||
                    string.Compare(password, clientCredentialsUserName.Reverse().ToString(), StringComparison.OrdinalIgnoreCase) != 0)
                {
                    throw new CoreWCF.IdentityModel.Tokens.SecurityTokenValidationException("Permission Denied");
                }

                List<CoreWCF.IdentityModel.Policy.IAuthorizationPolicy> policies = new List<CoreWCF.IdentityModel.Policy.IAuthorizationPolicy>(1);
                return new ValueTask<ReadOnlyCollection<CoreWCF.IdentityModel.Policy.IAuthorizationPolicy>>(policies.AsReadOnly());
            }
        }

        internal class MySecurityTokenManager : CoreWCF.Security.ServiceCredentialsSecurityTokenManager
        {
            private readonly string _userNameTokenType = "http://schemas.microsoft.com/ws/2006/05/identitymodel/tokens/UserName";

            public MySecurityTokenManager(MyUserNameCredential myUserNameCredential)
                : base(myUserNameCredential)
            {
            }

            public override SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(SecurityTokenRequirement tokenRequirement, out SecurityTokenResolver outOfBandTokenResolver)
            {
                if (tokenRequirement.TokenType == _userNameTokenType)
                {
                    outOfBandTokenResolver = null;
                    return new MyTokenAuthenticator();
                }
                else
                {
                    return base.CreateSecurityTokenAuthenticator(tokenRequirement, out outOfBandTokenResolver);
                }
            }
        }

        internal class BasicHttpTransportWithMessageCredentialWithCertificate : StartupBasicHttpBase
        {
            public BasicHttpTransportWithMessageCredentialWithCertificate() :
                base(CoreWCF.Channels.BasicHttpSecurityMode.TransportWithMessageCredential, BasicHttpMessageCredentialType.Certificate)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.ClientCertificate.Authentication.CertificateValidationMode
                    = CoreWCF.Security.X509CertificateValidationMode.PeerOrChainTrust;
                srvCredentials.ClientCertificate.Authentication.TrustedStoreLocation = StoreLocation.CurrentUser;
                srvCredentials.ServiceCertificate.SetCertificate(
                    StoreLocation.CurrentUser,
                    StoreName.My, X509FindType.FindBySerialNumber,
                    "16437c0e611928da");
                host.Description.Behaviors.Add(srvCredentials);
            }
        }

        internal class BasicHttpTransportWithMessageCredentialWithUserName : StartupBasicHttpBase
        {
            public BasicHttpTransportWithMessageCredentialWithUserName() :
                base(CoreWCF.Channels.BasicHttpSecurityMode.TransportWithMessageCredential, BasicHttpMessageCredentialType.UserName)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new MyUserNameCredential();
                srvCredentials.ClientCertificate.Authentication.CertificateValidationMode
                    = CoreWCF.Security.X509CertificateValidationMode.PeerOrChainTrust;
                host.Description.Behaviors.Add(srvCredentials);
            }
        }

        internal class BasicHttpTransportWithMessageCredentialWithUserName<TUserNamePasswordValidator> : StartupBasicHttpBase
            where TUserNamePasswordValidator : UserNamePasswordValidator, new()
        {
            public BasicHttpTransportWithMessageCredentialWithUserName() :
                base(CoreWCF.Channels.BasicHttpSecurityMode.TransportWithMessageCredential, BasicHttpMessageCredentialType.UserName)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode =
                    CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator =
                    new TUserNamePasswordValidator();
                host.Description.Behaviors.Add(srvCredentials);
            }
        }

        internal abstract class StartupBasicHttpBase
        {
            private readonly CoreWCF.Channels.BasicHttpSecurityMode _basicHttpSecurityMode;
            private readonly BasicHttpMessageCredentialType _credentialType;
            public StartupBasicHttpBase(CoreWCF.Channels.BasicHttpSecurityMode securityMode, BasicHttpMessageCredentialType credentialType)
            {
                _basicHttpSecurityMode = securityMode;
                _credentialType = credentialType;
            }
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public virtual void ChangeHostBehavior(ServiceHostBase host)
            {

            }

            public virtual CoreWCF.Channels.Binding ChangeBinding(BasicHttpBinding basicBinding)
            {
                return basicBinding;
            }

            public void Configure(IApplicationBuilder app)
            {
                CoreWCF.BasicHttpBinding serverBinding = new CoreWCF.BasicHttpBinding(_basicHttpSecurityMode);
                serverBinding.Security.Message.ClientCredentialType = _credentialType;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(ChangeBinding(serverBinding), "/BasicHttpWcfService/basichttp.svc");
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.EchoService>(serviceHost);
                });
            }
        }

        internal class StartupCustomBinding
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                CoreWCF.Channels.CustomBinding customBinding = new CoreWCF.Channels.CustomBinding();

                var textMessageEncodingBindingElement = new CoreWCF.Channels.TextMessageEncodingBindingElement
                {
                    MessageVersion = CoreWCF.Channels.MessageVersion.Soap11
                };
                customBinding.Elements.Add(textMessageEncodingBindingElement);
                customBinding.Elements.Add(new CoreWCF.Channels.HttpsTransportBindingElement());

                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(customBinding, "/BasicHttpWcfService/basichttp.svc");
                });
            }
        }
    }
}
