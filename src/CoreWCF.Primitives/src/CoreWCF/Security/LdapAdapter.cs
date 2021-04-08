// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Text;
using CoreWCF.IdentityModel.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Security
{
    internal static class LdapAdapter
    {
        public static List<Claim> RetrieveClaims(LdapSettings settings, string userName)
        {
            var user = userName;
            var userAccountNameIndex = user.IndexOf('@');
            var userAccountName = "";
            if (userAccountNameIndex == -1)
            {
                userAccountNameIndex = user.IndexOf("\\");
                if (userAccountNameIndex == -1)
                    return null;
                userAccountName = user.Substring(userAccountNameIndex+1);
            }
            else
            {
                userAccountName = user.Substring(0, userAccountNameIndex);
            }

            List<Claim> roleClaims = new List<IdentityModel.Claims.Claim>();
            if (settings.ClaimsCache == null)
            {
                settings.ClaimsCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = settings.ClaimsCacheSize });
            }

            if (settings.ClaimsCache.TryGetValue<IEnumerable<string>>(user, out var cachedClaims))
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
            var filter = $"(&(objectClass=user)(sAMAccountName={userAccountName}))"; // This is using ldap search query language, it is looking on the server for someUser
            var searchRequest = new SearchRequest(distinguishedName, filter, SearchScope.Subtree, null);
            SearchResponse searchResponse = null;
            try
            {
                searchResponse = settings.LdapConnection.SendRequest(searchRequest) as SearchResponse;
            }
            catch(Exception ex)
            {

               if (searchResponse?.ErrorMessage !=null)
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
                    // Example distinguished name: CN=TestGroup,DC=KERB,DC=local
                    var groupDN = $"{Encoding.UTF8.GetString((byte[])group)}";
                    var groupCN = groupDN.Split(',')[0].Substring("CN=".Length);
                    retrievedClaims.Add(groupCN);
                }

                var entrySize = user.Length * 2; //Approximate the size of stored key in memory cache.
                foreach (var claim in retrievedClaims)
                {
                    roleClaims.Add(new Claim(ClaimTypes.Role, claim, Rights.Identity));
                    entrySize += claim.Length * 2; //Approximate the size of stored value in memory cache.
                }

                settings.ClaimsCache.Set(user,
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
