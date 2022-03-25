// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Web;
using System.Xml.Linq;

namespace CoreWCF.Dispatcher
{
    internal class HelpHtmlBuilder
    {
        private const string HtmlHtmlElementName = "{http://www.w3.org/1999/xhtml}html";
        private const string HtmlHeadElementName = "{http://www.w3.org/1999/xhtml}head";
        private const string HtmlTitleElementName = "{http://www.w3.org/1999/xhtml}title";
        private const string HtmlBodyElementName = "{http://www.w3.org/1999/xhtml}body";
        private const string HtmlPElementName = "{http://www.w3.org/1999/xhtml}p";
        private const string HtmlDivElementName = "{http://www.w3.org/1999/xhtml}div";
        private const string HtmlClassAttributeName = "class";
        private const string HtmlIdAttributeName = "id";
        private const string HtmlHeading1Class = "heading1";
        private const string HtmlContentClass = "content";

        public static XDocument CreateServerErrorPage(Uri helpUri, Exception error)
        {
            XDocument document = CreateBaseDocument(SR.Format(SR.HelpPageRequestErrorTitle));

            XElement div = new XElement(HtmlDivElementName, new XAttribute(HtmlIdAttributeName, HtmlContentClass),
                new XElement(HtmlPElementName, new XAttribute(HtmlClassAttributeName, HtmlHeading1Class), SR.Format(SR.HelpPageRequestErrorTitle)));
            if (helpUri == null)
            {
                if (error != null)
                {
                    div.Add(new XElement(HtmlPElementName, SR.Format(SR.HelpServerErrorProcessingRequestWithDetails, error.Message)));
                    div.Add(new XElement(HtmlPElementName, error.StackTrace ?? String.Empty));
                }
                else
                {
                    div.Add(new XElement(HtmlPElementName, SR.Format(SR.HelpServerErrorProcessingRequest)));
                }
            }
            else
            {
                string encodedHelpLink = HttpUtility.HtmlEncode(helpUri.AbsoluteUri);
                if (error != null)
                {
                    string errorMessage = HttpUtility.HtmlEncode(error.Message);
                    div.Add(XElement.Parse(SR.Format(SR.HelpServerErrorProcessingRequestWithDetailsAndLink, encodedHelpLink, errorMessage)));
                    div.Add(new XElement(HtmlPElementName, error.StackTrace ?? string.Empty));
                }
                else
                {
                    div.Add(XElement.Parse(SR.Format(SR.HelpServerErrorProcessingRequestWithLink, encodedHelpLink)));
                }

            }

            document.Descendants(HtmlBodyElementName).First().Add(div);
            return document;
        }

        public static XDocument CreateTransferRedirectPage(string originalTo, string newLocation)
        {
            XDocument document = CreateBaseDocument(SR.HelpPageTitleText);

            XElement div = new XElement(HtmlDivElementName, new XAttribute(HtmlIdAttributeName, HtmlContentClass),
                new XElement(HtmlPElementName, new XAttribute(HtmlClassAttributeName, HtmlHeading1Class), SR.HelpPageTitleText),
                XElement.Parse(SR.Format(SR.HelpPageRedirect, HttpUtility.HtmlEncode(originalTo), HttpUtility.HtmlEncode(newLocation))));
            document.Descendants(HtmlBodyElementName).First().Add(div);
            return document;
        }

        public static XDocument CreateMethodNotAllowedPage(Uri helpUri)
        {
            XDocument document = CreateBaseDocument(SR.HelpPageTitleText);

            XElement div = new XElement(HtmlDivElementName, new XAttribute(HtmlIdAttributeName, HtmlContentClass),
                new XElement(HtmlPElementName, new XAttribute(HtmlClassAttributeName, HtmlHeading1Class), SR.HelpPageTitleText));
            if (helpUri == null)
            {
                div.Add(new XElement(HtmlPElementName, SR.HelpPageMethodNotAllowed));
            }
            else
            {
                div.Add(XElement.Parse(SR.Format(SR.HelpPageMethodNotAllowedWithLink, HttpUtility.HtmlEncode(helpUri.AbsoluteUri))));
            }
            document.Descendants(HtmlBodyElementName).First().Add(div);
            return document;
        }

        public static XDocument CreateEndpointNotFound(Uri helpUri)
        {
            XDocument document = CreateBaseDocument(SR.HelpPageTitleText);

            XElement div = new XElement(HtmlDivElementName, new XAttribute(HtmlIdAttributeName, HtmlContentClass),
                new XElement(HtmlPElementName, new XAttribute(HtmlClassAttributeName, HtmlHeading1Class), SR.HelpPageTitleText));
            if (helpUri == null)
            {
                div.Add(new XElement(HtmlPElementName, SR.HelpPageEndpointNotFound));
            }
            else
            {
                div.Add(XElement.Parse(SR.Format(SR.HelpPageEndpointNotFoundWithLink, HttpUtility.HtmlEncode(helpUri.AbsoluteUri))));
            }
            document.Descendants(HtmlBodyElementName).First().Add(div);
            return document;
        }

        private static XDocument CreateBaseDocument(string title)
        {
            return new XDocument(
                new XDocumentType("html", "-//W3C//DTD XHTML 1.0 Transitional//EN", "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd", null),
                new XElement(HtmlHtmlElementName,
                    new XElement(HtmlHeadElementName,
                        new XElement(HtmlTitleElementName, title),
                        new XElement("{http://www.w3.org/1999/xhtml}style", SR.Format(SR.HelpPageHtml))),
                    new XElement(HtmlBodyElementName)));
        }
    }
}
