// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Description
{
    internal static class MetadataStrings
    {
        public static class WSPolicy
        {
            public const string Prefix = "wsp";
            //public const string NamespaceUri = "http://schemas.xmlsoap.org/ws/2004/09/policy";
            //public const string NamespaceUri15 = "http://www.w3.org/ns/ws-policy";

            //public static class Attributes
            //{
            //    public const string Optional = "Optional";
            //    public const string PolicyURIs = "PolicyURIs";
            //    public const string URI = "URI";
            //    public const string TargetNamespace = "TargetNamespace";
            //}

            public static class Elements
            {
                //public const string PolicyReference = "PolicyReference";
                //public const string All = "All";
                public const string ExactlyOne = "ExactlyOne";
                //public const string Policy = "Policy";
            }
        }
    }
}
