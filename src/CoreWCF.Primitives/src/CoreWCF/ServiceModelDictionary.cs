// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Xml;

namespace CoreWCF
{
    internal class ServiceModelDictionary : IXmlDictionary
    {
        static public readonly ServiceModelDictionary Version1 = new ServiceModelDictionary(new ServiceModelStringsVersion1());
        readonly ServiceModelStrings _strings;
        readonly int _count;
        XmlDictionaryString[] _dictionaryStrings1;
        XmlDictionaryString[] _dictionaryStrings2;
        Dictionary<string, int> _dictionary;
        XmlDictionaryString[] _versionedDictionaryStrings;

        public ServiceModelDictionary(ServiceModelStrings strings)
        {
            _strings = strings;
            _count = strings.Count;
        }

        static public ServiceModelDictionary CurrentVersion
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
            if (_dictionary == null)
            {
                Dictionary<string, int> dictionary = new Dictionary<string, int>(_count);
                for (int i = 0; i < _count; i++)
                    dictionary.Add(_strings[i], i);
                _dictionary = dictionary;
            }
            int id;
            if (_dictionary.TryGetValue(key, out id))
                return TryLookup(id, out value);
            value = null;
            return false;
        }

        public bool TryLookup(int key, out XmlDictionaryString value)
        {
            const int keyThreshold = 32;
            if (key < 0 || key >= _count)
            {
                value = null;
                return false;
            }
            XmlDictionaryString s;
            if (key < keyThreshold)
            {
                if (_dictionaryStrings1 == null)
                    _dictionaryStrings1 = new XmlDictionaryString[keyThreshold];
                s = _dictionaryStrings1[key];
                if (s == null)
                {
                    s = CreateString(_strings[key], key);
                    _dictionaryStrings1[key] = s;
                }
            }
            else
            {
                if (_dictionaryStrings2 == null)
                    _dictionaryStrings2 = new XmlDictionaryString[_count - keyThreshold];
                s = _dictionaryStrings2[key - keyThreshold];
                if (s == null)
                {
                    s = CreateString(_strings[key], key);
                    _dictionaryStrings2[key - keyThreshold] = s;
                }
            }
            value = s;
            return true;
        }

        public bool TryLookup(XmlDictionaryString key, out XmlDictionaryString value)
        {
            if (key == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
            if (key.Dictionary == this)
            {
                value = key;
                return true;
            }
            if (key.Dictionary == CurrentVersion)
            {
                if (_versionedDictionaryStrings == null)
                    _versionedDictionaryStrings = new XmlDictionaryString[CurrentVersion._count];
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