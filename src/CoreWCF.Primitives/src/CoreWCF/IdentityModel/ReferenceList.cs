// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using DictionaryManager = CoreWCF.IdentityModel.DictionaryManager;
using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;

namespace CoreWCF.Security
{
    internal sealed class ReferenceList : ISecurityElement
    {
        internal static readonly XmlDictionaryString ElementName = XD.XmlEncryptionDictionary.ReferenceList;
        private const string NamespacePrefix = XmlEncryptionStrings.Prefix;
        internal static readonly XmlDictionaryString NamespaceUri = XD.XmlEncryptionDictionary.Namespace;// EncryptedType.NamespaceUri;
        internal static readonly XmlDictionaryString UriAttribute = XD.XmlEncryptionDictionary.URI;
        private readonly List<string> referredIds = new List<string>();

        public ReferenceList()
        {
        }

        public int DataReferenceCount => referredIds.Count;

        public bool HasId => false;

        public string Id => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());

        public void AddReferredId(string id)
        {
            if (id == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("id"));
            }
            referredIds.Add(id);
        }

        public bool ContainsReferredId(string id)
        {
            if (id == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("id"));
            }
            return referredIds.Contains(id);
        }

        public string GetReferredId(int index)
        {
            return referredIds[index];
        }

        public void ReadFrom(XmlDictionaryReader reader)
        {
            reader.ReadStartElement(ElementName, NamespaceUri);
            while (reader.IsStartElement())
            {
                string id = DataReference.ReadFrom(reader);
                if (referredIds.Contains(id))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new Exception(SR.Format("InvalidDataReferenceInReferenceList", "#" + id)));
                }
                referredIds.Add(id);
            }
            reader.ReadEndElement(); // ReferenceList
            if (DataReferenceCount == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new Exception(SR.Format("ReferenceListCannotBeEmpty")));
            }
        }

        public bool TryRemoveReferredId(string id)
        {
            if (id == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("id"));
            }
            return referredIds.Remove(id);
        }

        public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            if (DataReferenceCount == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format("ReferenceListCannotBeEmpty")));
            }
            writer.WriteStartElement(NamespacePrefix, ElementName, NamespaceUri);
            for (int i = 0; i < DataReferenceCount; i++)
            {
                DataReference.WriteTo(writer, referredIds[i]);
            }
            writer.WriteEndElement(); // ReferenceList
        }

        private static class DataReference
        {
            internal static readonly XmlDictionaryString ElementName = XD.XmlEncryptionDictionary.DataReference;
            internal static readonly XmlDictionaryString NamespaceUri = XD.XmlEncryptionDictionary.Namespace;

            public static string ReadFrom(XmlDictionaryReader reader)
            {
                throw new NotImplementedException();
                //string prefix;
                //string uri = XmlHelper.ReadEmptyElementAndRequiredAttribute(reader, ElementName, NamespaceUri, ReferenceList.UriAttribute, out prefix);
                //if (uri.Length < 2 || uri[0] != '#')
                //{
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                //        new SecurityMessageSerializationException(SR.GetString(SR.InvalidDataReferenceInReferenceList, uri)));
                //}
                //return uri.Substring(1);
            }

            public static void WriteTo(XmlDictionaryWriter writer, string referredId)
            {
                writer.WriteStartElement(XD.XmlEncryptionDictionary.Prefix.Value, ElementName, NamespaceUri);
                writer.WriteStartAttribute(ReferenceList.UriAttribute, null);
                writer.WriteString("#");
                writer.WriteString(referredId);
                writer.WriteEndAttribute();
                writer.WriteEndElement();
            }
        }
    }
}
