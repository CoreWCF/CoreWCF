// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace CoreWCF
{
    // This class was named UriTemplatePathEquivalentSet in the Orcas bits, where it used to
    //  represent a set of uri templates, which are equivalent in thier path. The introduction
    //  of terminal defaults, caused it to be no longer true; now it is representing a set
    //  of templates, which are equivalent in their path till a certian point, which is stored
    //  in the segmentsCount field. To highlight that fact the class was renamed as
    //  UriTemplatePathPartiallyEquivalentSet.
    internal class UriTemplatePathPartiallyEquivalentSet
    {
        public UriTemplatePathPartiallyEquivalentSet(int segmentsCount)
        {
            SegmentsCount = segmentsCount;
            Items = new List<KeyValuePair<UriTemplate, object>>();
        }

        public List<KeyValuePair<UriTemplate, object>> Items { get; }

        public int SegmentsCount { get; }
    }
}
