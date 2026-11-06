# MtomMessageEncodingBindingElement — MTOM binding configuration and encoder factory creation

**Source:** `src/CoreWCF.Primitives/src/CoreWCF/Channels/MtomMessageEncodingBindingElement.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-11-06

## Purpose

The public binding element configures MTOM message encoding and creates the encoder factory consumed by HTTP transports. It owns message version, character encoding, reader quotas, pool sizes, buffer size, and MIME-part quota.

## Key API

- `MaxMimeParts` defaults to `MtomEncoderDefaults.MaxMimeParts` (1000) and rejects values less than 1.
- `Clone()` preserves all MTOM-specific settings, including the MIME-part quota.
- `CreateMessageEncoderFactory()` passes the configured quota to `MtomMessageEncoderFactory`.

## Design Notes

`IsMatch` compares encoder settings and reader quotas; keep it in sync when adding binding-element properties. Defaults are declared in `TransportDefaults.cs`.
