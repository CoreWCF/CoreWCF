using Microsoft.IdentityModel.Tokens;
using Microsoft.ServiceModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Microsoft.IdentityModel.Selectors
{
    // TODO: Evaluate removing this class and all usages as the full framework implementation has a lot more functionality.
    internal abstract class SecurityTokenResolver
    {
        private class SimpleTokenResolver : SecurityTokenResolver
        {
            private ReadOnlyCollection<SecurityToken> _tokens;
            private bool _canMatchLocalId;

            public SimpleTokenResolver(ReadOnlyCollection<SecurityToken> tokens, bool canMatchLocalId)
            {
                if (tokens == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokens));

                _tokens = tokens;
                _canMatchLocalId = canMatchLocalId;
            }
        }
    }
}
