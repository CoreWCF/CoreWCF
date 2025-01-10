// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Helpers
{
    internal class ListeningPortHelper
    {
        private Dictionary<string, Func<int>> _schemeToPortDelegate = new();

        public void AddSchemeToPortDelegate(string scheme, Func<int> portDelegate)
        {
            _schemeToPortDelegate.Add(scheme, portDelegate);
        }

        public int GetPortForScheme(string scheme)
        {
            if (_schemeToPortDelegate.TryGetValue(scheme, out Func<int> portDelegate))
            {
                return portDelegate();
            }

            return 0;
        }

        public IEnumerable<string> Schemes => _schemeToPortDelegate.Keys;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (var kvp in _schemeToPortDelegate)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                first = false;
                var port = kvp.Value();
                var portStr = port != 0 ? port.ToString() : "?";
                sb.Append($"{kvp.Key} => {portStr}");

            }

            return sb.ToString();
        }
    }
}
