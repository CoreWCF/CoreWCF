// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security.Tokens
{
    public sealed class RecipientServiceModelSecurityTokenRequirement : ServiceModelSecurityTokenRequirement
    {
        public RecipientServiceModelSecurityTokenRequirement()
            : base()
        {
            Properties.Add(IsInitiatorProperty, (object)false);
        }

        public Uri ListenUri
        {
            get
            {
                return GetPropertyOrDefault<Uri>(ListenUriProperty, null);
            }
            set
            {
                Properties[ListenUriProperty] = value;
            }
        }

        //public AuditLogLocation AuditLogLocation
        //{
        //    get
        //    {
        //        return GetPropertyOrDefault<AuditLogLocation>(AuditLogLocationProperty, ServiceSecurityAuditBehavior.defaultAuditLogLocation);
        //    }
        //    set
        //    {
        //        this.Properties[AuditLogLocationProperty] = value;
        //    }
        //}

        //public bool SuppressAuditFailure
        //{
        //    get
        //    {
        //        return GetPropertyOrDefault<bool>(SuppressAuditFailureProperty, ServiceSecurityAuditBehavior.defaultSuppressAuditFailure);
        //    }
        //    set
        //    {
        //        this.Properties[SuppressAuditFailureProperty] = value;
        //    }
        //}

        //public AuditLevel MessageAuthenticationAuditLevel
        //{
        //    get
        //    {
        //        return GetPropertyOrDefault<AuditLevel>(MessageAuthenticationAuditLevelProperty, ServiceSecurityAuditBehavior.defaultMessageAuthenticationAuditLevel);
        //    }
        //    set
        //    {
        //        this.Properties[MessageAuthenticationAuditLevelProperty] = value;
        //    }
        //}

        public override string ToString()
        {
            return InternalToString();
        }
    }
}
