// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Claims;
using System.Collections.Generic;
using CoreWCF.Security;
using CoreWCF.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Authentication.Negotiate;
#endif

namespace WSHttp
{
    [CollectionDefinition(HttpSysTestCollectionDefinition.HttpSysTestCollection)]
    public class SimpleWSHTTPTest
    {
        private readonly ITestOutputHelper _output;

        public SimpleWSHTTPTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory, Description("no-security-with-an-anonymous-client")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestReplyEchoString(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateWebHostBuilder<WSHttpNoSecurity>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/WSHttpWcfService/basichttp.svc")));
                    channel = factory.CreateChannel();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }                
            }
        }

        // On NetFx, Negotiate auth is forwarded to Windows auth, but Windows auth on Kestrel is not supported on NetFx
        [WindowsNetCoreOnlyTheory, Description("transport-security-with-an-anonymous-client")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestReplyEchoStringTransportSecurity(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportSecurityOnly>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.Transport);
                    wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.None;
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Theory, Description("Demuxer-failure")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestReplyWithTransportMessageEchoStringDemuxFailure(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithUserNameExpire>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                    wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
                    ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                    clientCredentials.UserName.UserName = "testuser@corewcf";
                    clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    try
                    {
                        channel.EchoString(testString);
                    }
                    catch (Exception ex)
                    {
                        Assert.True(typeof(System.ServiceModel.FaultException).Equals(ex.InnerException.GetType()));
                        Assert.Contains("expired security context token", ex.InnerException.Message);
                    }
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
                
            }
        }

        [Theory, Description("user-validation-failure")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestReplyWithTransportMessageEchoStringUserValidationFailure(string bindingType)
        {
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithUserNameExpire>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                    wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
                    ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                    clientCredentials.UserName.UserName = "invalid-user@corewcf";
                    clientCredentials.UserName.Password = "invalid-password";
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    try
                    {
                        ((IChannel)channel).Open();
                    }
                    catch (Exception ex)
                    {
                        Assert.True(typeof(System.ServiceModel.FaultException).Equals(ex.InnerException.GetType()));
                        Assert.Contains("An error occurred when verifying security for the message.", ex.InnerException.Message);
                    }
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }               
            }
        }

        [Theory, Description("transport-security-with-basic-authentication")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestReplyWithTransportMessageEchoString(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithUserName>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                    wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
                    ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                    clientCredentials.UserName.UserName = "testuser@corewcf";
                    clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }               
            }
        }

        [Theory, Description("transport-security-with-basic-authentication-custom-validator")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestReplyWithTransportMessageCustomValidatorEchoString(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithUserNameAndToken>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                    wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
                    ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                    clientCredentials.UserName.UserName = "testuser@corewcf";
                    clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }               
            }
        }

        [Theory, Description("transport-security-with-certificate-authentication")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        internal void WSHttpRequestReplyWithTransportMessageCertificateEchoString(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithCertificate>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                    wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.Certificate;
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
                    ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                    clientCredentials.ClientCertificate.Certificate = ServiceHelper.GetServiceCertificate();
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }               
            }
        }


        [WindowsNetCoreOnlyTheory, Description("transport-security-with-windows-authentication-kestrel")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        internal void WSHttpRequestImpersonateWithKestrel(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithImpersonation>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc")));
                    factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoForImpersonation(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }                
            }
        }

        [WindowsOnlyTheory]
        [Description("transport-security-with-windows-authentication-httpsys")]

#if NET5_0_OR_GREATER
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        internal void WSHttpRequestImpersonateWithHttpSys(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateHttpsWebHostBuilderWithHttpSys<WSHttpTransportWithImpersonationHttpSys>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri("https://localhost:44300/WSHttpWcfService/basichttp.svc")));
                    factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoForImpersonation(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }                
            }
        }

        [WindowsOnlyTheory, Description("no-security-with-an-anonymous-client-using-impersonation")]
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestImpersonateFailsWithoutAuthentication(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateWebHostBuilder<WSHttpNoSecurity>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/WSHttpWcfService/basichttp.svc")));
                    factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() =>
                    {
                        channel.EchoForImpersonation(testString);
                    });
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }                
            }
        }

        [WindowsOnlyTheory]
        [Description("no-security-with-an-anonymous-client-using-impersonation-httpsys")]
