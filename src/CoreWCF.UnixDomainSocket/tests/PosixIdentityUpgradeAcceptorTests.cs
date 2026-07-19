// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.UnixDomainSocket.Tests
{
    public class PosixIdentityUpgradeAcceptorTests
    {
        // The framing pipeline checks GetRemoteSecurity() == null to decide whether
        // a stream upgrade actually negotiated a remote identity. The acceptor for
        // PosixIdentity must therefore follow the same contract documented on
        // StreamSecurityUpgradeAcceptor.GetRemoteSecurity ("works after call to
        // AcceptUpgrade") and used by the shared StreamSecurityUpgradeAcceptorBase:
        // remote security is only available once AcceptUpgradeAsync has completed.
        [Fact]
        public async Task GetRemoteSecurity_ReturnsNull_BeforeAcceptUpgradeAsync()
        {
            var bindingElement = new UnixPosixIdentityBindingElement();
            var transport = new UnixDomainSocketTransportBindingElement();
            var encoding = new BinaryMessageEncodingBindingElement();

            var customBinding = new CustomBinding(encoding, bindingElement, transport);
            Assert.Equal("net.uds", customBinding.Scheme);

            var context = new BindingContext(customBinding, new BindingParameterCollection());

            StreamUpgradeProvider provider = bindingElement.BuildServerStreamUpgradeProvider(context);
            Assert.NotNull(provider);

            await provider.OpenAsync();
            try
            {
                StreamUpgradeAcceptor acceptor = provider.CreateUpgradeAcceptor();
                var securityAcceptor = Assert.IsAssignableFrom<StreamSecurityUpgradeAcceptor>(acceptor);

                Assert.Null(securityAcceptor.GetRemoteSecurity());
            }
            finally
            {
                await provider.CloseAsync();
            }
        }
    }
}

