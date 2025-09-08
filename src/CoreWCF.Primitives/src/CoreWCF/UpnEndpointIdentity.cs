// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Principal;
using System.Xml;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public class UpnEndpointIdentity : EndpointIdentity
    {
        private SecurityIdentifier _upnSid;
        private bool _hasUpnSidBeenComputed;
        private readonly object _thisLock = new object();

        public UpnEndpointIdentity(string upnName)
        {
            if (upnName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(upnName));
            }

            Initialize(Claim.CreateUpnClaim(upnName));
            _hasUpnSidBeenComputed = false;
        }

        public UpnEndpointIdentity(Claim identity)
        {
            if (identity == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identity));
            }

            if (!identity.ClaimType.Equals(ClaimTypes.Upn))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.UnrecognizedClaimTypeForIdentity, identity.ClaimType, ClaimTypes.Upn));
            }

            Initialize(identity);
        }

        internal override void WriteContentsTo(XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            writer.WriteElementString(XD.AddressingDictionary.Upn, XD.AddressingDictionary.IdentityExtensionNamespace, (string)IdentityClaim.Resource);
        }

        internal SecurityIdentifier GetUpnSid()
        {
            Fx.Assert(ClaimTypes.Upn.Equals(IdentityClaim.ClaimType), "");
            if (!_hasUpnSidBeenComputed)
            {
                lock (_thisLock)
                {
                    string upn = (string)IdentityClaim.Resource;
                    if (!_hasUpnSidBeenComputed)
                    {
                        try
                        {
                            var userAccount = new NTAccount(upn);
                            _upnSid = userAccount.Translate(typeof(SecurityIdentifier)) as SecurityIdentifier;
                        }
                        catch (Exception e)
                        {
                            // Always immediately rethrow fatal exceptions.
                            if (Fx.IsFatal(e))
                            {
                                throw;
                            }

                            if (e is NullReferenceException)
                            {
                                throw;
                            }

                            //SecurityTraceRecordHelper.TraceSpnToSidMappingFailure(upn, e);
                        }
                        finally
                        {
                            _hasUpnSidBeenComputed = true;
                        }
                    }
                }
            }
            return _upnSid;
        }
    }
}
