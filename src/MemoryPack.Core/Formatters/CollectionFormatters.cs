﻿using MemoryPack.Internal;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MemoryPack.Formatters;

// IEunmerable collection formatters
// List, LinkedList, Queue, Stack, HashSet, ReadOnlyCollection, PriorityQueue,
// ObservableCollection, ReadOnlyObservableCollection
// ConcurrentQueue, ConcurrentStack, ConcurrentBag, BlockingCollection
// IEnumerable, ICollection, IReadOnlyCollection, IList, IReadOnlyList

// TODO:impl collections

public sealed class ListFormatter<T> : MemoryPackFormatter<List<T?>>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref List<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNullLengthHeader();
            return;
        }

        writer.WriteSpan(CollectionsMarshal.AsSpan(value));
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref List<T?>? value)
    {
        if (!reader.TryReadLengthHeader(out var length))
        {
            value = null;
            return;
        }

        if (value == null)
        {
            value = new List<T?>(length);
        }
        else if (value.Count == length)
        {
            // same length, can use Span method
            var span = CollectionsMarshal.AsSpan(value);
            reader.ReadSpan(ref span);
            return;
        }
        else
        {
            // List<T> supports clear
            value.Clear();
        }

        var formatter = MemoryPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? v = default;
            formatter.Deserialize(ref reader, ref v);
            value.Add(v);
        }
    }
}

public sealed class StackFormatter<T> : MemoryPackFormatter<Stack<T?>>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Stack<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNullLengthHeader();
            return;
        }


        // TODO:reverse order?
        var formatter = MemoryPackFormatterProvider.GetFormatter<T>();

        writer.WriteLengthHeader(value.Count);
        foreach (var item in value)
        {
            var v = item;
            formatter.Serialize(ref writer, ref v);
        }
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Stack<T?>? value)
    {
        throw new NotImplementedException();
    }
}


public sealed class CollectionFormatter<T> : MemoryPackFormatter<IReadOnlyCollection<T?>>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref IReadOnlyCollection<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNullLengthHeader();
            return;
        }

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            if (value is T?[] array) // value is T[] ???
            {
                writer.DangerousWriteUnmanagedArray(array!); // nullable? ok?
                return;
            }
            else if (value is List<T?> list)
            {
                ReadOnlySpan<T> span = CollectionsMarshal.AsSpan(list);
                writer.DangerousWriteUnmanagedSpan<T>(span);
                return;
            }
        }

        writer.WriteLengthHeader(value.Count);
        if (value.Count != 0)
        {
            var formatter = MemoryPackFormatterProvider.GetFormatter<T>();
            foreach (var item in value)
            {
                var v = item;
                formatter.Serialize(ref writer, ref v);
            }
        }
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref IReadOnlyCollection<T?>? value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            value = reader.DangerousReadUnmanagedArray<T>();
            return;
        }





        if (!reader.TryReadLengthHeader(out var length))
        {
            value = null;
            return;
        }

        if (length == 0)
        {
            value = Array.Empty<T>();
            return;
        }

        // context.TryReadUnmanagedSpan<

        // TODO: security check
        var formatter = MemoryPackFormatterProvider.GetFormatter<T>();// TODO:direct?
        var collection = new T?[length];
        for (int i = 0; i < length; i++)
        {
            // TODO: read item
            formatter.Deserialize(ref reader, ref collection[i]);
        }

        value = collection;
    }
}

public sealed class EnumerableFormatter<T> : MemoryPackFormatter<IEnumerable<T>>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref IEnumerable<T>? value)
    {
        if (value == null)
        {
            writer.WriteNullLengthHeader();
            return;
        }

        if (TryGetNonEnumeratedCount(value, out var count))
        {
            writer.WriteLengthHeader(count);
            foreach (var item in value)
            {
                // TODO: write item
            }
        }
        else
        {
            var tempWriter = ReusableLinkedArrayBufferWriterPool.Rent();
            try
            {
                var tempContext = new MemoryPackWriter<ReusableLinkedArrayBufferWriter>(ref tempWriter);

                foreach (var item in value)
                {
                    // TODO: write item to tempContext
                }

                tempContext.Flush();

                writer.WriteLengthHeader(tempWriter.TotalWritten);
                tempWriter.WriteToAndReset(ref writer);
            }
            finally
            {
                tempWriter.Reset();
                ReusableLinkedArrayBufferWriterPool.Return(tempWriter);
            }
        }
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref IEnumerable<T>? value)
    {
        // TODO:...
        throw new NotImplementedException();
    }

    static bool TryGetNonEnumeratedCount(IEnumerable<T> value, out int count)
    {
        // TryGetNonEnumeratedCount is not check IReadOnlyCollection<T> so add check manually.
        // https://github.com/dotnet/runtime/issues/54764

        if (value.TryGetNonEnumeratedCount(out count))
        {
            return true;
        }

        if (value is IReadOnlyCollection<T> readOnlyCollection)
        {
            count = readOnlyCollection.Count;
            return true;
        }

        return false;
    }
}

public class DictionaryFormatter<TKey, TValue> : MemoryPackFormatter<Dictionary<TKey, TValue?>>
    where TKey : notnull
{
    IEqualityComparer<TKey>? equalityComparer;

    public DictionaryFormatter()
    {

    }

    public DictionaryFormatter(IEqualityComparer<TKey> equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Dictionary<TKey, TValue?>? value)
    {
        if (value == null)
        {
            writer.WriteNullLengthHeader();
            return;
        }

        writer.WriteLengthHeader(value.Count);

        var keyFormatter = MemoryPackFormatterProvider.GetFormatter<TKey>();
        var valueFormatter = MemoryPackFormatterProvider.GetFormatter<TValue?>();

        foreach (var item in value)
        {
            keyFormatter.Serialize(ref writer, ref Unsafe.AsRef(item.Key)!);
            valueFormatter.Serialize(ref writer, ref Unsafe.AsRef(item.Value));
        }
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Dictionary<TKey, TValue?>? value)
    {
        if (!reader.TryReadLengthHeader(out var length))
        {
            value = null;
            return;
        }

        var keyFormatter = MemoryPackFormatterProvider.GetFormatter<TKey>();
        var valueFormatter = MemoryPackFormatterProvider.GetFormatter<TValue>();

        var dict = new Dictionary<TKey, TValue?>(length, equalityComparer);

        for (int i = 0; i < length; i++)
        {
            TKey? k = default;
            keyFormatter.Deserialize(ref reader, ref k);

            TValue? v = default;
            valueFormatter.Deserialize(ref reader, ref v);

            dict.Add(k!, v);
        }

        value = dict;
    }
}