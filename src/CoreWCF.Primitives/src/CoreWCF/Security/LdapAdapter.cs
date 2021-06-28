// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace CoreWCF.Security
{
    internal static class LdapAdapter
    {
        public static async Task<List<Claim>> RetrieveClaimsAsync(LdapSettings settings, string originalUserName)
        {
            var upnIndex = originalUserName.IndexOf('@');
            var userAccountName = "";
            if (upnIndex == -1)
            {
                int domainIndex = originalUserName.IndexOf("\\");
                if (domainIndex == -1)
                    return null;
                userAccountName = originalUserName.Substring(domainIndex + 1);
            }
            else
            {
                userAccountName = originalUserName.Substring(0, upnIndex);
            }

            List<Claim> roleClaims = new List<IdentityModel.Claims.Claim>();

            if (settings.ClaimsCache.TryGetValue<IEnumerable<string>>(originalUserName, out var cachedClaims))
            {
                foreach (var claim in cachedClaims)
                {
                    roleClaims.Add(new Claim(ClaimTypes.Role, claim, Rights.Identity));
                }

                return roleClaims;
            }

            var distinguishedName = settings.Domain.Split('.').Select(name => $"dc={name}").Aggregate((a, b) => $"{a},{b}");
            var retrievedClaims = new List<string>();
            if (!string.IsNullOrEmpty(settings.OrgUnit))
            {
                distinguishedName = "OU=" + settings.OrgUnit + "," + distinguishedName;
            }

            var genericFilter = $"(&(objectClass=user)(sAMAccountName={userAccountName}))"; // This is using ldap search query language, it is looking on the server for someUser
            var upnFilter = $"(&(objectClass=user)(userPrincipalName={originalUserName}))";
            var genericSearchRequest = new SearchRequest(distinguishedName, genericFilter, SearchScope.Subtree, null);
            var upnSearchRequest = new SearchRequest(distinguishedName, upnFilter, SearchScope.Subtree, null);
            SearchResponse searchResponse = null;
            try
            {
                if (upnIndex > 0)
                {
                    searchResponse = (SearchResponse)await Task<DirectoryResponse>.Factory.FromAsync(
                    settings.LdapConnection.BeginSendRequest, settings.LdapConnection.EndSendRequest,
                    upnSearchRequest, PartialResultProcessing.NoPartialResultSupport, null);
                    if(searchResponse !=null && searchResponse.Entries != null && searchResponse.Entries.Count > 1)
                    {
                        throw new SecurityNegotiationException(SR.DuplicateUPN); //resource
                    }
                }
                if (searchResponse == null || searchResponse.Entries == null || searchResponse.Entries.Count == 0)
                {
                    searchResponse = (SearchResponse)await Task<DirectoryResponse>.Factory.FromAsync(
                    settings.LdapConnection.BeginSendRequest, settings.LdapConnection.EndSendRequest,
                    genericSearchRequest, PartialResultProcessing.NoPartialResultSupport, null);
                }
            }
            catch (Exception ex)
            {

                if (searchResponse?.ErrorMessage != null)
                {
                    throw new Exception(searchResponse.ErrorMessage);
                }
                else
                {
                    throw ex;
                }
            }

            if (searchResponse.Entries.Count > 0)
            {
                var userFound = searchResponse.Entries[0]; //Get the object that was found on ldap
                var memberof = userFound.Attributes["memberof"]; // You can access ldap Attributes with Attributes property

                foreach (var group in memberof)
                {
                    if(group is null)
                    {
                        continue;
                    }

                    // Example distinguished name: CN=TestGroup,DC=KERB,DC=local
                    var groupDN = $"{Encoding.UTF8.GetString((byte[])group)}";
                    if(!string.IsNullOrEmpty(groupDN))
                    {
                        string[] groupDNItems = groupDN.Split(',');
                        if(groupDNItems.Length > 0 && groupDNItems[0].Contains("CN"))
                        {
                            string[] groupCNItems = groupDNItems[0].Split('=');
                            if(groupCNItems.Length == 2)
                            {
                                retrievedClaims.Add(groupCNItems[1].Trim());
                            }
                        }
                    }
                }

                var entrySize = originalUserName.Length * 2; //Approximate the size of stored key in memory cache.
                foreach (var claim in retrievedClaims)
                {
                    roleClaims.Add(new Claim(ClaimTypes.Role, claim, Rights.Identity));
                    entrySize += claim.Length * 2; //Approximate the size of stored value in memory cache.
                }

                settings.ClaimsCache.Set(originalUserName,
                    retrievedClaims,
                    new MemoryCacheEntryOptions()
                        .SetSize(entrySize)
                        .SetSlidingExpiration(settings.ClaimsCacheSlidingExpiration)
                        .SetAbsoluteExpiration(settings.ClaimsCacheAbsoluteExpiration));
            }
            return roleClaims;
        }
    }
}
