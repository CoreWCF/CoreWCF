using System;
using System.IO;
using System.ServiceModel;
using System.Text;
using CoreWCF.Channels;
using Xunit;
using NetTcpBinding = CoreWCF.NetTcpBinding;
using System.Diagnostics;
using System.Threading;

public static class BasicServiceTest
{
    //[Fact]
    //public static void NetTcpRequestReplyEchoString()
    //{
    //    string httpListeningUrl = "http://localhost:18080"; // Dummy for now until we can run without HTTP
    //    string netTcplisteningUrl = "net.tcp://localhost:11808";
    //    string testString = new string('a', 3000);
    //    var host = ServiceHelper.CreateWebHost<Startup>(httpListeningUrl);

    //    using (host)
    //    {
    //        host.Start();
    //        var netTcpBinding = new System.ServiceModel.NetTcpBinding();
    //        netTcpBinding.Security.Mode = SecurityMode.None;
    //        var factory = new ChannelFactory<ClientContract.IEchoService>(netTcpBinding,
    //            new EndpointAddress(new Uri(netTcplisteningUrl + "/BasicWcfService/nettcp.svc")));
    //        var channel = factory.CreateChannel();
    //        System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
    //        ichannel.Open();
    //        var result = channel.EchoString(testString);
    //        Assert.Equal(testString, result);
    //        ichannel.Close();
    //    }
    //}

    //[Fact]
    //public static void NetTcpRequestReplyEchoStringAsync()
    //{
    //    string httpListeningUrl = "http://localhost:18080"; // Dummy for now until we can run without HTTP
    //    string netTcplisteningUrl = "net.tcp://localhost:11808";
    //    string testString = new string('a', 3000);
    //    var host = ServiceHelper.CreateWebHost<Startup>(httpListeningUrl);

    //    using (host)
    //    {
    //        host.Start();
    //        var netTcpBinding = new System.ServiceModel.NetTcpBinding();
    //        netTcpBinding.Security.Mode = SecurityMode.None;
    //        var factory = new ChannelFactory<ClientContract.IEchoService>(netTcpBinding,
    //            new EndpointAddress(new Uri(netTcplisteningUrl + "/BasicWcfService/nettcp.svc")));
    //        var channel = factory.CreateChannel();
    //        System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
    //        ichannel.Open();
    //        var result = channel.EchoStringAsync(testString);
    //        Assert.Equal(testString, result);
    //    }
    //}

    //[Fact]
    //public static void NetTcpRequestReplyEchoStream()
    //{
    //    string httpListeningUrl = "http://localhost:18080"; // Dummy for now until we can run without HTTP
    //    string netTcplisteningUrl = "net.tcp://localhost:11808";
    //    string testString = new string('a', 3000);
    //    MemoryStream testStream = new MemoryStream(Encoding.UTF8.GetBytes(testString));
    //    var host = ServiceHelper.CreateWebHost<Startup>(httpListeningUrl);

    //    using (host)
    //    {
    //        host.Start();
    //        var netTcpBinding = new System.ServiceModel.NetTcpBinding();
    //        netTcpBinding.Security.Mode = SecurityMode.None;
    //        var factory = new ChannelFactory<ClientContract.IEchoService>(netTcpBinding,
    //            new EndpointAddress(new Uri(netTcplisteningUrl + "/BasicWcfService/nettcp.svc")));
    //        var channel = factory.CreateChannel();
    //        System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
    //        ichannel.Open();
    //        var result = channel.EchoStream(testStream);
    //        testStream.SetLength(0);
    //        result.CopyTo(testStream);
    //        var resultString = Encoding.UTF8.GetString(testStream.ToArray());
    //        Assert.Equal(testString, resultString);
    //    }
    //}

    //[Fact]
    //public static void NetTcpRequestReplyEchoStreamAsync()
    //{
    //    string httpListeningUrl = "http://localhost:18080"; // Dummy for now until we can run without HTTP
    //    string netTcplisteningUrl = "net.tcp://localhost:11808";
    //    string testString = new string('a', 3000);
    //    MemoryStream testStream = new MemoryStream(Encoding.UTF8.GetBytes(testString));
    //    var host = ServiceHelper.CreateWebHost<Startup>(httpListeningUrl);

    //    using (host)
    //    {
    //        host.Start();
    //        var netTcpBinding = new System.ServiceModel.NetTcpBinding();
    //        netTcpBinding.Security.Mode = SecurityMode.None;
    //        var factory = new ChannelFactory<ClientContract.IEchoService>(netTcpBinding,
    //            new EndpointAddress(new Uri(netTcplisteningUrl + "/BasicWcfService/nettcp.svc")));
    //        var channel = factory.CreateChannel();
    //        System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
    //        ichannel.Open();
    //        var result = channel.EchoStreamAsync(testStream);
    //        testStream.SetLength(0);
    //        result.CopyTo(testStream);
    //        var resultString = Encoding.UTF8.GetString(testStream.ToArray());
    //        Assert.Equal(testString, resultString);
    //    }
    //}

    //public class Startup : BaseStartup<Services.EchoService, ServiceContract.IEchoService>
    //{
    //    public override string ServiceBaseAddress => "/BasicWcfService";
    //    public override Binding Binding => new NetTcpBinding
    //                { Security = new CoreWCF.NetTcpSecurity
    //                    { Mode = CoreWCF.SecurityMode.None }
    //                };
    //    public override string RelativeEndpointAddress => "nettcp.svc";
    //}
}
