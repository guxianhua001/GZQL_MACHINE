using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
namespace Interfaces
{
    public class ObservableDictionaryExt<TKey, TValue> : Dictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public new void Add(TKey key, TValue value)
        {
            base.Add(key, value);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value));
        }

        public new bool Remove(TKey key)
        {
            if (TryGetValue(key, out TValue value))
            {
                var item = new KeyValuePair<TKey, TValue>(key, value);
                bool result = base.Remove(key);
                if (result) OnCollectionChanged(NotifyCollectionChangedAction.Remove, item);
                return result;
            }
            return false;
        }

        public new void Clear()
        {
            base.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public new TValue this[TKey key]
        {
            get => base[key];
            set
            {
                bool exists = TryGetValue(key, out TValue oldValue);
                base[key] = value;

                if (exists)
                {
                    OnCollectionChanged(NotifyCollectionChangedAction.Replace,
                        new KeyValuePair<TKey, TValue>(key, value),
                        new KeyValuePair<TKey, TValue>(key, oldValue));
                }
                else
                {
                    OnCollectionChanged(NotifyCollectionChangedAction.Add,
                        new KeyValuePair<TKey, TValue>(key, value));
                }
            }
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> item)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, item));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> newItem, KeyValuePair<TKey, TValue> oldItem)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, newItem, oldItem));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
