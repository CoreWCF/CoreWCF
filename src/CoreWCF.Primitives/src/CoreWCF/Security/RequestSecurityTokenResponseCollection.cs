// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    sealed class RequestSecurityTokenResponseCollection : BodyWriter
    {
        IEnumerable<RequestSecurityTokenResponse> rstrCollection;
        SecurityStandardsManager standardsManager;

        public RequestSecurityTokenResponseCollection(IEnumerable<RequestSecurityTokenResponse> rstrCollection)
            : this(rstrCollection, SecurityStandardsManager.DefaultInstance)
        { }

        public RequestSecurityTokenResponseCollection(IEnumerable<RequestSecurityTokenResponse> rstrCollection, SecurityStandardsManager standardsManager)
            : base(true)
        {
            if (rstrCollection == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rstrCollection));
            int index = 0;
            foreach (RequestSecurityTokenResponse rstr in rstrCollection)
            {
                if (rstr == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(String.Format(CultureInfo.InvariantCulture, "rstrCollection[{0}]", index));
                ++index;
            }
            this.rstrCollection = rstrCollection;
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(standardsManager)));
            }
            this.standardsManager = standardsManager;
        }

        public IEnumerable<RequestSecurityTokenResponse> RstrCollection
        {
            get
            {
                return this.rstrCollection;
            }
        }

        public void WriteTo(XmlWriter writer)
        {
            this.standardsManager.TrustDriver.WriteRequestSecurityTokenResponseCollection(this, writer);
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            WriteTo(writer);
        }
    }
}
