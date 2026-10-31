// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;

namespace CoreWCF.Channels
{
    /// <summary>
    /// Chains memory into a <see cref="ReadOnlySequence{T}"/> so that data arriving in pieces can be
    /// presented as one sequence without being copied into a single buffer.
    /// </summary>
    internal sealed class SequenceBuilder<T>
    {
        private readonly MemorySegment _first;
        private MemorySegment _last;

        public SequenceBuilder(ReadOnlyMemory<T> memory)
        {
            _last = _first = new MemorySegment(memory);
        }

        public SequenceBuilder<T> Append(ReadOnlyMemory<T> memory)
        {
            _last = _last.Append(memory);
            return this;
        }

        public SequenceBuilder<T> Append(ReadOnlySequence<T> readOnlySequence)
        {
            foreach (ReadOnlyMemory<T> memory in readOnlySequence)
            {
                _last = _last.Append(memory);
            }

            return this;
        }

        public ReadOnlySequence<T> Build() => new(_first, 0, _last, _last.Memory.Length);

        private sealed class MemorySegment : ReadOnlySequenceSegment<T>
        {
            public MemorySegment(ReadOnlyMemory<T> memory)
            {
                Memory = memory;
            }

            public MemorySegment Append(ReadOnlyMemory<T> memory)
            {
                var segment = new MemorySegment(memory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };

                Next = segment;

                return segment;
            }
        }
    }
}
