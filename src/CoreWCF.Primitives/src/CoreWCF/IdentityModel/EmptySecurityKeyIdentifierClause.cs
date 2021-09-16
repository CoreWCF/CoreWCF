// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Represents an empty SecurityKeyClause.  This class is used when an 'encrypted data element' or ' signature element' does
    /// not contain a 'key info element' that is used to describe the key required to decrypt the data or check the signature.
    /// </summary>
    public class EmptySecurityKeyIdentifierClause : SecurityKeyIdentifierClause
    {

        /// <summary>
        /// Creates an instance of <see cref="EmptySecurityKeyIdentifierClause"/>
        /// </summary>
        /// <remarks>This constructor assumes that the user knows how to resolve the key required without any context.</remarks>
        public EmptySecurityKeyIdentifierClause()
            : this( null )
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="EmptySecurityKeyIdentifierClause"/>
        /// </summary>
        /// <param name="context">Used to provide a hint when there is a need resolve an empty clause to a particular key.
        /// In the case of Saml11 and Saml2 tokens that have signatures without KeyInfo, 
        /// this clause will contain the assertion that is currently being processed.</param>
        public EmptySecurityKeyIdentifierClause( object context )
            : base( typeof( EmptySecurityKeyIdentifierClause ).ToString() )
        {
            Context = context;
        }

        /// <summary>
        /// Used to provide a hint when there is a need to resolve to a particular key.
        /// In the case of Saml11 and Saml2 tokens that have signatures without KeyInfo, 
        /// this will contain the assertion that is currently being processed.
        /// </summary>
        public object Context { get; }
    }
}
