using CoreWCF;
using System.Xml;

namespace CoreWCF.IdentityModel.Tokens
{
    public class GenericXmlSecurityKeyIdentifierClause : SecurityKeyIdentifierClause
    {
        public GenericXmlSecurityKeyIdentifierClause(XmlElement referenceXml)
            : this(referenceXml, null, 0)
        {
        }

        public GenericXmlSecurityKeyIdentifierClause(XmlElement referenceXml, byte[] derivationNonce, int derivationLength)
            : base(null, derivationNonce, derivationLength)
        {
            ReferenceXml = referenceXml ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(referenceXml));
        }

        public XmlElement ReferenceXml { get; }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            GenericXmlSecurityKeyIdentifierClause that = keyIdentifierClause as GenericXmlSecurityKeyIdentifierClause;
            return ReferenceEquals(this, that) || (that != null && that.Matches(ReferenceXml));
        }

        private bool Matches(XmlElement xmlElement)
        {
            if (xmlElement == null)
            {
                return false;
            }

            return CompareNodes(ReferenceXml, xmlElement);
        }

        private bool CompareNodes(XmlNode originalNode, XmlNode newNode)
        {
            if (originalNode.OuterXml == newNode.OuterXml)
            {
                return true;
            }

            if (originalNode.LocalName != newNode.LocalName || originalNode.InnerText != newNode.InnerText)
            {
                return false;
            }

            if (originalNode.InnerXml == newNode.InnerXml)
            {
                return true;
            }

            if (originalNode.HasChildNodes)
            {
                if (!newNode.HasChildNodes || originalNode.ChildNodes.Count != newNode.ChildNodes.Count)
                {
                    return false;
                }

                bool childrenStatus = true;
                for (int i = 0; i < originalNode.ChildNodes.Count; i++)
                {
                    childrenStatus = childrenStatus & CompareNodes(originalNode.ChildNodes[i], newNode.ChildNodes[i]);
                }

                return childrenStatus;
            }
            else if (newNode.HasChildNodes)
            {
                return false;
            }

            return true;
        }
    }
}
