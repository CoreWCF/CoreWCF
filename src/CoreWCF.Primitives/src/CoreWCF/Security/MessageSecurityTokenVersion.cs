// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.Security
{
    internal sealed class MessageSecurityTokenVersion : SecurityTokenVersion
    {
        private readonly string _toString;
        private readonly ReadOnlyCollection<string> _supportedSpecs;
        private const string bsp10ns = @"http://ws-i.org/profiles/basic-security/core/1.0";
        private static readonly MessageSecurityTokenVersion s_wss10bsp10 = new MessageSecurityTokenVersion(
            SecurityVersion.WSSecurity10,
            TrustVersion.WSTrustFeb2005,
            SecureConversationVersion.WSSecureConversationFeb2005,
            "WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10",
            true,
            XD.SecurityJan2004Dictionary.Namespace.Value,
            XD.TrustFeb2005Dictionary.Namespace.Value,
            XD.SecureConversationFeb2005Dictionary.Namespace.Value,
            bsp10ns);
        private static readonly MessageSecurityTokenVersion s_wss11bsp10 = new MessageSecurityTokenVersion(
            SecurityVersion.WSSecurity11,
            TrustVersion.WSTrustFeb2005,
            SecureConversationVersion.WSSecureConversationFeb2005,
            "WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10",
            true,
            XD.SecurityXXX2005Dictionary.Namespace.Value,
            XD.TrustFeb2005Dictionary.Namespace.Value,
            XD.SecureConversationFeb2005Dictionary.Namespace.Value,
            bsp10ns);
        private static readonly MessageSecurityTokenVersion s_wss10oasisdec2005bsp10 = new MessageSecurityTokenVersion(
            SecurityVersion.WSSecurity10,
            TrustVersion.WSTrust13,
            SecureConversationVersion.WSSecureConversation13,
            "WSSecurity10WSTrust13WSSecureConversation13BasicSecurityProfile10",
            true,
            XD.SecurityXXX2005Dictionary.Namespace.Value,
            DXD.TrustDec2005Dictionary.Namespace.Value,
            DXD.SecureConversationDec2005Dictionary.Namespace.Value
            );
        private static readonly MessageSecurityTokenVersion s_wss11oasisdec2005 = new MessageSecurityTokenVersion(
            SecurityVersion.WSSecurity11,
            TrustVersion.WSTrust13,
            SecureConversationVersion.WSSecureConversation13,
            "WSSecurity11WSTrust13WSSecureConversation13",
            false,
            XD.SecurityJan2004Dictionary.Namespace.Value,
            DXD.TrustDec2005Dictionary.Namespace.Value,
            DXD.SecureConversationDec2005Dictionary.Namespace.Value
            );
        private static readonly MessageSecurityTokenVersion s_wss11oasisdec2005bsp10 = new MessageSecurityTokenVersion(
            SecurityVersion.WSSecurity11,
            TrustVersion.WSTrust13,
            SecureConversationVersion.WSSecureConversation13,
            "WSSecurity11WSTrust13WSSecureConversation13BasicSecurityProfile10",
            true,
            XD.SecurityXXX2005Dictionary.Namespace.Value,
            DXD.TrustDec2005Dictionary.Namespace.Value,
            DXD.SecureConversationDec2005Dictionary.Namespace.Value
            );

        public static MessageSecurityTokenVersion WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005 { get; } = new MessageSecurityTokenVersion(
            SecurityVersion.WSSecurity11,
            TrustVersion.WSTrustFeb2005,
            SecureConversationVersion.WSSecureConversationFeb2005,
            "WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005",
            false,
            XD.SecurityXXX2005Dictionary.Namespace.Value,
            XD.TrustFeb2005Dictionary.Namespace.Value,
            XD.SecureConversationFeb2005Dictionary.Namespace.Value);

        public static MessageSecurityTokenVersion WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10
        {
            get
            {
                return s_wss11bsp10;
            }
        }

        public static MessageSecurityTokenVersion WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10
        {
            get
            {
                return s_wss10bsp10;
            }
        }

        public static MessageSecurityTokenVersion WSSecurity10WSTrust13WSSecureConversation13BasicSecurityProfile10
        {
            get
            {
                return s_wss10oasisdec2005bsp10;
            }
        }

        public static MessageSecurityTokenVersion WSSecurity11WSTrust13WSSecureConversation13
        {
            get
            {
                return s_wss11oasisdec2005;
            }
        }

        public static MessageSecurityTokenVersion WSSecurity11WSTrust13WSSecureConversation13BasicSecurityProfile10
        {
            get
            {
                return s_wss11oasisdec2005bsp10;
            }
        }

        public static MessageSecurityTokenVersion GetSecurityTokenVersion(SecurityVersion version, bool emitBspAttributes)
        {
            if (version == SecurityVersion.WSSecurity10)
            {
                if (emitBspAttributes)
                {
                    return MessageSecurityTokenVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                }
            }
            else if (version == SecurityVersion.WSSecurity11)
            {
                if (emitBspAttributes)
                {
                    return MessageSecurityTokenVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005BasicSecurityProfile10;
                }
                else
                {
                    return MessageSecurityTokenVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005;
                }
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }

        private MessageSecurityTokenVersion(SecurityVersion securityVersion, TrustVersion trustVersion, SecureConversationVersion secureConversationVersion, string toString, bool emitBspRequiredAttributes, params string[] supportedSpecs)
            : base()
        {
            EmitBspRequiredAttributes = emitBspRequiredAttributes;
            _supportedSpecs = new ReadOnlyCollection<string>(supportedSpecs);
            _toString = toString;
            SecurityVersion = securityVersion;
            TrustVersion = trustVersion;
            SecureConversationVersion = secureConversationVersion;
        }

        public bool EmitBspRequiredAttributes { get; }

        public SecurityVersion SecurityVersion { get; }

        public TrustVersion TrustVersion { get; }

        public SecureConversationVersion SecureConversationVersion { get; }

        public override ReadOnlyCollection<string> GetSecuritySpecifications()
        {
            return _supportedSpecs;
        }

        public override string ToString()
        {
            return _toString;
        }
    }
}
