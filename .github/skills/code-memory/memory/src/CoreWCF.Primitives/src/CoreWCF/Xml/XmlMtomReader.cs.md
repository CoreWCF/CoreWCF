# XmlMtomReader — MTOM multipart XML reader

**Source:** `src/CoreWCF.Primitives/src/CoreWCF/Xml/XmlMtomReader.cs`
**Namespace:** `CoreWCF.Xml`
**Last validated:** 2026-11-06

## Purpose

`XmlMtomReader` parses the multipart MIME envelope, reads the root XOP infoset, and resolves `xop:Include` elements to MIME attachment streams. It is internal and initialized through `IXmlMtomReaderInitializer` for pooled readers or static `Create` helpers.

## Internal Structure

`Initialize` configures MIME parsing and stores the buffer and MIME-part quotas. `ReadMimePart` locates content IDs, caches encountered parts, and throws once the cached part count exceeds `_maxMimeParts`. Existing helper overloads without an explicit MIME quota retain the default of 1000.

## Design Notes

Both stream and byte-buffer initialization paths must receive the current encoder's limit, including on reader reuse. The quota counts MIME parts entered in the content-ID cache.
