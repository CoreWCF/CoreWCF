# MtomMessageEncodingElement — XML configuration for MTOM encoding

**Source:** `src/CoreWCF.ConfigurationManager/src/CoreWCF/Configuration/MtomMessageEncodingElement.cs`
**Namespace:** `CoreWCF.Configuration`
**Last validated:** 2026-11-06

## Purpose

This `BindingElementExtensionElement` maps the `<mtomMessageEncoding>` configuration section to `MtomMessageEncodingBindingElement`.

## Key API

- `ApplyConfiguration` transfers XML properties to the binding element.
- `CopyFrom` copies settings between configuration elements.
- `InitializeFrom` writes non-default binding settings into the configuration representation.
- `MaxMimeParts` maps the positive integer `maxMimeParts` attribute, defaulting to 1000 (also represented in `ConfigurationDefaults.cs`).
