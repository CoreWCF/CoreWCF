using System.Xml;

namespace CoreWCF.Channels
{
    internal interface ITransportTokenAssertionProvider
    {
        XmlElement GetTransportTokenAssertion();
    }
}