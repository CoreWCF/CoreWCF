// // Licensed to the .NET Foundation under one or more agreements.
// // The .NET Foundation licenses this file to you under the MIT license.
//
// using System;
// using Helpers;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.AspNetCore.TestHost;
// using Xunit;
// using Xunit.Abstractions;
//
// namespace CoreWCF.Http.Tests.Authorization;
//
// public class AuthorizationPoliciesSimpleTests
// {
//     private readonly ITestOutputHelper _output;
//
//     public AuthorizationPoliciesSimpleTests(ITestOutputHelper output)
//     {
//         _output = output;
//     }
//
//     [Fact]
//     public void BasicHttpRequestReplyEchoString()
//     {
//         string testString = new('a', 3000);
//         IWebHost host = ServiceHelper.CreateWebHostBuilder<AuthorizationStartup>(_output).Build();
//         host.GetTestServer().
//         using (host)
//         {
//             host.Start();
//             System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
//             var factory = new System.ServiceModel.ChannelFactory<AuthorizationStartup.ISecuredService>(httpBinding,
//                 new System.ServiceModel.EndpointAddress(
//                     new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
//             AuthorizationStartup.ISecuredService channel = factory.CreateChannel();
//             string result = channel.Default(testString);
//             Assert.Equal(testString, result);
//         }
//     }
// }
