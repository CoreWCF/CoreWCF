using System;
using Xunit;
using CoreWCF.Description;

namespace CoreWCF.Primitives.Tests
{
    public class ServiceDescriptionExceptionTests
    {
        private class NoDefaultCtorService
        {
            public NoDefaultCtorService(string param) { }
        }

        [Fact]
        public void CreateImplementation_ThrowsExceptionWithTypeName()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ServiceDescription.CreateImplementation<NoDefaultCtorService>());
            Assert.Contains("NoDefaultCtorService", ex.Message);
            Assert.Contains("CoreWCF.Primitives.Tests.ServiceDescriptionExceptionTests+NoDefaultCtorService", ex.Message);
        }
    }
}
