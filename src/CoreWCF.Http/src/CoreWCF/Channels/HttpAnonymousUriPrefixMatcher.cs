// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class HttpAnonymousUriPrefixMatcher : IAnonymousUriPrefixMatcher
    {
        private UriPrefixTable<Uri> _anonymousUriPrefixes;

        internal HttpAnonymousUriPrefixMatcher()
        {
        }

        internal HttpAnonymousUriPrefixMatcher(HttpAnonymousUriPrefixMatcher objectToClone)
            : this()
        {
            if (objectToClone._anonymousUriPrefixes != null)
            {
                _anonymousUriPrefixes = new UriPrefixTable<Uri>(objectToClone._anonymousUriPrefixes);
            }
        }

        public void Register(Uri anonymousUriPrefix)
        {
            if (anonymousUriPrefix == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(anonymousUriPrefix));
            }

            if (!anonymousUriPrefix.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(anonymousUriPrefix), SR.UriMustBeAbsolute);
            }

            if (_anonymousUriPrefixes == null)
            {
                _anonymousUriPrefixes = new UriPrefixTable<Uri>(true);
            }

            if (!_anonymousUriPrefixes.IsRegistered(new BaseUriWithWildcard(anonymousUriPrefix, HostNameComparisonMode.Exact)))
            {
                _anonymousUriPrefixes.RegisterUri(anonymousUriPrefix, HostNameComparisonMode.Exact, anonymousUriPrefix);
            }
        }

        internal bool IsAnonymousUri(Uri to)
        {
            Fx.Assert(to == null || to.IsAbsoluteUri, SR.UriMustBeAbsolute);

            if (_anonymousUriPrefixes == null)
            {
                return false;
            }

            return _anonymousUriPrefixes.TryLookupUri(to, HostNameComparisonMode.Exact, out Uri returnValue);
        }
    }
}
