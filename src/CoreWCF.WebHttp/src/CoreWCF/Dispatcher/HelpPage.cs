// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TODO: Help

using System.Collections.Generic;
using System.Net;
using CoreWCF.Channels;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class HelpPage
    {
        public const string OperationListHelpPageUriTemplate = "help";
        public const string OperationHelpPageUriTemplate = "help/operations/{operation}";
        private const string HelpMethodName = "GetHelpPage";
        private const string HelpOperationMethodName = "GetOperationHelpPage";

        public static IEnumerable<KeyValuePair<UriTemplate, object>> GetOperationTemplatePairs()
        {
            return new KeyValuePair<UriTemplate, object>[]
            {
                new KeyValuePair<UriTemplate, object>(new UriTemplate(OperationListHelpPageUriTemplate), HelpMethodName),
                new KeyValuePair<UriTemplate, object>(new UriTemplate(OperationHelpPageUriTemplate), HelpOperationMethodName)
            };
        }

        public object Invoke(UriTemplateMatch match)
        {
            switch ((string)match.Data)
            {
                case HelpMethodName:
                case HelpOperationMethodName:
                    return GetHelpPage();
                default:
                    return null;
            }
        }

        private Message GetHelpPage()
        {
            if (WebOperationContext.Current != null)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Redirect;
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Location", "/swagger/index.html");

                return Message.CreateMessage(MessageVersion.None, null);
            }

            return null;
        }
    }
}
