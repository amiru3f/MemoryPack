﻿namespace MemoryPack.Formatters;

public sealed class KeyValuePairFormatter<TKey, TValue> : MemoryPackFormatter<KeyValuePair<TKey?, TValue?>>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref KeyValuePair<TKey?, TValue?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            writer.DangerousWriteUnmanaged(value);
            return;
        }

        writer.WriteObjectHeader(2);
        writer.WriteValue(value.Key);
        writer.WriteValue(value.Value);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref KeyValuePair<TKey?, TValue?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            reader.DangerousReadUnmanaged(out value);
            return;
        }

        if (!reader.TryReadObjectHeader(out var count))
        {
            value = default;
            return;
        }

        if (count != 2) MemoryPackSerializationException.ThrowInvalidPropertyCount(2, count);

        value = new KeyValuePair<TKey?, TValue?>(
            reader.ReadValue<TKey>(),
            reader.ReadValue<TValue>()
        );
    }
}
