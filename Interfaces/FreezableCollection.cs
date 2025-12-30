using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

public class FreezableCollection<T> : Freezable, IList<T> where T : DependencyObject
{
    private ObservableCollection<T> _collection = new ObservableCollection<T>();

    public FreezableCollection() { }

    public T this[int index]
    {
        get => _collection[index];
        set => _collection[index] = value;
    }

    public int Count => _collection.Count;

    public bool IsReadOnly => throw new System.NotImplementedException();

    public IEnumerator<T> GetEnumerator() => _collection.GetEnumerator();
    public void Add(T item) => _collection.Add(item);
    public void Clear() => _collection.Clear();
    public bool Contains(T item) => _collection.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _collection.CopyTo(array, arrayIndex);
    public bool Remove(T item) => _collection.Remove(item);
    public int IndexOf(T item) => _collection.IndexOf(item);
    public void Insert(int index, T item) => _collection.Insert(index, item);
    public void RemoveAt(int index) => _collection.RemoveAt(index);

    protected override Freezable CreateInstanceCore() => new FreezableCollection<T>();

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new System.NotImplementedException();
    }


}
