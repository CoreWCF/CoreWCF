// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class WSTrustFeb2005 : WSTrust
    {
        public WSTrustFeb2005(WSSecurityTokenSerializer tokenSerializer)
            : base(tokenSerializer)
        {
        }

        public override TrustDictionary SerializerDictionary
        {
            get { return XD.TrustFeb2005Dictionary; }
        }

        public class DriverFeb2005 : Driver
        {
            public DriverFeb2005(SecurityStandardsManager standardsManager)
                : base(standardsManager)
            {
            }

            public override TrustDictionary DriverDictionary
            {
                get
                {
                    return XD.TrustFeb2005Dictionary;
                }
            }

            public override XmlDictionaryString RequestSecurityTokenResponseFinalAction
            {
                get
                {
                    return XD.TrustFeb2005Dictionary.RequestSecurityTokenIssuanceResponse;
                }
            }

            public override bool IsSessionSupported
            {
                get
                {
                    return true;
                }
            }

            public override bool IsIssuedTokensSupported
            {
                get
                {
                    return true;
                }
            }

            public override string IssuedTokensHeaderName
            {
                get
                {
                    return DriverDictionary.IssuedTokensHeader.Value;
                }
            }

            public override string IssuedTokensHeaderNamespace
            {
                get
                {
                    return DriverDictionary.Namespace.Value;
                }
            }

            public override string RequestTypeRenew
            {
                get
                {
                    return DriverDictionary.RequestTypeRenew.Value;
                }
            }

            public override string RequestTypeClose
            {
                get
                {
                    return DriverDictionary.RequestTypeClose.Value;
                }
            }

            public override Collection<XmlElement> ProcessUnknownRequestParameters(Collection<XmlElement> unknownRequestParameters, Collection<XmlElement> originalRequestParameters)
            {
                throw new NotImplementedException();
            }

            protected override void ReadReferences(XmlElement rstrXml, out SecurityKeyIdentifierClause requestedAttachedReference,
                    out SecurityKeyIdentifierClause requestedUnattachedReference)
            {
                XmlElement issuedTokenXml = null;
                requestedAttachedReference = null;
                requestedUnattachedReference = null;
                for (int i = 0; i < rstrXml.ChildNodes.Count; ++i)
                {
                    XmlElement child = rstrXml.ChildNodes[i] as XmlElement;
                    if (child != null)
                    {
                        if (child.LocalName == DriverDictionary.RequestedSecurityToken.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            issuedTokenXml = XmlHelper.GetChildElement(child);
                        }
                        else if (child.LocalName == DriverDictionary.RequestedAttachedReference.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            requestedAttachedReference = GetKeyIdentifierXmlReferenceClause(XmlHelper.GetChildElement(child));
                        }
                        else if (child.LocalName == DriverDictionary.RequestedUnattachedReference.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            requestedUnattachedReference = GetKeyIdentifierXmlReferenceClause(XmlHelper.GetChildElement(child));
                        }
                    }
                }

                try
                {
                    if (issuedTokenXml != null)
                    {
                        if (requestedAttachedReference == null)
                        {
                            StandardsManager.TryCreateKeyIdentifierClauseFromTokenXml(issuedTokenXml, SecurityTokenReferenceStyle.Internal, out requestedAttachedReference);
                        }
                        if (requestedUnattachedReference == null)
                        {
                            StandardsManager.TryCreateKeyIdentifierClauseFromTokenXml(issuedTokenXml, SecurityTokenReferenceStyle.External, out requestedUnattachedReference);
                        }
                    }
                }
                catch (XmlException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.TrustDriverIsUnableToCreatedNecessaryAttachedOrUnattachedReferences, issuedTokenXml.ToString())));
                }
            }

            protected override bool ReadRequestedTokenClosed(XmlElement rstrXml)
            {
                for (int i = 0; i < rstrXml.ChildNodes.Count; ++i)
                {
                    XmlElement child = (rstrXml.ChildNodes[i] as XmlElement);
                    if (child != null)
                    {
                        if (child.LocalName == DriverDictionary.RequestedTokenClosed.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            protected override void ReadTargets(XmlElement rstXml, out SecurityKeyIdentifierClause renewTarget, out SecurityKeyIdentifierClause closeTarget)
            {
                renewTarget = null;
                closeTarget = null;

                for (int i = 0; i < rstXml.ChildNodes.Count; ++i)
                {
                    XmlElement child = (rstXml.ChildNodes[i] as XmlElement);
                    if (child != null)
                    {
                        if (child.LocalName == DriverDictionary.RenewTarget.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            renewTarget = StandardsManager.SecurityTokenSerializer.ReadKeyIdentifierClause(new XmlNodeReader(child.FirstChild));
                        }
                        else if (child.LocalName == DriverDictionary.CloseTarget.Value && child.NamespaceURI == DriverDictionary.Namespace.Value)
                        {
                            closeTarget = StandardsManager.SecurityTokenSerializer.ReadKeyIdentifierClause(new XmlNodeReader(child.FirstChild));
                        }
                    }
                }
            }

            protected override void WriteReferences(RequestSecurityTokenResponse rstr, XmlDictionaryWriter writer)
            {
                if (rstr.RequestedAttachedReference != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestedAttachedReference, DriverDictionary.Namespace);
                    StandardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, rstr.RequestedAttachedReference);
                    writer.WriteEndElement();
                }

                if (rstr.RequestedUnattachedReference != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RequestedUnattachedReference, DriverDictionary.Namespace);
                    StandardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, rstr.RequestedUnattachedReference);
                    writer.WriteEndElement();
                }
            }

            protected override void WriteRequestedTokenClosed(RequestSecurityTokenResponse rstr, XmlDictionaryWriter writer)
            {
                if (rstr.IsRequestedTokenClosed)
                {
                    writer.WriteElementString(DriverDictionary.RequestedTokenClosed, DriverDictionary.Namespace, String.Empty);
                }
            }

            protected override void WriteTargets(RequestSecurityToken rst, XmlDictionaryWriter writer)
            {
                if (rst.RenewTarget != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.RenewTarget, DriverDictionary.Namespace);
                    StandardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, rst.RenewTarget);
                    writer.WriteEndElement();
                }

                if (rst.CloseTarget != null)
                {
                    writer.WriteStartElement(DriverDictionary.Prefix.Value, DriverDictionary.CloseTarget, DriverDictionary.Namespace);
                    StandardsManager.SecurityTokenSerializer.WriteKeyIdentifierClause(writer, rst.CloseTarget);
                    writer.WriteEndElement();
                }
            }
        }
    }
}
