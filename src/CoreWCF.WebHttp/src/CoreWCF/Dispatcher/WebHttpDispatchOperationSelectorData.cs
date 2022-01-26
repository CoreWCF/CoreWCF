// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Dispatcher
{
    internal class WebHttpDispatchOperationSelectorData
    {
        internal List<string> AllowedMethods { get; set; }

        internal string AllowHeader
        {
            get
            {
                if (AllowedMethods != null)
                {
                    int allowedHeadersCount = AllowedMethods.Count;
                    if (allowedHeadersCount > 0)
                    {
                        StringBuilder stringBuilder = new StringBuilder(AllowedMethods[0]);
                        for (int x = 1; x < allowedHeadersCount; x++)
                        {
                            stringBuilder.Append(", " + AllowedMethods[x]);
                        }

                        return stringBuilder.ToString();
                    }
                }

                return null;
            }
        }
    }
}
