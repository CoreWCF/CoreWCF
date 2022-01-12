// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CoreWCF.Dispatcher;

namespace CoreWCF.Description
{
    internal static class ChannelDispatcherExtensions
    {
        public static string CreateContractListString(this ChannelDispatcher channelDispatcher)
        {
            const string OpenQuote = "\"";
            const string CloseQuote = "\"";
            const string Space = " ";

            Collection<string> namesSeen = new Collection<string>();
            StringBuilder endpointContractNames = new StringBuilder();

            foreach (EndpointDispatcher ed in channelDispatcher.Endpoints)
            {
                if (!namesSeen.Contains(ed.ContractName))
                {
                    if (endpointContractNames.Length > 0)
                    {
                        endpointContractNames.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                        endpointContractNames.Append(Space);
                    }

                    endpointContractNames.Append(OpenQuote);
                    endpointContractNames.Append(ed.ContractName);
                    endpointContractNames.Append(CloseQuote);

                    namesSeen.Add(ed.ContractName);
                }
            }

            return endpointContractNames.ToString();
        }
    }
}
