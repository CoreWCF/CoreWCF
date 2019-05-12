using System;

namespace CoreWCF.Channels
{
    public interface IAnonymousUriPrefixMatcher
    {
        void Register(Uri anonymousUriPrefix);
    }
}