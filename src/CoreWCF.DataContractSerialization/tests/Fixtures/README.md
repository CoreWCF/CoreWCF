# Golden fixtures

Each file records the **exact bytes** CoreWCF's reflection-based `DataContractSerializer` writes for
one corpus case. They are the oracle the source-generated serializer will be diffed against, so they
are compared byte for byte with no canonicalisation.

Do not hand-edit them. Regenerate instead - see
[`../../README.md`](../../README.md) for the commands, the determinism rules and the per-framework
override mechanism.

## Format

UTF-8, no byte-order mark, no XML declaration, no indentation, no trailing newline, one line.
`FixtureWriterTests` pins every one of those properties.

They diff poorly. That is an accepted trade: byte fidelity is the whole point, and a mismatch is
triaged from the test failure message - which pretty-prints both sides and reports the first
differing byte offset - rather than from `git diff`.

## The two dotfiles here are load-bearing

- `.gitattributes` marks fixtures `-text`. The repository root sets `* text=auto`, which would
  rewrite a raw `0x0A` byte inside a serialized string value on Windows checkout. That is a silent
  corruption: it passes on Linux CI and fails on Windows.
- `.editorconfig` disables `insert_final_newline`, which `src/.editorconfig` turns on globally. Any
  editor that opened and saved a fixture would otherwise append a byte.

## Subdirectories

A `net472/` or `net10.0/` directory holds a fixture **only** where that framework's output genuinely
differs from the `net8.0` baseline; regeneration deletes overrides that have become redundant. If
one appears in a diff, find out which runtime changed before assuming it is a bug.
