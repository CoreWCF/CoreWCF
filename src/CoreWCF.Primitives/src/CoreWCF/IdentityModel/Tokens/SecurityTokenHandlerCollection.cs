// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Defines a collection of SecurityTokenHandlers.
    /// </summary>
    public class SecurityTokenHandlerCollection : Collection<SecurityTokenHandler>
    {
        internal static int s_defaultHandlerCollectionCount = 8;
        internal static SecurityTokenHandlerCollection s_defaultSecurityTokenHandlerCollection = null; //TODO if better way
        private readonly Dictionary<string, SecurityTokenHandler> _handlersByIdentifier = new Dictionary<string, SecurityTokenHandler>();
        private readonly Dictionary<Type, SecurityTokenHandler> _handlersByType = new Dictionary<Type, SecurityTokenHandler>();
        private readonly KeyInfoSerializer _keyInfoSerializer;

        /// <summary>
        /// Creates an instance of <see cref="SecurityTokenHandlerCollection"/>.
        /// Creates an empty set.
        /// </summary>
        public SecurityTokenHandlerCollection()
            : this(new SecurityTokenHandlerConfiguration())
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="SecurityTokenHandlerCollection"/>.
        /// Creates an empty set.
        /// </summary>
        /// <param name="configuration">The configuration to associate with the collection.</param>
        public SecurityTokenHandlerCollection(SecurityTokenHandlerConfiguration configuration)
        {
            Configuration = configuration ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(configuration));
            _keyInfoSerializer = new KeyInfoSerializer(true);
        }

        /// <summary>
        /// Creates an instance of <see cref="SecurityTokenHandlerCollection"/>
        /// </summary>
        /// <param name="handlers">List of SecurityTokenHandlers to initialize from.</param>
        /// <remarks>
        /// Do not use this constructor to attempt to clone an instance of a SecurityTokenHandlerCollection,
        /// use the Clone method instead.
        /// </remarks>
        public SecurityTokenHandlerCollection(IEnumerable<SecurityTokenHandler> handlers)
            : this(handlers, new SecurityTokenHandlerConfiguration())
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="SecurityTokenHandlerCollection"/>
        /// </summary>
        /// <param name="handlers">List of SecurityTokenHandlers to initialize from.</param>
        /// <param name="configuration">The <see cref="SecurityTokenHandlerConfiguration"/> in effect.</param>
        /// <remarks>
        /// Do not use this constructor to attempt to clone an instance of a SecurityTokenHandlerCollection,
        /// use the Clone method instead.
        /// </remarks>
        public SecurityTokenHandlerCollection(IEnumerable<SecurityTokenHandler> handlers, SecurityTokenHandlerConfiguration configuration)
            : this(configuration)
        {
            if (handlers == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(handlers));
            }

            foreach (SecurityTokenHandler handler in handlers)
            {
                Add(handler);
            }
        }

        /// <summary>
        /// Gets an instance of <see cref="SecurityTokenHandlerConfiguration"/>
        /// </summary>
        public SecurityTokenHandlerConfiguration Configuration { get; }

        /// <summary>
        /// Gets the List of System.Type of the Token Handlers in this collection.
        /// </summary>
        public IEnumerable<Type> TokenTypes
        {
            get { return _handlersByType.Keys; }
        }

        /// <summary>
        /// Gets the list of Token type Identifier of the Token Handlers.
        /// </summary>
        public IEnumerable<string> TokenTypeIdentifiers
        {
            get
            {
                return _handlersByIdentifier.Keys;
            }
        }

        /// <summary>
        /// Gets a Token Handler by its Token Type Identifier.
        /// </summary>
        /// <param name="tokenTypeIdentifier">The Token Type Identfier string to search for.</param>
        /// <returns>Instance of a SecurityTokenHandler.</returns>
        public SecurityTokenHandler this[string tokenTypeIdentifier]
        {
            get
            {
                if (string.IsNullOrEmpty(tokenTypeIdentifier))
                {
                    return null;
                }

                _handlersByIdentifier.TryGetValue(tokenTypeIdentifier, out SecurityTokenHandler handler);
                return handler;
            }
        }

        /// <summary>
        /// Gets a Token Handler that can handle a given SecurityToken.
        /// </summary>
        /// <param name=nameof(token)>SecurityToken for which a Token Handler is requested.</param>
        /// <returns>Instance of SecurityTokenHandler.</returns>
        public SecurityTokenHandler this[SecurityToken token]
        {
            get
            {
                if (null == token)
                {
                    return null;
                }

                return this[token.GetType()];
            }
        }

        /// <summary>
        /// Gets a Token Handler based on the System.Type of the token.
        /// </summary>
        /// <param name="tokenType">System.Type of the Token that needs to be handled.</param>
        /// <returns>Instance of SecurityTokenHandler.</returns>
        public SecurityTokenHandler this[Type tokenType]
        {
            get
            {
                SecurityTokenHandler handler = null;
                if (tokenType != null)
                {
                    _handlersByType.TryGetValue(tokenType, out handler);
                }

                return handler;
            }
        }

        /// <summary>
        /// Creates a system default collection of basic SecurityTokenHandlers, each of which has the system default configuration.
        /// The SecurityTokenHandlers in this collection must be configured with service specific data before they can be used.
        /// </summary>
        /// <returns>A SecurityTokenHandlerCollection with default basic SecurityTokenHandlers.</returns>
        public static SecurityTokenHandlerCollection CreateDefaultSecurityTokenHandlerCollection()
        {
            SecurityTokenHandlerCollection collection = new SecurityTokenHandlerCollection(new SecurityTokenHandlerConfiguration());
            foreach(SecurityTokenHandler handler in s_defaultSecurityTokenHandlerCollection)
            {
                collection.Add(handler);
            }
            return collection;
        }

        //TODO - Check which one is important apart from Saml and port accordingly..

        /// <summary>
        /// Creates a system default collection of basic SecurityTokenHandlers, each of which has the system default configuration.
        /// The SecurityTokenHandlers in this collection must be configured with service specific data before they can be used.
        /// </summary>
        /// <param name="configuration">The configuration to associate with the collection.</param>
        /// <returns>A SecurityTokenHandlerCollection with default basic SecurityTokenHandlers.</returns>
        internal static SecurityTokenHandlerCollection CreateDefaultSecurityTokenHandlerCollection(SecurityTokenHandlerConfiguration configuration,
            IEnumerable<SecurityTokenHandler> securityTokenHandlers)
        {
            SecurityTokenHandlerCollection collection = new SecurityTokenHandlerCollection(configuration);
            foreach(SecurityTokenHandler handler in securityTokenHandlers)
            {
                collection.AddOrReplace(handler);
            }
            s_defaultHandlerCollectionCount = collection.Count;
            s_defaultSecurityTokenHandlerCollection = collection;
            return collection;
        }

        internal SecurityTokenSerializer KeyInfoSerializer
        {
            get { return _keyInfoSerializer; }
        }

        /// <summary>
        /// Adds a new handler or replace the existing handler with the same token type identifier 
        /// with with the new handler.
        /// </summary>
        /// <param name="handler">The SecurityTokenHandler to add or replace</param>
        /// <exception cref="ArgumentNullException">When the input parameter is null.</exception>
        public void AddOrReplace(SecurityTokenHandler handler)
        {
            if (handler == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(handler));
            }

            // Remove the old one if it exists
            Type tokenType = handler.TokenType;
            if (tokenType != null && _handlersByType.ContainsKey(tokenType))
            {
                Remove(this[tokenType]);
            }
            else
            {
                string[] identifiers = handler.GetTokenTypeIdentifiers();
                if (identifiers != null)
                {
                    foreach (string tokenTypeIdentifier in identifiers)
                    {
                        if (tokenTypeIdentifier != null && _handlersByIdentifier.ContainsKey(tokenTypeIdentifier))
                        {
                            Remove(this[tokenTypeIdentifier]);
                            break;
                        }
                    }
                }
            }

            // Add the new handler in the collection
            Add(handler);
        }

        /// <summary>
        /// Checks if a token can be read using the SecurityTokenHandlers.
        /// </summary>
        /// <param name=nameof(reader)>XmlReader pointing at token.</param>
        /// <returns>True if the token can be read, false otherwise</returns>
        /// <exception cref="ArgumentNullException">The input argument 'reader' is null.</exception>
        public bool CanReadToken(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            foreach (SecurityTokenHandler handler in this)
            {
                if (null != handler && handler.CanReadToken(reader))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a token can be read using the SecurityTokenHandlers.
        /// </summary>
        /// <param name="tokenString">The token string thats needs to be read.</param>
        /// <returns>True if the token can be read, false otherwise</returns>
        /// <exception cref="ArgumentException">The input argument 'tokenString' is null or empty.</exception>
        public bool CanReadToken(string tokenString)
        {
            if (string.IsNullOrEmpty(tokenString))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenString));
            }

            foreach (SecurityTokenHandler handler in this)
            {
                if (null != handler && handler.CanReadToken(tokenString))
                {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Checks if a token can be written using the SecurityTokenHandlers.
        /// </summary>
        /// <param name=token>SecurityToken to be written out.</param>
        /// <returns>True if the token can be written, false otherwise</returns>
        /// <exception cref="ArgumentNullException">The input argument 'token' is null.</exception>
        public bool CanWriteToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            SecurityTokenHandler handler = this[token];
            if (null != handler && handler.CanWriteToken)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates a given token using the SecurityTokenHandlers.
        /// </summary>
        /// <param name=nameof(token)>The SecurityToken to be validated.</param>
        /// <returns>A <see cref="ReadOnlyCollection{T}"/> of <see cref="ClaimsIdentity"/> representing the identities contained in the token.</returns>
        /// <exception cref="ArgumentNullException">The input argument 'token' is null.</exception>
        /// <exception cref="InvalidOperationException">A <see cref="SecurityTokenHandler"/> cannot be found that can validate the <see cref="SecurityToken"/>.</exception>                
        public ReadOnlyCollection<ClaimsIdentity> ValidateToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            SecurityTokenHandler handler = this[token];
            if (null == handler || !handler.CanValidateToken)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID4011, token.GetType())));
            }

            return handler.ValidateToken(token);
        }

        /// <summary>
        /// Reads a token using the TokenHandlers.
        /// </summary>
        /// <param name=nameof(reader)>XmlReader pointing at token.</param>
        /// <returns>Instance of <see cref="SecurityToken"/></returns>
        /// <exception cref="ArgumentNullException">The input argument 'reader' is null.</exception>
        public SecurityToken ReadToken(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            foreach (SecurityTokenHandler handler in this)
            {
                if (null != handler && handler.CanReadToken(reader))
                {
                    return handler.ReadToken(reader);
                }
            }

            return null;
        }

        /// <summary>
        /// Reads a token using the TokenHandlers.
        /// </summary>
        /// <param name="tokenString">The token string to be deserialized.</param>
        /// <returns>Instance of <see cref="SecurityToken"/></returns>
        /// <exception cref="ArgumentException">The input argument 'tokenString' is null or empty.</exception>
        public SecurityToken ReadToken(string tokenString)
        {
            if (string.IsNullOrEmpty(tokenString))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenString));
            }

            foreach (SecurityTokenHandler handler in this)
            {
                if (null != handler && handler.CanReadToken(tokenString))
                {
                    return handler.ReadToken(tokenString);
                }
            }

            return null;
        }

        /// <summary>
        /// Writes a given SecurityToken to the XmlWriter.
        /// </summary>
        /// <param name="writer">XmlWriter to write the token into.</param>
        /// <param name=nameof(token)>SecurityToken to be written out.</param>
        /// <exception cref="ArgumentNullException">The input argument 'writer' or 'token' is null.</exception>
        public void WriteToken(XmlWriter writer, SecurityToken token)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            SecurityTokenHandler handler = this[token];
            if (null == handler || !handler.CanWriteToken)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID4010, token.GetType())));
            }

            handler.WriteToken(writer, token);
        }

        /// <summary>
        /// Writes a given SecurityToken to a string.
        /// </summary>
        /// <param name=nameof(token)>SecurityToken to be written out.</param>
        /// <returns>The serialized token.</returns>
        /// <exception cref="ArgumentNullException">The input argument 'token' is null.</exception>
        public string WriteToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            SecurityTokenHandler handler = this[token];
            if (null == handler || !handler.CanWriteToken)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID4010, token.GetType())));
            }

            return handler.WriteToken(token);
        }

        /// <summary>
        /// Override. (Inherited from Collection&lt;T>"/>
        /// </summary>
        protected override void ClearItems()
        {
            base.ClearItems();
            _handlersByIdentifier.Clear();
            _handlersByType.Clear();
        }

        /// <summary>
        /// Override. (Inherited from Collection&lt;T&gt;"/>
        /// </summary>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="item">The object to insert. The value can be null for reference types.</param>
        protected override void InsertItem(int index, SecurityTokenHandler item)
        {
            base.InsertItem(index, item);

            try
            {
                AddToDictionaries(item);
            }
            catch
            {
                base.RemoveItem(index);
                throw;
            }
        }

        /// <summary>
        /// Override. (Inherited from Collection&lt;T>"/>
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        protected override void RemoveItem(int index)
        {
            SecurityTokenHandler removedItem = Items[index];
            base.RemoveItem(index);
            RemoveFromDictionaries(removedItem);
        }

        /// <summary>
        /// Override. (Inherited from Collection&lt;T>"/>
        /// </summary>
        /// <param name="index">The zero-based index of the element to replace.</param>
        /// <param name="item">The new value for the element at the specified index. The value can be null for reference types.</param>
        protected override void SetItem(int index, SecurityTokenHandler item)
        {
            SecurityTokenHandler replaced = Items[index];
            base.SetItem(index, item);

            RemoveFromDictionaries(replaced);

            try
            {
                AddToDictionaries(item);
            }
            catch
            {
                base.SetItem(index, replaced);
                AddToDictionaries(replaced);
                throw;
            }
        }

        public bool CanReadKeyIdentifierClause(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            return CanReadKeyIdentifierClauseCore(reader);
        }

        /// <summary>
        /// Checks if the wrapped SecurityTokenHandler or the base WSSecurityTokenSerializer can read the 
        /// SecurityKeyIdentifierClause.
        /// </summary>
        /// <param name=nameof(reader)>Reader to a SecurityKeyIdentifierClause.</param>
        /// <returns>'True' if the SecurityKeyIdentifierCause can be read.</returns>
        /// <exception cref="ArgumentNullException">The input parameter 'reader' is null.</exception>
        protected virtual bool CanReadKeyIdentifierClauseCore(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            foreach (SecurityTokenHandler securityTokenHandler in this)
            {
                if (securityTokenHandler.CanReadKeyIdentifierClause(reader))
                {
                    return true;
                }
            }

            return false;
        }

        public SecurityKeyIdentifierClause ReadKeyIdentifierClause(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            return ReadKeyIdentifierClauseCore(reader);
        }

        /// <summary>
        /// Deserializes a SecurityKeyIdentifierClause from the given reader.
        /// </summary>
        /// <param name=nameof(reader)>XmlReader to a SecurityKeyIdentifierClause.</param>
        /// <returns>The deserialized SecurityKeyIdentifierClause.</returns>
        /// <exception cref="ArgumentNullException">The input parameter 'reader' is null.</exception>
        protected virtual SecurityKeyIdentifierClause ReadKeyIdentifierClauseCore(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            foreach (SecurityTokenHandler securityTokenHandler in this)
            {
                if (securityTokenHandler.CanReadKeyIdentifierClause(reader))
                {
                    return securityTokenHandler.ReadKeyIdentifierClause(reader);
                }
            }

            return _keyInfoSerializer.ReadKeyIdentifierClause(reader);
        }

        public void WriteKeyIdentifierClause(XmlWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }

            WriteKeyIdentifierClauseCore(writer, keyIdentifierClause);
        }

        /// <summary>
        /// Serializes the given SecurityKeyIdentifierClause in a XmlWriter.
        /// </summary>
        /// <param name="writer">XmlWriter to write into.</param>
        /// <param name="keyIdentifierClause">SecurityKeyIdentifierClause to be written.</param>
        /// <exception cref="ArgumentNullException">The input parameter 'writer' or 'keyIdentifierClause' is null.</exception>
        protected virtual void WriteKeyIdentifierClauseCore(XmlWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }

            foreach (SecurityTokenHandler securityTokenHandler in this)
            {
                if (securityTokenHandler.CanWriteKeyIdentifierClause(keyIdentifierClause))
                {
                    securityTokenHandler.WriteKeyIdentifierClause(writer, keyIdentifierClause);
                    return;
                }
            }

            _keyInfoSerializer.WriteKeyIdentifierClause(writer, keyIdentifierClause);
        }

        private void AddToDictionaries(SecurityTokenHandler handler)
        {
            if (handler == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("handler");
            }

            bool firstSucceeded = false;

            string[] identifiers = handler.GetTokenTypeIdentifiers();
            if (identifiers != null)
            {
                foreach (string typeId in identifiers)
                {
                    if (typeId != null)
                    {
                        _handlersByIdentifier.Add(typeId, handler);
                        firstSucceeded = true;
                    }
                }
            }

            Type type = handler.TokenType;
            if (handler.TokenType != null)
            {
                try
                {
                    _handlersByType.Add(type, handler);
                }
                catch
                {
                    if (firstSucceeded)
                    {
                        RemoveFromDictionaries(handler);
                    }

                    throw;
                }
            }

            // Ensure that the handler knows which collection it is in.
            handler.ContainingCollection = this;

            // Propagate this collection's STH configuration to the handler
            // if the handler's configuration is unset.
            if (handler.Configuration == null)
            {
                handler.Configuration = Configuration;
            }
        }

        private void RemoveFromDictionaries(SecurityTokenHandler handler)
        {
            string[] identifiers = handler.GetTokenTypeIdentifiers();
            if (identifiers != null)
            {
                foreach (string typeId in identifiers)
                {
                    if (typeId != null)
                    {
                        _handlersByIdentifier.Remove(typeId);
                    }
                }
            }

            Type type = handler.TokenType;
            if (type != null && _handlersByType.ContainsKey(type))
            {
                _handlersByType.Remove(type);
            }

            // Ensure that the handler knows that it is no longer
            // in a collection.
            handler.ContainingCollection = null;
            handler.Configuration = null;
        }
    }
}
