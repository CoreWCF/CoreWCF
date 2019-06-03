using CoreWCF.Channels;

namespace Helpers
{
    internal class MockTransportBindingElement : TransportBindingElement
    {
        public override string Scheme => "foo";

        public override BindingElement Clone()
        {
            return this;
        }
    }
}
