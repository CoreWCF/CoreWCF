// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF
{
    internal static class JsonGlobals
    {
        public static readonly XmlDictionaryString DDictionaryString = new XmlDictionary().Add("d");
        public static readonly XmlDictionaryString ItemDictionaryString = new XmlDictionary().Add("item");
        public static readonly XmlDictionaryString RootDictionaryString = new XmlDictionary().Add("root");
        public const string ApplicationJsonMediaType = "application/json";
        public const string TextJsonMediaType = "text/json";
        public const string DString = "d";
        public const string ItemString = "item";
        public const string NullString = "null";
        public const string ObjectString = "object";
        public const string RootString = "root";
        public const string TypeString = "type";
    }
}
