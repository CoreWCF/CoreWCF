// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Security
{
    class WSTrustDec2005 : WSTrustFeb2005
    {
        public WSTrustDec2005(WSSecurityTokenSerializer tokenSerializer)
            : base(tokenSerializer)
        {
        }

        public override TrustDictionary SerializerDictionary
        {
            get { return DXD.TrustDec2005Dictionary; }
        }

        public class DriverDec2005 : DriverFeb2005
        {
            public DriverDec2005(SecurityStandardsManager standardsManager)
                : base(standardsManager)
            {
            }

            //public override TrustDictionary DriverDictionary
            //{
            //    get
            //    {
            //        return DXD.TrustDec2005Dictionary;
            //    }
            //}

            //public override XmlDictionaryString RequestSecurityTokenResponseFinalAction
            //{
            //    get
            //    {
            //        return DXD.TrustDec2005Dictionary.RequestSecurityTokenCollectionIssuanceFinalResponse;
            //    }
            //}

            internal virtual bool IsSecondaryParametersElement(XmlElement element)
            {
                return ((element.LocalName == DXD.TrustDec2005Dictionary.SecondaryParameters.Value) &&
                        (element.NamespaceURI == DXD.TrustDec2005Dictionary.Namespace.Value));
            }

            public virtual XmlElement CreateKeyWrapAlgorithmElement(string keyWrapAlgorithm)
            {
                if (keyWrapAlgorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyWrapAlgorithm));
                }

                XmlDocument doc = new XmlDocument();
                XmlElement result = doc.CreateElement(DXD.TrustDec2005Dictionary.Prefix.Value, DXD.TrustDec2005Dictionary.KeyWrapAlgorithm.Value,
                    DXD.TrustDec2005Dictionary.Namespace.Value);
                result.AppendChild(doc.CreateTextNode(keyWrapAlgorithm));
                return result;
            }
        }
    }
}
