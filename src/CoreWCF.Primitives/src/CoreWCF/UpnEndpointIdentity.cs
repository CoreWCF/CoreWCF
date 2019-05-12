using CoreWCF.IdentityModel.Claims;
using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace CoreWCF
{
    public class UpnEndpointIdentity : EndpointIdentity
    {
        SecurityIdentifier _upnSid;
        bool _hasUpnSidBeenComputed;
        WindowsIdentity _windowsIdentity;

        object _thisLock = new object();

        public UpnEndpointIdentity(string upnName)
        {
            if (upnName == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(upnName));

            Initialize(Claim.CreateUpnClaim(upnName));
            _hasUpnSidBeenComputed = false;
        }

        public UpnEndpointIdentity(Claim identity)
        {
            if (identity == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identity));

            if (!identity.ClaimType.Equals(ClaimTypes.Upn))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.UnrecognizedClaimTypeForIdentity, identity.ClaimType, ClaimTypes.Upn));

            Initialize(identity);
        }

        internal UpnEndpointIdentity(WindowsIdentity windowsIdentity)
        {
            _windowsIdentity = windowsIdentity ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(windowsIdentity));
            _upnSid = windowsIdentity.User;
            _hasUpnSidBeenComputed = true;
        }

        internal override void EnsureIdentityClaim()
        {
            if (_windowsIdentity != null)
            {
                lock (_thisLock)
                {
                    if (_windowsIdentity != null)
                    {
                        Initialize(Claim.CreateUpnClaim(GetUpnFromWindowsIdentity(_windowsIdentity)));
                        _windowsIdentity.Dispose();
                        _windowsIdentity = null;
                    }
                }
            }
        }

        string GetUpnFromWindowsIdentity(WindowsIdentity windowsIdentity)
        {
            string downlevelName = null;
            string upnName = null;

            try
            {
                downlevelName = windowsIdentity.Name;

                if (IsMachineJoinedToDomain())
                {
                    upnName = GetUpnFromDownlevelName(downlevelName);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
            }

            // if the AD cannot be queried for the fully qualified domain name,
            // fall back to the downlevel UPN name
            return upnName ?? downlevelName;
        }

        bool IsMachineJoinedToDomain()
        {
            throw new PlatformNotSupportedException();
        }

        string GetUpnFromDownlevelName(string downlevelName)
        {
            throw new PlatformNotSupportedException();
        }

        internal override void WriteContentsTo(XmlDictionaryWriter writer)
        {
            if (writer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));

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
