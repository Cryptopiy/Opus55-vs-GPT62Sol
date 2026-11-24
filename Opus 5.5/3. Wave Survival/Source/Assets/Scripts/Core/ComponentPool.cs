using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Fixed-cap object pool. Objects are created inactive (pre-warmed), handed out inactive so the caller can place them
    /// before activation, and never destroyed. Get() returns null once the hard cap is reached (spawners must respect caps).
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        readonly Stack<T> free;
        readonly Func<T> factory;
        readonly int cap;
        int created;

        public int Created => created;
        public int Active => created - free.Count;
        public int Cap => cap;

        public ComponentPool(Func<T> factory, int warm, int cap)
        {
            this.factory = factory;
            this.cap = Mathf.Max(1, cap);
            free = new Stack<T>(this.cap);
            int n = Mathf.Min(warm, this.cap);
            for (int i = 0; i < n; i++) free.Push(Create());
        }

        T Create()
        {
            T item = factory();
            item.gameObject.SetActive(false);
            created++;
            return item;
        }

        public T Get()
        {
            if (free.Count > 0) return free.Pop();
            return created < cap ? Create() : null;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            free.Push(item);
        }
    }
}
