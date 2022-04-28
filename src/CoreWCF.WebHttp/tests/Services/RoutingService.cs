// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Services
{
    public class RoutingService : ServiceContract.IRoutingService
    {
        public void NoParam() { }

        public string PathParam(string val) => val;

        public string QueryParam(string val) => val;

        public string Wildcard() => "wildcard";

        public string CompoundPath(string filename, string ext) => filename + "." + ext;

        public string NamedWildcard(string val) => val;

        public string DefaultValue(string val) => val;
    }
}
