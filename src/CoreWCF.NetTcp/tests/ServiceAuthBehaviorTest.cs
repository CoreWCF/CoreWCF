// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ServiceModel.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Primitives.Tests.CustomSecurity;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class ServiceAuthBehaviorTest
    {
        private readonly ITestOutputHelper _output;

        public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
        public const string NoSecurityRelativePath = "/nettcp.svc/security-none";

        public ServiceAuthBehaviorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> GetSimpleNetTcpClientConnectionWindowsAuthTestVariations()
        {
            yield return new object[] { typeof(Startup<MySyncTestServiceAuthorizationManager>) };
            yield return new object[] { typeof(Startup<MyAsyncTestServiceAuthorizationManager>) };
        }

        [WindowsOnlyTheory]
        [MemberData(nameof(GetSimpleNetTcpClientConnectionWindowsAuthTestVariations))]
        public void SimpleNetTcpClientConnectionWindowsAuth(Type startupType)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding =
                        ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() +
                                                                WindowsAuthRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [WindowsOnlyFact]
        public void SimpleNetTcpClientConnectionUseWindowsGroups()
        {
            string testString = "a" + PrincipalPermissionMode.UseWindowsGroups + "test";
            IHost host = ServiceHelper.CreateWebHostBuilder<PermissionUseWindowsGroup>(_output).Build();
            AssertForCommon(testString, host);
        }

        [WindowsOnlyFact]
        public void SimpleNetTcpClientConnectionUseAlways()
        {
            string testString = "a" + PrincipalPermissionMode.Always + "test";
            IHost host = ServiceHelper.CreateWebHostBuilder<PermissionUseAlways>(_output).Build();
            AssertForCommon(testString, host);
        }

        [WindowsOnlyFact]
        public void SimpleNetTcpClientConnectionUseNone()
        {
            string testString = "a" + PrincipalPermissionMode.None + "test";
            IHost host = ServiceHelper.CreateWebHostBuilder<PermissionUseNone>(_output).Build();
            AssertForCommon(testString, host);
        }

        [WindowsOnlyFact]
        public void SimpleNetTcpClientImpersonateUser()
        {
            string sourceString = "test";
            IHost host = ServiceHelper.CreateWebHostBuilder<ImpersonateCallerForAll>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding =
                        ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() +
                                                                WindowsAuthRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoForImpersonation(sourceString);
                    Assert.Equal(sourceString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        private void AssertForCommon(string sourceString, IHost host)
        {
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding =
                        ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() +
                                                                WindowsAuthRelativePath));
                    channel = factory.CreateChannel();

                    // ReSharper disable once SuspiciousTypeConversion.Global
                    ((IChannel)channel).Open();
                    string result = channel.EchoForPermission(sourceString);
                    Assert.Equal(sourceString, result);

                    //
                    // These were explicitly removed because the ServiceHelper already cleans these up, and
                    // in some cases not using proper disposal handling will result in:
                    // 'System.IO.IOException : Received an unexpected EOF or 0 bytes from the transport stream.'
                    // on the build server, causing false test failures.
                    //
                    // ((IChannel)channel).Close();
                    // factory.Close();
                }
                finally
                {
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }

        }

        public class PermissionUseAlways : StartUpPermissionBase
        {
            public PermissionUseAlways() : base(PrincipalPermissionMode.Always)
            {

            }
        }

        public class PermissionUseWindowsGroup : StartUpPermissionBase
        {
            public PermissionUseWindowsGroup() : base(PrincipalPermissionMode.UseWindowsGroups)
            {
            }

        }

        public class PermissionUseNone : StartUpPermissionBase
        {
            public PermissionUseNone() : base(PrincipalPermissionMode.None)
            {
            }

        }

        public class ImpersonateCallerForAll : StartUpPermissionBase
        {
            public ImpersonateCallerForAll() : base(PrincipalPermissionMode.UseWindowsGroups, true)
            {
            }

        }


        public class StartUpPermissionBase
        {
            private readonly PrincipalPermissionMode principalMode;
            private readonly bool isImpersonate = false;

            public StartUpPermissionBase(PrincipalPermissionMode modeToTest, bool isImmpersonation = false)
            {
                principalMode = modeToTest;
                isImpersonate = isImmpersonation;
            }

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                ServiceAuthorizationBehavior authBehavior =
                    app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
                authBehavior.PrincipalPermissionMode = principalMode;
                if (isImpersonate)
                {
                    authBehavior.ImpersonateCallerForAllOperations = true;
                }

                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(), WindowsAuthRelativePath);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.None), NoSecurityRelativePath);
                });
            }


        }

        public class Startup<TServiceAuthorizationManager>
            where TServiceAuthorizationManager : ServiceAuthorizationManager, new()
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<ServiceAuthorizationManager, TServiceAuthorizationManager>();
            }

            public void Configure(IApplicationBuilder app)
            {
                ServiceAuthorizationBehavior authBehavior =
                    app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
                var authPolicies = new List<IAuthorizationPolicy> { new MyTestAuthorizationPolicy() };
                var externalAuthPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(authPolicies);
                authBehavior.ExternalAuthorizationPolicies = externalAuthPolicies;
                authBehavior.ServiceAuthorizationManager = new TServiceAuthorizationManager();
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(), WindowsAuthRelativePath);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.None), NoSecurityRelativePath);
                });
            }
        }
    }
}
