﻿namespace CoreWCF.IdentityModel.Claims
{
    internal static class Rights
    {
        const string rightNamespace = XsiConstants.Namespace + "/right";

        const string identity = rightNamespace + "/identity";
        const string possessProperty = rightNamespace + "/possessproperty";

        static public string Identity { get { return identity; } }
        static public string PossessProperty { get { return possessProperty; } }

    }
}