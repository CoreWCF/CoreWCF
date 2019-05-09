using System;

namespace Microsoft.ServiceModel.Channels
{
    public interface IAnonymousUriPrefixMatcher
    {
        void Register(Uri anonymousUriPrefix);
    }
}