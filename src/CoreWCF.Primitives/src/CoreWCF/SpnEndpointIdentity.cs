// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF
{
    public class SpnEndpointIdentity : EndpointIdentity
    {
        private static TimeSpan s_spnLookupTime = TimeSpan.FromMinutes(1);
        private SecurityIdentifier _spnSid;

        // Double-checked locking pattern requires volatile for read/write synchronization
        private bool _hasSpnSidBeenComputed;
        private readonly object _thisLock = new object();
        private static readonly object s_typeLock = new object();

        // Double-checked locking pattern requires volatile for read/write synchronization
        private static DirectoryEntry s_directoryEntry;

        public static TimeSpan SpnLookupTime
        {
            get
            {
                return s_spnLookupTime;
            }
            set
            {
                if (value.Ticks < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value.Ticks,
                                                    SRCommon.ValueMustBeNonNegative));
                }
                s_spnLookupTime = value;
            }
        }

        public SpnEndpointIdentity(string spnName)
        {
            if (spnName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(spnName));
            }

            Initialize(Claim.CreateSpnClaim(spnName));
        }

        public SpnEndpointIdentity(Claim identity)
        {
            if (identity == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identity));
            }

            if (!ClaimTypes.Spn.Equals(identity.ClaimType))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.UnrecognizedClaimTypeForIdentity, identity.ClaimType, ClaimTypes.Spn));
            }

            Initialize(identity);
        }

        internal override void WriteContentsTo(XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            writer.WriteElementString(XD.AddressingDictionary.Spn, XD.AddressingDictionary.IdentityExtensionNamespace, (string)IdentityClaim.Resource);
        }

        internal SecurityIdentifier GetSpnSid()
        {
            Fx.Assert(ClaimTypes.Spn.Equals(IdentityClaim.ClaimType) || ClaimTypes.Dns.Equals(IdentityClaim.ClaimType), "");
            if (!_hasSpnSidBeenComputed)
            {
                lock (_thisLock)
                {
                    if (!_hasSpnSidBeenComputed)
                    {
                        string spn = null;
                        try
                        {
                            if (ClaimTypes.Dns.Equals(IdentityClaim.ClaimType))
                            {
                                spn = "host/" + (string)IdentityClaim.Resource;
                            }
                            else
                            {
                                spn = (string)IdentityClaim.Resource;
                            }
                            // canonicalize SPN for use in LDAP filter following RFC 1960:
                            if (spn != null)
                            {
                                spn = spn.Replace("*", @"\*").Replace("(", @"\(").Replace(")", @"\)");
                            }

                            DirectoryEntry de = GetDirectoryEntry();
                            using (DirectorySearcher searcher = new DirectorySearcher(de))
                            {
                                searcher.CacheResults = true;
                                searcher.ClientTimeout = SpnLookupTime;
                                searcher.Filter = "(&(objectCategory=Computer)(objectClass=computer)(servicePrincipalName=" + spn + "))";
                                searcher.PropertiesToLoad.Add("objectSid");
                                SearchResult result = searcher.FindOne();
                                if (result != null)
                                {
                                    byte[] sidBinaryForm = (byte[])result.Properties["objectSid"][0];
                                    _spnSid = new SecurityIdentifier(sidBinaryForm, 0);
                                }
                                else
                                {
                                    //SecurityTraceRecordHelper.TraceSpnToSidMappingFailure(spn, null);
                                }
                            }
                        }
#pragma warning disable 56500 // covered by FxCOP
                        catch (Exception e)
                        {
                            // Always immediately rethrow fatal exceptions.
                            if (Fx.IsFatal(e))
                            {
                                throw;
                            }

                            if (e is NullReferenceException || e is SEHException)
                            {
                                throw;
                            }

                            //SecurityTraceRecordHelper.TraceSpnToSidMappingFailure(spn, e);
                        }
                        finally
                        {
                            _hasSpnSidBeenComputed = true;
                        }
                    }
                }
            }
            return _spnSid;
        }

        private static DirectoryEntry GetDirectoryEntry()
        {
            if (s_directoryEntry == null)
            {
                lock (s_typeLock)
                {
                    if (s_directoryEntry == null)
                    {
                        DirectoryEntry tmp = new DirectoryEntry(@"LDAP://" + SecurityUtils.GetPrimaryDomain());
                        tmp.RefreshCache(new string[] { "name" });
                        s_directoryEntry = tmp;
                    }
                }
            }
            return s_directoryEntry;
        }
    }
}
