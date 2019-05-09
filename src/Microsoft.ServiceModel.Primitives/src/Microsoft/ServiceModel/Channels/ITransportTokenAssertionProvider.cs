using System.Xml;

namespace Microsoft.ServiceModel.Channels
{
    internal interface ITransportTokenAssertionProvider
    {
        XmlElement GetTransportTokenAssertion();
    }
}