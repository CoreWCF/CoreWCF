using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Microsoft.IdentityModel.Selectors
{
    public abstract class SecurityTokenVersion
    {
        public abstract ReadOnlyCollection<string> GetSecuritySpecifications();
    }
}
