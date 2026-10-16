// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;

// Byte-budgeted least-recently-used cache. Kept free of Godot types so the
// eviction policy can be tested by the plain .NET fixture project.
public sealed class Wa2LruCache<TValue> where TValue : class
{
    private sealed class Entry
    {
        public string Key;
        public TValue Value;
        public long Bytes;
    }

    private readonly Dictionary<string, LinkedListNode<Entry>> _map = new();
    private readonly LinkedList<Entry> _order = new();
    private readonly long _budget;

    public Wa2LruCache(long budgetBytes)
    {
        if (budgetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
        _budget = budgetBytes;
    }

    public long Bytes { get; private set; }
    public int Count => _map.Count;

    public bool TryGet(string key, out TValue value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
        value = null;
        return false;
    }

    // Items larger than the whole budget are not retained.
    public void Add(string key, TValue value, long bytes)
    {
        if (_map.TryGetValue(key, out var existing)) Remove(existing);
        if (bytes > _budget) return;
        var node = _order.AddFirst(new Entry { Key = key, Value = value, Bytes = bytes });
        _map[key] = node;
        Bytes += bytes;
        while (Bytes > _budget && _order.Last != null) Remove(_order.Last);
    }

    public void Clear()
    {
        _map.Clear();
        _order.Clear();
        Bytes = 0;
    }

    private void Remove(LinkedListNode<Entry> node)
    {
        _order.Remove(node);
        _map.Remove(node.Value.Key);
        Bytes -= node.Value.Bytes;
    }
}
