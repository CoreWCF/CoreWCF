using Microsoft.IdentityModel.Claims;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Security;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace Microsoft.ServiceModel
{
    public class SpnEndpointIdentity : EndpointIdentity
    {
        private static TimeSpan s_spnLookupTime = TimeSpan.FromMinutes(1);
        private SecurityIdentifier _spnSid;

        // Double-checked locking pattern requires volatile for read/write synchronization
        private bool _hasSpnSidBeenComputed;
        private object _thisLock = new object();
        private static object s_typeLock = new object();

        // Double-checked locking pattern requires volatile for read/write synchronization
        private static DirectoryEntry directoryEntry;

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
                                                    SR.ValueMustBeNonNegative));
                }
                s_spnLookupTime = value;
            }
        }

        public SpnEndpointIdentity(string spnName)
        {
            if (spnName == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("spnName");

            base.Initialize(Claim.CreateSpnClaim(spnName));
        }

        public SpnEndpointIdentity(Claim identity)
        {
            if (identity == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("identity");

            // PreSharp Bug: Parameter 'identity.ResourceType' to this public method must be validated: A null-dereference can occur here.
#pragma warning suppress 56506 // Claim.ClaimType will never return null
            if (!identity.ClaimType.Equals(ClaimTypes.Spn))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.UnrecognizedClaimTypeForIdentity, identity.ClaimType, ClaimTypes.Spn));

            base.Initialize(identity);
        }

        internal override void WriteContentsTo(XmlDictionaryWriter writer)
        {
            if (writer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("writer");

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
#pragma warning suppress 56500 // covered by FxCOP
                        catch (Exception e)
                        {
                            // Always immediately rethrow fatal exceptions.
                            if (Fx.IsFatal(e)) throw;

                            if (e is NullReferenceException || e is SEHException)
                                throw;

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
            if (directoryEntry == null)
            {
                lock (s_typeLock)
                {
                    if (directoryEntry == null)
                    {
                        DirectoryEntry tmp = new DirectoryEntry(@"LDAP://" + SecurityUtils.GetPrimaryDomain());
                        tmp.RefreshCache(new string[] { "name" });
                        directoryEntry = tmp;
                    }
                }
            }
            return directoryEntry;
        }
    }

}
