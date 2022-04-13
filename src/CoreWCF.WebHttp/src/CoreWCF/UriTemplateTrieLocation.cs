// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal class UriTemplateTrieLocation
    {
        public UriTemplateTrieIntraNodeLocation LocationWithin;

        public UriTemplateTrieNode Node;

        public UriTemplateTrieLocation(UriTemplateTrieNode n, UriTemplateTrieIntraNodeLocation i)
        {
            Node = n;
            LocationWithin = i;
        }
    }
}
