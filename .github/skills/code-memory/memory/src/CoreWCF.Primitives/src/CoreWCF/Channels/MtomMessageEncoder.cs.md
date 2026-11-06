# MtomMessageEncoder — MTOM encoder factory and message encoding/decoding

**Source:** `src/CoreWCF.Primitives/src/CoreWCF/Channels/MtomMessageEncoder.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-11-06

## Purpose

`MtomMessageEncoderFactory` creates MTOM encoders and pools XML readers/writers. `MtomMessageEncoder` handles buffered and streamed SOAP messages; buffered reads use `MtomBufferedMessageData`, and streamed reads use the factory's pooled reader.

## Internal Structure

The factory retains encoder options and provides them to the encoder. On both reader initialization paths, `MaxBufferSize` and `MaxMimeParts` are passed to `XmlMtomReader`, including when a pooled reader is reused. Keep these paths aligned when changing reader quotas.

## Relationships

`MtomMessageEncodingBindingElement.CreateMessageEncoderFactory()` supplies the settings. `MtomMessageEncoderFactory.TakeStreamedReader` and `MtomBufferedMessageData.TakeXmlReader` initialize `XmlMtomReader` for incoming MTOM messages.
