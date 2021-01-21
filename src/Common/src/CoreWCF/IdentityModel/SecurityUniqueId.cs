// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;

namespace CoreWCF.IdentityModel
{
    internal class SecurityUniqueId
    {
        static long nextId = 0;
        static string commonPrefix = "uuid-" + Guid.NewGuid().ToString() + "-";

        long id;
        string prefix;
        string val;

        SecurityUniqueId(string prefix, long id)
        {
            this.id = id;
            this.prefix = prefix;
            val = null;
        }

        public static SecurityUniqueId Create()
        {
            return SecurityUniqueId.Create(commonPrefix);
        }

        public static SecurityUniqueId Create(string prefix)
        {
            return new SecurityUniqueId(prefix, Interlocked.Increment(ref nextId));
        }

        public string Value
        {
            get
            {
                if (val == null)
                    val = prefix + id.ToString(CultureInfo.InvariantCulture);

                return val;
            }
        }
    }

}
