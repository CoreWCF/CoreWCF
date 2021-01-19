
using System.Collections.Generic;
using TokenEntry = CoreWCF.Security.WSSecurityTokenSerializer.TokenEntry;

namespace CoreWCF.Security
{
    internal class WSSecurityXXX2005 : WSSecurityJan2004
    {
        public WSSecurityXXX2005(WSSecurityTokenSerializer tokenSerializer, SamlSerializer samlSerializer)
            : base(tokenSerializer, samlSerializer)
        {
        }
            
        public override void PopulateTokenEntries(IList<TokenEntry> tokenEntryList)
        {
            PopulateJan2004TokenEntries(tokenEntryList);
        }
    }
}
