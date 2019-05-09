using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Microsoft.IdentityModel
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
