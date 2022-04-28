// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Represents a serializable version of a token that can be attached to a <see cref="System.Security.Claims.ClaimsIdentity"/> to retain the 
    /// original token that was used to create <see cref="System.Security.Claims.ClaimsIdentity"/>
    /// </summary>
    [Serializable]
    public class BootstrapContext : ISerializable
    {
        private readonly SecurityToken _token;
        private readonly string _tokenString;
        private readonly byte[] _tokenBytes;
        private readonly SecurityTokenHandler _tokenHandler;
        private const string TokenTypeKey = "K";
        private const string TokenKey = "T";
        private const char SecurityTokenType = 'T';
        private const char StringTokenType = 'S';
        private const char ByteTokenType = 'B';

        protected BootstrapContext(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                return;

            switch (info.GetChar(TokenTypeKey))
            {
                case SecurityTokenType:
                    {
                        if (context.Context is SecurityTokenHandler sth)
                        {
                            using (XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(Convert.FromBase64String(info.GetString(TokenKey)), XmlDictionaryReaderQuotas.Max))
                            {
                                reader.MoveToContent();
                                if (sth.CanReadToken(reader))
                                {
                                    SecurityToken token = sth.ReadToken(reader);
                                    if (token == null)
                                    {
                                        _tokenString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(info.GetString(TokenKey)));
                                    }
                                    else
                                    {
                                        _token = token;
                                    }
                                }
                            }
                        }
                        else
                        {
                            _tokenString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(info.GetString(TokenKey)));
                        }
                    }

                    break;

                case StringTokenType:
                    {
                        _tokenString = info.GetString(TokenKey);
                    }
                    break;

                case ByteTokenType:
                    {
                        _tokenBytes = (byte[])info.GetValue(TokenKey, typeof(byte[]));
                    }
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// A SecurityToken and a SecurityTokenHandler that can serialize the token.
        /// </summary>
        /// <param name=nameof(token)><see cref="SecurityToken"/> that can be serialized. Cannot be null.</param>
        /// <param name="tokenHandler"><see cref="SecurityTokenHandler"/> that is responsible for serializing the token. Cannon be null.</param>
        /// <exception cref="ArgumentNullException"> thrown if 'token' or 'tokenHandler' is null.</exception>
        /// <remarks>The <see cref="SecurityTokenHandler"/> is used not used to deserialize the token as it cannot be assumed to exist</remarks>
        public BootstrapContext(SecurityToken token, SecurityTokenHandler tokenHandler)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _tokenHandler = tokenHandler ?? throw new ArgumentNullException(nameof(tokenHandler));
        }

        /// <summary>
        /// String that represents a SecurityToken.
        /// </summary>
        /// <param name=nameof(token)>string that represents a token.  Can not be null.</param>
        /// <exception cref="ArgumentNullException"> thrown if 'token' is null.</exception>
        public BootstrapContext(string token)
        {
            _tokenString = token ?? throw new ArgumentNullException(nameof(token));
        }

        /// <summary>
        /// String that represents a SecurityToken.
        /// </summary>
        /// <param name=nameof(token)>string that represents a token.  Can not be null.</param>
        /// <exception cref="ArgumentNullException"> thrown if 'token' is null.</exception>
        public BootstrapContext(byte[] token)
        {
            _tokenBytes = token ?? throw new ArgumentNullException(nameof(token));
        }

        #region ISerializable Members
        /// <summary>
        /// Called to serialize this context.
        /// </summary>
        /// <param name="info"><see cref="SerializationInfo"/> container for storing data. Cannot be null.</param>
        /// <param name="context"><see cref="StreamingContext"/> contains the context for streaming and optionally additional user data.</param>
        /// <exception cref="ArgumentNullException"> thrown if 'info' is null.</exception>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (_tokenBytes != null)
            {
                info.AddValue(TokenTypeKey, ByteTokenType);
                info.AddValue(TokenKey, _tokenBytes);
            }
            else if (_tokenString != null)
            {
                info.AddValue(TokenTypeKey, StringTokenType);
                info.AddValue(TokenKey, _tokenString);
            }
            else if (_token != null && _tokenHandler != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    info.AddValue(TokenTypeKey, SecurityTokenType);
                    using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(ms, System.Text.Encoding.UTF8, false))
                    {
                        _tokenHandler.WriteToken(writer, _token);
                        writer.Flush();
                        info.AddValue(TokenKey, Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length));
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Gets the string that was passed in constructor. If a different constructor was used, will be null.
        /// </summary>
        public byte[] TokenBytes
        {
            get { return _tokenBytes; }
        }

        /// <summary>
        /// Gets the string that was passed in constructor. If a different constructor was used, will be null.
        /// </summary>
        public string Token
        {
            get { return _tokenString; }
        }

        /// <summary>
        /// Gets the SecurityToken that was passed in constructor. If a different constructor was used, will be null.
        /// </summary>
        public SecurityToken SecurityToken
        {
            get { return _token; }
        }

        /// <summary>
        /// Gets the SecurityTokenHandler that was passed in constructor. If a different constructor was used, will be null.
        /// </summary>
        public SecurityTokenHandler SecurityTokenHandler
        {
            get { return _tokenHandler; }
        }
    }
}
