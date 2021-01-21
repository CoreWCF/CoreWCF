// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;

namespace CoreWCF
{
    internal class ServiceModelDictionary : IXmlDictionary
    {
        static public readonly ServiceModelDictionary Version1 = new ServiceModelDictionary(new ServiceModelStringsVersion1());
        private readonly ServiceModelStrings strings;
        private readonly int count;
        private XmlDictionaryString[] dictionaryStrings1;
        private XmlDictionaryString[] dictionaryStrings2;
        private Dictionary<string, int> dictionary;
        private XmlDictionaryString[] versionedDictionaryStrings;

        public ServiceModelDictionary(ServiceModelStrings strings)
        {
            this.strings = strings;
            count = strings.Count;
        }

        static public ServiceModelDictionary CurrentVersion => Version1;

        public XmlDictionaryString CreateString(string value, int key) => new XmlDictionaryString(this, value, key);

        public bool TryLookup(string key, out XmlDictionaryString value)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(key)));
            }

            if (dictionary == null)
            {
                Dictionary<string, int> dictionary = new Dictionary<string, int>(count);
                for (int i = 0; i < count; i++)
                {
                    dictionary.Add(strings[i], i);
                }

                this.dictionary = dictionary;
            }
            if (dictionary.TryGetValue(key, out int id))
            {
                return TryLookup(id, out value);
            }

            value = null;
            return false;
        }

        public bool TryLookup(int key, out XmlDictionaryString value)
        {
            const int keyThreshold = 32;
            if (key < 0 || key >= count)
            {
                value = null;
                return false;
            }
            XmlDictionaryString s;
            if (key < keyThreshold)
            {
                if (dictionaryStrings1 == null)
                {
                    dictionaryStrings1 = new XmlDictionaryString[keyThreshold];
                }

                s = dictionaryStrings1[key];
                if (s == null)
                {
                    s = CreateString(strings[key], key);
                    dictionaryStrings1[key] = s;
                }
            }
            else
            {
                if (dictionaryStrings2 == null)
                {
                    dictionaryStrings2 = new XmlDictionaryString[count - keyThreshold];
                }

                s = dictionaryStrings2[key - keyThreshold];
                if (s == null)
                {
                    s = CreateString(strings[key], key);
                    dictionaryStrings2[key - keyThreshold] = s;
                }
            }
            value = s;
            return true;
        }

        public bool TryLookup(XmlDictionaryString key, out XmlDictionaryString value)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("key"));
            }

            if (key.Dictionary == this)
            {
                value = key;
                return true;
            }
            if (key.Dictionary == CurrentVersion)
            {
                if (versionedDictionaryStrings == null)
                {
                    versionedDictionaryStrings = new XmlDictionaryString[CurrentVersion.count];
                }

                XmlDictionaryString s = versionedDictionaryStrings[key.Key];
                if (s == null)
                {
                    if (!TryLookup(key.Value, out s))
                    {
                        value = null;
                        return false;
                    }
                    versionedDictionaryStrings[key.Key] = s;
                }
                value = s;
                return true;
            }
            value = null;
            return false;
        }
    }
}
