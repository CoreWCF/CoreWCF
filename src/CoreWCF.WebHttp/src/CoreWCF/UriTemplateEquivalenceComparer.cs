// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace CoreWCF
{
    public class UriTemplateEquivalenceComparer : IEqualityComparer<UriTemplate>
    {
        private static UriTemplateEquivalenceComparer s_instance;

        internal static UriTemplateEquivalenceComparer Instance
        {
            get
            {
                if (s_instance == null)
                {
                    // lock-free, fine if we allocate more than one
                    s_instance = new UriTemplateEquivalenceComparer();
                }
                return s_instance;
            }
        }

        public bool Equals(UriTemplate x, UriTemplate y)
        {
            if (x == null)
            {
                return y == null;
            }

            return x.IsEquivalentTo(y);
        }

        public int GetHashCode(UriTemplate obj)
        {
            if (obj == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(obj));
            }

            for (int i = obj._segments.Count - 1; i >= 0; --i)
            {
                if (obj._segments[i].Nature == UriTemplatePartType.Literal)
                {
                    return obj._segments[i].GetHashCode();
                }
            }

            return obj._segments.Count + obj._queries.Count;
        }
    }
}
