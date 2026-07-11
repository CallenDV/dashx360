using System.Collections;

internal sealed class _003C_003Ez__ReadOnlyArray<T> : IList<T>, IReadOnlyList<T>, IList
{
    private readonly T[] _items;

    public _003C_003Ez__ReadOnlyArray(T[] items)
    {
        _items = items;
    }

    public int Count => _items.Length;
    public bool IsReadOnly => true;
    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    public T this[int index]
    {
        get => _items[index];
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => _items[index];
        set => throw new NotSupportedException();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    public bool Contains(T item) => ((ICollection<T>)_items).Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(T item) => ((IList<T>)_items).IndexOf(item);
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    bool IList.Contains(object? value) => ((IList)_items).Contains(value);
    int IList.IndexOf(object? value) => ((IList)_items).IndexOf(value);
    public void Add(T item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, T item) => throw new NotSupportedException();
    public bool Remove(T item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
}

internal sealed class _003C_003Ez__ReadOnlySingleElementList<T> : IList<T>, IReadOnlyList<T>, IList
{
    private readonly T _item;

    public _003C_003Ez__ReadOnlySingleElementList(T item)
    {
        _item = item;
    }

    public int Count => 1;
    public bool IsReadOnly => true;
    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    public T this[int index]
    {
        get => index == 0 ? _item : throw new IndexOutOfRangeException();
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => index == 0 ? _item : throw new IndexOutOfRangeException();
        set => throw new NotSupportedException();
    }

    public IEnumerator<T> GetEnumerator()
    {
        yield return _item;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Contains(T item) => EqualityComparer<T>.Default.Equals(_item, item);
    public void CopyTo(T[] array, int arrayIndex) => array[arrayIndex] = _item;
    public int IndexOf(T item) => Contains(item) ? 0 : -1;
    void ICollection.CopyTo(Array array, int index) => array.SetValue(_item, index);
    bool IList.Contains(object? value) => value is T item && Contains(item);
    int IList.IndexOf(object? value) => value is T item ? IndexOf(item) : -1;
    public void Add(T item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, T item) => throw new NotSupportedException();
    public bool Remove(T item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
}