#if NET5_0_OR_GREATER
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
        [InlineData("WSHttpBinding")]
        [InlineData("WS2007HttpBinding")]
        public void WSHttpRequestImpersonateWithHttpSysFailsWithoutAuthentication(string bindingType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateWebHostBuilderWithHttpSys<WSHttpNoSecurityHttpSys>(_output).UseSetting("bindingType", bindingType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                        new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8085/WSHttpWcfService/basichttp.svc")));
                    factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                    factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                    };
                    channel = factory.CreateChannel();
                    Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() =>
                    {
                        channel.EchoForImpersonation(testString);
                    });
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }                
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

        internal class AuthenticationDataTokenManager : ServiceCredentialsSecurityTokenManager
        {
            public AuthenticationDataTokenManager(AuthenticationDataServiceCredentials parent) : base(parent)
            {
            }

            public override SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(
                SecurityTokenRequirement tokenRequirement, out SecurityTokenResolver outOfBandTokenResolver)
            {

                if (tokenRequirement.TokenType == SecurityTokenTypes.UserName)
                {
                    outOfBandTokenResolver = null;

                    var validator = new CustomTestValidator();

                    return new CustomUserNameSecurityTokenTestValidator(validator);
                }

                return base.CreateSecurityTokenAuthenticator(tokenRequirement, out outOfBandTokenResolver);
            }
        }

        internal class AuthenticationDataServiceCredentials : CoreWCF.Description.ServiceCredentials
        {
            public override SecurityTokenManager CreateSecurityTokenManager()
            {
                if (UserNameAuthentication.UserNamePasswordValidationMode == UserNamePasswordValidationMode.Custom)
                    return new AuthenticationDataTokenManager(this);

                return base.CreateSecurityTokenManager();
            }

        }

        internal class CustomUserNameSecurityTokenTestValidator : CustomUserNameSecurityTokenAuthenticator
        {
            private readonly UserNamePasswordValidator _validator;

            public CustomUserNameSecurityTokenTestValidator(UserNamePasswordValidator validator) : base(validator)
            {
                _validator = validator;
            }

            protected override async ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>
                ValidateUserNamePasswordCoreAsync(string userName, string password)
            {
                var newPolicies = new List<IAuthorizationPolicy>();

                var currentPolicies = await base.ValidateUserNamePasswordCoreAsync(userName, password);
                newPolicies.AddRange(currentPolicies);

                if (_validator != null)
                {
                    await _validator.ValidateAsync(userName, password);
                    newPolicies.Add(new UserNameSecurityTokenAuthorizationPolicy());
                }

                var readOnlyPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(newPolicies);
                return readOnlyPolicies;
            }
        }

        internal class UserNameSecurityTokenAuthorizationPolicy : IAuthorizationPolicy
        {
            public ClaimSet Issuer { get; private set; }

            public UserNameSecurityTokenAuthorizationPolicy()
            {
                Issuer = ClaimSet.System;
            }

            public string Id => Guid.NewGuid().ToString();

            public bool Evaluate(EvaluationContext evaluationContext, ref object state)
            {
                return true;
            }
        }

        internal class WSHttpTransportWithMessageCredentialWithCertificate : StartupWSHttpBase
        {
            public WSHttpTransportWithMessageCredentialWithCertificate() :
                base(SecurityMode.TransportWithMessageCredential, MessageCredentialType.Certificate)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.ClientCertificate.Authentication.CertificateValidationMode
                    = CoreWCF.Security.X509CertificateValidationMode.Custom;
                srvCredentials.ClientCertificate.Authentication.CustomCertificateValidator
                    = new MyX509CertificateValidator();
                srvCredentials.ServiceCertificate.Certificate = ServiceHelper.GetServiceCertificate();
                host.Description.Behaviors.Add(srvCredentials);
            }

            public class MyX509CertificateValidator : X509CertificateValidator
            {
                public MyX509CertificateValidator()
                {
                }

                public override void Validate(X509Certificate2 certificate)
                {
                    // just Check that there is a certificate.
                    if (certificate == null)
                    {
                        throw new ArgumentNullException("certificate");
                    }
                }
            }
        }

        internal class WSHttpTransportWithMessageCredentialWithUserNameExpire : WSHttpTransportWithMessageCredentialWithUserName
        {
            public override CoreWCF.Channels.Binding ChangeBinding(WSHttpBinding binding)
            {
                CoreWCF.Channels.CustomBinding customBinding = new CoreWCF.Channels.CustomBinding(binding);
                CoreWCF.Channels.SecurityBindingElement security = customBinding.Elements.Find<CoreWCF.Channels.SecurityBindingElement>();
                security.LocalServiceSettings.InactivityTimeout = TimeSpan.FromSeconds(3);
                return customBinding;
            }
        }

        internal class WSHttpTransportWithMessageCredentialWithUserName : StartupWSHttpBase
        {
            public WSHttpTransportWithMessageCredentialWithUserName() :
                base(SecurityMode.TransportWithMessageCredential, MessageCredentialType.UserName)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
                    = CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                    = new CustomTestValidator();
                host.Description.Behaviors.Add(srvCredentials);
            }
        }

        internal class WSHttpTransportWithMessageCredentialWithUserNameAndToken : StartupWSHttpBase
        {
            public WSHttpTransportWithMessageCredentialWithUserNameAndToken() :
                base(SecurityMode.TransportWithMessageCredential, MessageCredentialType.UserName)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new AuthenticationDataServiceCredentials();
                srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
                    = CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                    = new CustomTestValidator();
                host.Description.Behaviors.Add(srvCredentials);
            }
        }

        internal class WSHttpTransportSecurityOnly : StartupWSHttpBase
        {
            public WSHttpTransportSecurityOnly() : base(SecurityMode.Transport, MessageCredentialType.None)
            {
            }

            protected override AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
#if NET472
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddPolicyScheme("Negotiate", "Negotiate", options =>
                    {
                        options.ForwardDefault = HttpSysDefaults.AuthenticationScheme;
                    });
#else
                return services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                    .AddNegotiate();
#endif
            }
        }

        internal class WSHttpNoSecurityHttpSys : WSHttpNoSecurity
        {
            protected override AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddPolicyScheme("Negotiate", "Negotiate", options =>
                    {
                        options.ForwardDefault = HttpSysDefaults.AuthenticationScheme;
                    });
            }
        }

        internal class WSHttpNoSecurity : StartupWSHttpBase
        {
            public WSHttpNoSecurity() : base(SecurityMode.None, MessageCredentialType.None)
            {
            }
        }

        internal class WSHttpTransportWithImpersonationHttpSys : WSHttpTransportWithImpersonation
        {
            protected override AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
#if NET472
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddPolicyScheme("Negotiate", "Negotiate", options =>
                    {
                        options.ForwardDefault = HttpSysDefaults.AuthenticationScheme;
                    });
#else
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddNegotiate();
#endif
            }
        }

        internal class WSHttpTransportWithImpersonation : StartupWSHttpBase
        {
            public WSHttpTransportWithImpersonation() :
                base(SecurityMode.Transport, MessageCredentialType.None)
            {
            }

            public override CoreWCF.Channels.Binding ChangeBinding(WSHttpBinding wsBinding)
            {
                wsBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
                return wsBinding;
            }
        }

        internal abstract class StartupWSHttpBase
        {
            private readonly CoreWCF.SecurityMode _wsHttpSecurityMode;
            private readonly MessageCredentialType _credentialType;
            public StartupWSHttpBase(CoreWCF.SecurityMode securityMode, MessageCredentialType credentialType)
            {
                _wsHttpSecurityMode = securityMode;
                _credentialType = credentialType;
            }
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                AddAuthenticationService(services);
            }

            protected virtual AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
#if NET472
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme);
#else
                return services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                    .AddNegotiate();
#endif
            }

            public virtual void ChangeHostBehavior(ServiceHostBase host)
            {

            }

            public virtual CoreWCF.Channels.Binding ChangeBinding(WSHttpBinding wsBInding)
            {
                return wsBInding;
            }

            public void Configure(IApplicationBuilder app)
            {
                var config = app.ApplicationServices.GetRequiredService<IConfiguration>();

                WSHttpBinding serverBinding;
                if (config["bindingType"] == "WS2007HttpBinding")
                {
                    serverBinding = new WS2007HttpBinding(_wsHttpSecurityMode);
                }
                else
                {
                    serverBinding = new WSHttpBinding(_wsHttpSecurityMode);
                }

                serverBinding.Security.Message.ClientCredentialType = _credentialType;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(ChangeBinding(serverBinding), "/WSHttpWcfService/basichttp.svc");
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.EchoService>(serviceHost);
                });
            }
        }
    }
}
