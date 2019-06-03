using CoreWCF.Channels;

namespace CoreWCF
{
    internal class DummyTransportBindingElement : TransportBindingElement
    {
        public DummyTransportBindingElement()
        {
        }

        public override BindingElement Clone()
        {
            return this;
        }

        public override string Scheme { get { return "dummy"; } }
    }
}