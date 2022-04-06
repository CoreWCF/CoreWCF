// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class UriTemplateClientFormatter : IClientMessageFormatter
    {
        public object DeserializeReply(Message message, object[] parameters) => throw new PlatformNotSupportedException();

        public Message SerializeRequest(MessageVersion messageVersion, object[] parameters) => throw new PlatformNotSupportedException();

        internal static string GetUTStringOrDefault(OperationDescription operationDescription)
        {
            string utString = WebHttpBehavior.GetWebUriTemplate(operationDescription);
            if (utString == null && WebHttpBehavior.GetWebMethod(operationDescription) == WebHttpBehavior.GET)
            {
                utString = MakeDefaultGetUTString(operationDescription);
            }

            if (utString == null)
            {
                utString = operationDescription.Name; // note: not + "/*", see 8988 and 9653
            }

            return utString;
        }

        internal static void Populate(out Dictionary<int, string> pathMapping,
            out Dictionary<int, KeyValuePair<string, Type>> queryMapping,
            out int totalNumUTVars,
            out UriTemplate uriTemplate,
            OperationDescription operationDescription,
            QueryStringConverter qsc,
            string contractName)
        {
            pathMapping = new Dictionary<int, string>();
            queryMapping = new Dictionary<int, KeyValuePair<string, Type>>();
            string utString = GetUTStringOrDefault(operationDescription);
            uriTemplate = new UriTemplate(utString);
            List<string> neededPathVars = new List<string>(uriTemplate.PathSegmentVariableNames);
            List<string> neededQueryVars = new List<string>(uriTemplate.QueryValueVariableNames);
            Dictionary<string, byte> alreadyGotVars = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            totalNumUTVars = neededPathVars.Count + neededQueryVars.Count;
            for (int i = 0; i < operationDescription.Messages[0].Body.Parts.Count; ++i)
            {
                MessagePartDescription mpd = operationDescription.Messages[0].Body.Parts[i];
                string parameterName = XmlConvert.DecodeName(mpd.Name);
                if (alreadyGotVars.ContainsKey(parameterName))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UriTemplateVarCaseDistinction, XmlConvert.DecodeName(operationDescription.Name), contractName, parameterName)));
                }

                List<string> neededPathCopy = new List<string>(neededPathVars);
                foreach (string pathVar in neededPathCopy)
                {
                    if (string.Compare(parameterName, pathVar, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (mpd.Type != typeof(string))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                SR.Format(SR.UriTemplatePathVarMustBeString, XmlConvert.DecodeName(operationDescription.Name), contractName, parameterName)));
                        }
                        pathMapping.Add(i, parameterName);
                        alreadyGotVars.Add(parameterName, 0);
                        neededPathVars.Remove(pathVar);
                    }
                }

                List<string> neededQueryCopy = new List<string>(neededQueryVars);
                foreach (string queryVar in neededQueryCopy)
                {
                    if (string.Compare(parameterName, queryVar, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (!qsc.CanConvert(mpd.Type))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                SR.Format(SR.UriTemplateQueryVarMustBeConvertible, XmlConvert.DecodeName(operationDescription.Name), contractName, parameterName, mpd.Type, qsc.GetType().Name)));
                        }
                        queryMapping.Add(i, new KeyValuePair<string, Type>(parameterName, mpd.Type));
                        alreadyGotVars.Add(parameterName, 0);
                        neededQueryVars.Remove(queryVar);
                    }
                }
            }

            if (neededPathVars.Count != 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.Format(SR.UriTemplateMissingVar, XmlConvert.DecodeName(operationDescription.Name), contractName, neededPathVars[0]))));
            }

            if (neededQueryVars.Count != 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format
                    (SR.Format(SR.UriTemplateMissingVar, XmlConvert.DecodeName(operationDescription.Name), contractName, neededQueryVars[0]))));
            }
        }

        private static string MakeDefaultGetUTString(OperationDescription od)
        {
            StringBuilder sb = new StringBuilder(XmlConvert.DecodeName(od.Name));
            //sb.Append("/*"); // note: not + "/*", see 8988 and 9653
            if (!WebHttpBehavior.IsUntypedMessage(od.Messages[0]))
            {
                sb.Append("?");
                foreach (MessagePartDescription mpd in od.Messages[0].Body.Parts)
                {
                    string parameterName = XmlConvert.DecodeName(mpd.Name);
                    sb.Append(parameterName);
                    sb.Append("={");
                    sb.Append(parameterName);
                    sb.Append("}&");
                }
                sb.Remove(sb.Length - 1, 1);
            }

            return sb.ToString();
        }
    }
}
