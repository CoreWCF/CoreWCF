// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;

namespace CoreWCF.IdentityModel
{
    internal class IdentityModelDictionary : IXmlDictionary
    {
        public static readonly IdentityModelDictionary Version1 = new IdentityModelDictionary(new IdentityModelStringsVersion1());
        private readonly IdentityModelStrings _strings;
        private readonly int _count;
        private XmlDictionaryString[] _dictionaryStrings;
        private Dictionary<string, int> _dictionary;
        private XmlDictionaryString[] _versionedDictionaryStrings;

        public IdentityModelDictionary(IdentityModelStrings strings)
        {
            _strings = strings;
            _count = strings.Count;
        }

        public static IdentityModelDictionary CurrentVersion
        {
            get
            {
                return Version1;
            }
        }

        public XmlDictionaryString CreateString(string value, int key)
        {
            return new XmlDictionaryString(this, value, key);
        }

        public bool TryLookup(string key, out XmlDictionaryString value)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(key)));
            }

            if (_dictionary == null)
            {
                Dictionary<string, int> dictionary = new Dictionary<string, int>(_count);
                for (int i = 0; i < _count; i++)
                {
                    dictionary.Add(_strings[i], i);
                }

                _dictionary = dictionary;
            }
            if (_dictionary.TryGetValue(key, out int id))
            {
                return TryLookup(id, out value);
            }

            value = null;
            return false;
        }

        public bool TryLookup(int key, out XmlDictionaryString value)
        {
            if (key < 0 || key >= _count)
            {
                value = null;
                return false;
            }
            if (_dictionaryStrings == null)
            {
                _dictionaryStrings = new XmlDictionaryString[_count];
            }

            XmlDictionaryString s = _dictionaryStrings[key];
            if (s == null)
            {
                s = CreateString(_strings[key], key);
                _dictionaryStrings[key] = s;
            }
            value = s;
            return true;
        }

        public bool TryLookup(XmlDictionaryString key, out XmlDictionaryString value)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(key)));
            }

            if (key.Dictionary == this)
            {
                value = key;
                return true;
            }
            if (key.Dictionary == CurrentVersion)
            {
                if (_versionedDictionaryStrings == null)
                {
                    _versionedDictionaryStrings = new XmlDictionaryString[CurrentVersion._count];
                }

                XmlDictionaryString s = _versionedDictionaryStrings[key.Key];
                if (s == null)
                {
                    if (!TryLookup(key.Value, out s))
                    {
                        value = null;
                        return false;
                    }
                    _versionedDictionaryStrings[key.Key] = s;
                }
                value = s;
                return true;
            }
            value = null;
            return false;
        }
    }
}
