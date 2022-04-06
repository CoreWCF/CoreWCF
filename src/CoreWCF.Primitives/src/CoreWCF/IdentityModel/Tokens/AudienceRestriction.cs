// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Defines settings for a AudienceRestriction verification.
    /// </summary>
    public class AudienceRestriction
    {
        /// <summary>
        /// Creates an instance of <see cref="AudienceRestriction"/>
        /// </summary>
        public AudienceRestriction()
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="AudienceRestriction"/>
        /// </summary>
        /// <param name="audienceMode">Specifies the mode in which AudienceUri restriction is applied.</param>
        public AudienceRestriction( AudienceUriMode audienceMode )
        {
            AudienceMode = audienceMode;
        }

        /// <summary>
        /// Gets/Sets the mode in which Audience URI restriction is applied.
        /// </summary>
        public AudienceUriMode AudienceMode { get; set; } = AudienceUriMode.Always;

        /// <summary>
        /// Gets the list of Allowed Audience URIs.
        /// </summary>
        public Collection<Uri> AllowedAudienceUris { get; } = new Collection<Uri>();
    }

}
