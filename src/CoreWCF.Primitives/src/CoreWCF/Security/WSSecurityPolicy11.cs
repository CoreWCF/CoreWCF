// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Description;

namespace CoreWCF.Security
{
    internal class WSSecurityPolicy11 : WSSecurityPolicy
    {
        public const string WsspNamespace = @"http://schemas.xmlsoap.org/ws/2005/07/securitypolicy";

        public override string WsspNamespaceUri
        {
            get { return WsspNamespace; }
        }

        public override bool IsSecurityVersionSupported(MessageSecurityVersion version)
        {
            return version == MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10 ||
                version == MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11 ||
                version == MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10;
        }

        public override TrustDriver TrustDriver
        {
            get
            {
                return new WSTrustFeb2005.DriverFeb2005(new SecurityStandardsManager(MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11, WSSecurityTokenSerializer.DefaultInstance));
            }
        }

        // WS-SecurityPolicy 11 should still use the mssp namespace for MustNotSendCancel
        public override XmlElement CreateWsspMustNotSendCancelAssertion(bool requireCancel)
        {
            if (!requireCancel)
            {
                XmlElement result = CreateMsspAssertion(MustNotSendCancelName);
                return result;
            }
            else
            {
                return null;
            }
        }

        public override XmlElement CreateWsspTrustAssertion(MetadataExporter exporter, SecurityKeyEntropyMode keyEntropyMode)
        {
            return CreateWsspTrustAssertion(Trust10Name, exporter, keyEntropyMode);
        }
    }
}
