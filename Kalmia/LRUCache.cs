using System.Collections.Generic;
using System.Collections.Specialized;

namespace Kalmia
{
    public class LRUCache<TKey, TValue>
    {
        readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> KEY_TO_NODE;
        readonly LinkedList<(TKey key, TValue value)> VALUES;
        readonly object LOCK_OBJ = new object();

        public int Capacity { get; }
        public int Count { get { return this.KEY_TO_NODE.Count; } }

        public TValue this[TKey key]
        {
            get
            {
                LinkedListNode<(TKey key, TValue value)> node;
                lock (this.LOCK_OBJ)
                {
                    node = this.KEY_TO_NODE[key];
                    this.VALUES.Remove(node);
                    this.VALUES.AddFirst(node);
                }
                return node.Value.value;
            }

            set
            {
                if (this.KEY_TO_NODE.ContainsKey(key))
                {
                    var node = this.KEY_TO_NODE[key];
                    lock (this.LOCK_OBJ)
                    {
                        this.VALUES.Remove(node);
                        this.VALUES.AddFirst(node);
                    }
                    node.Value = (key, value);
                }
                else
                {
                    var node = new LinkedListNode<(TKey, TValue)>((key, value));
                    lock (this.LOCK_OBJ)
                    {
                        this.KEY_TO_NODE[key] = node;
                        this.VALUES.AddFirst(node);
                        while (this.KEY_TO_NODE.Count > this.Capacity)
                        {
                            this.KEY_TO_NODE.Remove(this.VALUES.Last.Value.key);
                            this.VALUES.RemoveLast();
                        }
                    }
                }
            }
        }

        public LRUCache(int capacity)
        {
            this.Capacity = capacity;
            this.KEY_TO_NODE = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity + 1);
            this.VALUES = new LinkedList<(TKey, TValue)>();
        }

        public bool Contains(TKey key)
        {
            return this.KEY_TO_NODE.ContainsKey(key);
        }

        public void Clear()
        {
            this.KEY_TO_NODE.Clear();
            this.VALUES.Clear();
        }
    }
}
