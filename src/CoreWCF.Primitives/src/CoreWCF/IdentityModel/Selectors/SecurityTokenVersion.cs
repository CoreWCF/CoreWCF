using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CoreWCF.IdentityModel.Selectors
{
    public abstract class SecurityTokenVersion
    {
        public abstract ReadOnlyCollection<string> GetSecuritySpecifications();
    }
}
