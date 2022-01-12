// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    public class MessagePartSpecification
    {
        private List<XmlQualifiedName> _headerTypes;
        private bool _isBodyIncluded;
        private static MessagePartSpecification s_noParts;

        public ICollection<XmlQualifiedName> HeaderTypes
        {
            get
            {
                if (_headerTypes == null)
                {
                    _headerTypes = new List<XmlQualifiedName>();
                }

                if (IsReadOnly)
                {
                    return new ReadOnlyCollection<XmlQualifiedName>(_headerTypes);
                }
                else
                {
                    return _headerTypes;
                }
            }
        }

        internal bool HasHeaders
        {
            get { return _headerTypes != null && _headerTypes.Count > 0; }
        }

        public bool IsBodyIncluded
        {
            get
            {
                return _isBodyIncluded;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
                }

                _isBodyIncluded = value;
            }
        }

        public bool IsReadOnly { get; private set; }

        public static MessagePartSpecification NoParts
        {
            get
            {
                if (s_noParts == null)
                {
                    MessagePartSpecification parts = new MessagePartSpecification();
                    parts.MakeReadOnly();
                    s_noParts = parts;
                }
                return s_noParts;
            }
        }

        public void Clear()
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            if (_headerTypes != null)
            {
                _headerTypes.Clear();
            }

            _isBodyIncluded = false;
        }

        public void Union(MessagePartSpecification specification)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }

            if (specification == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(specification));
            }

            _isBodyIncluded |= specification.IsBodyIncluded;

            List<XmlQualifiedName> headerTypes = specification._headerTypes;
            if (headerTypes != null && headerTypes.Count > 0)
            {
                if (_headerTypes == null)
                {
                    _headerTypes = new List<XmlQualifiedName>(headerTypes.Count);
                }

                for (int i = 0; i < headerTypes.Count; i++)
                {
                    XmlQualifiedName qname = headerTypes[i];
                    _headerTypes.Add(qname);
                }
            }
        }

        public void MakeReadOnly()
        {
            if (IsReadOnly)
            {
                return;
            }

            if (_headerTypes != null)
            {
                List<XmlQualifiedName> noDuplicates = new List<XmlQualifiedName>(_headerTypes.Count);
                for (int i = 0; i < _headerTypes.Count; i++)
                {
                    XmlQualifiedName qname = _headerTypes[i];
                    if (qname != null)
                    {
                        bool include = true;
                        for (int j = 0; j < noDuplicates.Count; j++)
                        {
                            XmlQualifiedName qname1 = noDuplicates[j];

                            if (qname.Name == qname1.Name && qname.Namespace == qname1.Namespace)
                            {
                                include = false;
                                break;
                            }
                        }

                        if (include)
                        {
                            noDuplicates.Add(qname);
                        }
                    }
                }

                _headerTypes = noDuplicates;
            }

            IsReadOnly = true;
        }

        public MessagePartSpecification()
        {
            // empty
        }

        public MessagePartSpecification(bool isBodyIncluded)
        {
            _isBodyIncluded = isBodyIncluded;
        }

        public MessagePartSpecification(params XmlQualifiedName[] headerTypes)
            : this(false, headerTypes)
        {
            // empty
        }

        public MessagePartSpecification(bool isBodyIncluded, params XmlQualifiedName[] headerTypes)
        {
            _isBodyIncluded = isBodyIncluded;
            if (headerTypes != null && headerTypes.Length > 0)
            {
                _headerTypes = new List<XmlQualifiedName>(headerTypes.Length);
                for (int i = 0; i < headerTypes.Length; i++)
                {
                    _headerTypes.Add(headerTypes[i]);
                }
            }
        }

        internal bool IsHeaderIncluded(MessageHeader header)
        {
            if (header == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(header));
            }

            return IsHeaderIncluded(header.Name, header.Namespace);
        }

        internal bool IsHeaderIncluded(string name, string ns)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }

            if (_headerTypes != null)
            {
                for (int i = 0; i < _headerTypes.Count; i++)
                {
                    XmlQualifiedName qname = _headerTypes[i];
                    // Name is an optional attribute. If not present, compare with only the namespace.
                    if (string.IsNullOrEmpty(qname.Name))
                    {
                        if (qname.Namespace == ns)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (qname.Name == name && qname.Namespace == ns)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal bool IsEmpty()
        {
            if (_headerTypes != null && _headerTypes.Count > 0)
            {
                return false;
            }

            return !IsBodyIncluded;
        }
    }
}
