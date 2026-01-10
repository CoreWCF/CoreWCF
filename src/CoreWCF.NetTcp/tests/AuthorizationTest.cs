// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Data;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class AuthorizationTest
    {
        private readonly ITestOutputHelper _output;
        public const string FakeAuthRelativePath = "/nettcp.svc/fake-auth";

        public AuthorizationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetAuthorizationVariations))]
        public void AuthorizationBasedOnRolesTest(Dictionary<string, List<Claim>> authorizeClaims, Claim[] authenticatedClaims, bool shouldSucced )
        {
            string testString = "a" + PrincipalPermissionMode.Always + "test";
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupForAuthorization>(_output).Build();
            var serviceBuilder = host.Services.GetRequiredService<IServiceBuilder>();
            serviceBuilder.ConfigureAllServiceHostBase(serviceHostBase =>
            {
                var customPolicy = new CustomAuthorizationPolicy(authenticatedClaims);
                serviceHostBase.Authorization.ExternalAuthorizationPolicies = new List<IAuthorizationPolicy> { customPolicy }.AsReadOnly();

                var authorizeClaimsBehavior = new AuthorizeClaimsSetterOperationBehavior(authorizeClaims, true);
                foreach (var endpoint in serviceHostBase.Description.Endpoints)
                {
                    OperationDescription echoAuthOperation = endpoint.Contract.Operations.Find("EchoForAuthorizarionOneRole");
                    if (echoAuthOperation != null)
                    {
                        if (!echoAuthOperation.OperationBehaviors.Contains(typeof(AuthorizeClaimsSetterOperationBehavior)))
                        {
                            echoAuthOperation.OperationBehaviors.Add(authorizeClaimsBehavior);
                        }
                    }
                }
            });

            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + FakeAuthRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    if (shouldSucced)
                    {
                        string result = channel.EchoForAuthorizarionOneRole(testString);
                        Assert.Equal(testString, result);
                    }
                    else
                    {
                        var sade = Assert.Throws<SecurityAccessDeniedException>(() => channel.EchoForAuthorizarionOneRole(testString));
                        Assert.Contains("Access is denied", sade.Message);
                    }

                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        public static IEnumerable<object[]> GetAuthorizationVariations()
        {
            var adminRoleClaim = new Claim(ClaimTypes.Role, "CoreWCFGroupAdmin", Rights.Identity);
            var userRoleClaim = new Claim(ClaimTypes.Role, "CoreWCFGroupUsers", Rights.Identity);
            var alwaysFailsClaim = new Claim(ClaimTypes.Role, "AlwaysFails", Rights.Identity);
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Default", new() { adminRoleClaim } } }, new Claim[] { adminRoleClaim }, true };
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Default", new() { adminRoleClaim } } }, new Claim[] { userRoleClaim }, false };
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Default", new() { adminRoleClaim } } }, new Claim[] { }, false };
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Default", new() { alwaysFailsClaim } } }, new Claim[] { adminRoleClaim }, false };
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Primary", new() { adminRoleClaim } },
                                                                                { "Secondary", new() { userRoleClaim} } }, new Claim[] { adminRoleClaim }, false };
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Primary", new() { adminRoleClaim } },
                                                                                { "Secondary", new() { userRoleClaim} } }, new Claim[] { userRoleClaim }, false };
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Default", new() { adminRoleClaim, userRoleClaim } } }, new Claim[] { adminRoleClaim }, true };
            yield return new object[] { new Dictionary<string, List<Claim>>() { { "Default", new() { adminRoleClaim, userRoleClaim } } }, new Claim[] { userRoleClaim }, true };
        }

        [Fact]
        public void AuthorizationBasedOnRolesUsingAttributesTest()
        {
            string testString = "a" + PrincipalPermissionMode.Always + "test";
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupForAuthorization>(_output).Build();
            var serviceBuilder = host.Services.GetRequiredService<IServiceBuilder>();
            serviceBuilder.ConfigureAllServiceHostBase(serviceHostBase =>
            {
                var customPolicy = new CustomAuthorizationPolicy(new Claim(ClaimTypes.Role, "CoreWCFGroupAdmin", Rights.Identity));
                serviceHostBase.Authorization.ExternalAuthorizationPolicies = new List<IAuthorizationPolicy> { customPolicy }.AsReadOnly();
            });

            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + FakeAuthRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoForAuthorizarionOneRole(testString);
                    Assert.Equal(testString, result);
                    var sade = Assert.Throws<SecurityAccessDeniedException>(() => channel.EchoForAuthorizarionNoRole(testString));
                    Assert.Contains("Access is denied", sade.Message);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }
    }

    public class StartupForAuthorization
    {
        public const string FakeAuthRelativePath = "/nettcp.svc/fake-auth";
        public const string NoSecurityRelativePath = "/nettcp.svc/security-none";
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.TestService>(options =>
                {
                    options.DebugBehavior.IncludeExceptionDetailInFaults = true;
                });
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(SecurityMode.None), FakeAuthRelativePath);
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(SecurityMode.None), NoSecurityRelativePath);
                Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                builder.ConfigureServiceHostBase<Services.TestService>(serviceHost);
            });
        }
        public void ChangeHostBehavior(ServiceHostBase host)
        {
            var srvCredentials = new CoreWCF.Description.ServiceCredentials();
            LdapSettings _ldapSettings = new LdapSettings("ldapserver.test.local", "test.local", "your_own_top_org");
            srvCredentials.WindowsAuthentication.LdapSetting = _ldapSettings;
            host.Description.Behaviors.Add(srvCredentials);
        }
    }
}
