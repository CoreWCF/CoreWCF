using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Primitives.Tests.CustomSecurity;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
   public class ServiceAuthBehaviorTest
    {
        private const string NetTcpServiceBaseUri = "net.tcp://localhost:8808";
        private const string WindowsAuthNetTcpServiceUri = NetTcpServiceBaseUri + Startup.WindowsAuthRelativePath;
        private ITestOutputHelper _output;

        public ServiceAuthBehaviorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "WindowsOnly")]
        public void SimpleNetTcpClientConnectionWindowsAuth()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(WindowsAuthNetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var result = channel.EchoString(testString);
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

        [Fact]
        [Trait("Category", "WindowsOnly")]
        public void SimpleNetTcpClientConnectionUseWindowsGroups()
        {
            string testString ="a"+PrincipalPermissionMode.UseWindowsGroups.ToString()+"test";
            var host = ServiceHelper.CreateWebHostBuilder<PermissionUseWindowsGroup>(_output).Build();
            assertForCommon(testString, host);
        }
        [Fact]
        [Trait("Category", "WindowsOnly")]
        public void SimpleNetTcpClientConnectionUseAlways()
        {
            string testString = "a" + PrincipalPermissionMode.Always.ToString() + "test";
            var host = ServiceHelper.CreateWebHostBuilder<PermissionUseAlways>(_output).Build();
            assertForCommon(testString, host);
        }

        [Fact]
        [Trait("Category", "WindowsOnly")]
        public void SimpleNetTcpClientConnectionUseNone()
        {
            string testString = "a" + PrincipalPermissionMode.None.ToString() + "test";
            var host = ServiceHelper.CreateWebHostBuilder<PermissionUseNone>(_output).Build();
            assertForCommon(testString, host);
        }

        [Fact]
        [Trait("Category", "WindowsOnly")]
        public void SimpleNetTcpClientImpersonateUser()
        {
            string sourceString = "test";
            var host = ServiceHelper.CreateWebHostBuilder<ImpersonateCallerForAll>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(WindowsAuthNetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var result = channel.EchoForImpersonation(sourceString);
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

        private void assertForCommon(String sourceString,  IWebHost host)
        {
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(WindowsAuthNetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var result = channel.EchoForPermission(sourceString);
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
        public ImpersonateCallerForAll() : base(PrincipalPermissionMode.UseWindowsGroups,true)
        {
        }

    }


    public class StartUpPermissionBase
    {
        PrincipalPermissionMode principalMode;
        bool isImpersonate = false;

        public StartUpPermissionBase(PrincipalPermissionMode modeToTest, bool isImmpersonation = false)
        {
            this.principalMode = modeToTest;
            this.isImpersonate = isImmpersonation;
        }

        public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
        public const string NoSecurityRelativePath = "/nettcp.svc/security-none";
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            var authBehavior = app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
             authBehavior.PrincipalPermissionMode = principalMode;
            if (isImpersonate)
                authBehavior.ImpersonateCallerForAllOperations = true;
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.TestService>();
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(), WindowsAuthRelativePath);
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None), NoSecurityRelativePath);
            });
        }
    }

        public class Startup
        {
            public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
            public const string NoSecurityRelativePath = "/nettcp.svc/security-none";
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<ServiceAuthorizationManager, MyTestServiceAuthorizationManager>();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                var authBehavior = app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
                var authPolicies = new List<IAuthorizationPolicy>();
                authPolicies.Add(new MyTestAuthorizationPolicy());
                var externalAuthPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(authPolicies);
                authBehavior.ExternalAuthorizationPolicies = externalAuthPolicies;
                authBehavior.ServiceAuthorizationManager = new MyTestServiceAuthorizationManager();
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(), WindowsAuthRelativePath);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None), NoSecurityRelativePath);
                });
            }
        }
    }

