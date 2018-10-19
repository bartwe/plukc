using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace Compiler
{
    [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Set")]
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    public class Set<T> : ICollection<T>
    {
        private Dictionary<T, bool> values;

        public Set(IEqualityComparer<T> comparer)
        {
            values = new Dictionary<T, bool>(comparer);
        }

        public Set(IEnumerable<T> collection)
        {
            values = new Dictionary<T, bool>();
            AddRange(collection);
        }

        public Set()
        {
            values = new Dictionary<T, bool>();
        }

        public void Add(T item)
        {
            values.Add(item, true);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            foreach (T item in collection)
                Add(item);
        }

        public void Put(T item)
        {
            values[item] = true;
        }

        public void PutRange(IEnumerable<T> collection)
        {
            foreach (T item in collection)
                Put(item);
        }

        public void Clear()
        {
            values.Clear();
        }

        public bool Contains(T item)
        {
            return values.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach(T t in this)
                array[arrayIndex++] = t;
        }

        public int Count
        {
            get { return values.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            return values.Remove(item);
        }

        public Set<T> Select(Predicate<T> match)
        {
            Set<T> result = new Set<T>();
            foreach (T t in values.Keys)
                if (match(t))
                    result.Add(t);
            return result;
        }

        public bool ContainsAll(IEnumerable<T> collection)
        {
            foreach (T item in collection)
                if (!Contains(item))
                    return false;
            return true;
        }

        public void RemoveAll(Predicate<T> match)
        {
            Dictionary<T, bool> newValues = new Dictionary<T, bool>();
            foreach (T t in values.Keys)
                if (!match(t))
                    newValues.Add(t, true);
            values = newValues;
        }

        public bool MatchAll(Predicate<T> match)
        {
            foreach (T t in values.Keys)
                if (!match(t))
                    return false;
            return true;
        }

        public bool MatchNone(Predicate<T> match)
        {
            foreach (T t in values.Keys)
                if (match(t))
                    return false;
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return values.Keys.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return values.Keys.GetEnumerator();
        }
    }
}
