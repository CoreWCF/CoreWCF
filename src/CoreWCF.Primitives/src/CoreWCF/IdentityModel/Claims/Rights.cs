// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Claims
{
    public static class Rights
    {
        private const string rightNamespace = XsiConstants.Namespace + "/right";
        private const string identity = rightNamespace + "/identity";
        private const string possessProperty = rightNamespace + "/possessproperty";

        static public string Identity { get { return identity; } }
        static public string PossessProperty { get { return possessProperty; } }

    }
}