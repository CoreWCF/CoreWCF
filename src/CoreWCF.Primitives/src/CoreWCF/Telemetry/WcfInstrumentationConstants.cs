// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Telemetry;

internal static class WcfInstrumentationConstants
{
    public const string RpcSystemTag = "rpc.system";
    public const string RpcServiceTag = "rpc.service";
    public const string RpcMethodTag = "rpc.method";
    public const string NetHostNameTag = "net.host.name";
    public const string NetHostPortTag = "net.host.port";
    public const string SoapMessageVersionTag = "soap.message_version";
    public const string SoapReplyActionTag = "soap.reply_action";
    public const string WcfChannelSchemeTag = "wcf.channel.scheme";
    public const string WcfChannelPathTag = "wcf.channel.path";

    public const string WcfSystemValue = "dotnet_wcf";
}
