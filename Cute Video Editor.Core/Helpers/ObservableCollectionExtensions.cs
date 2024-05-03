using DynamicData;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CuteVideoEditor.Core.Helpers;

public static class ObservableCollectionExtensions
{
    public static void ActOnEveryObject<T>(this ObservableCollection<T> collection, PropertyChangedEventHandler action)
        where T : IReactiveObject
    {
        collection.ActOnEveryObject(
            x => { x.PropertyChanged += action; action(collection, new(null)); },
            x => x.PropertyChanged -= action);
    }

    static readonly Dictionary<object, Action> synchronizedObservableCollections = [];
    public static void KeepInSync<TSrc, TDst>(this ObservableCollection<TDst> target, ObservableCollection<TSrc> source,
        Func<TSrc, TDst> projection)
    {
        synchronizedObservableCollections[target] = () =>
        {
            target.Clear();
            target.AddRange(source.Select(projection));
        };
        target.ResetSync();

        source.CollectionChanged += (s, e) =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    for (int i = 0; i < e.NewItems!.Count; i++)
                        target.Insert(e.NewStartingIndex + i, projection((TSrc)e.NewItems[i]!));
                    break;

                case NotifyCollectionChangedAction.Remove:
                    for (int i = 0; i < e.OldItems!.Count; i++)
                        target.RemoveAt(e.OldStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    target[e.NewStartingIndex] = projection((TSrc)e.NewItems![0]!);
                    break;

                case NotifyCollectionChangedAction.Move:
                    target.Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    target.Clear();
                    target.AddRange(source.Select(projection));
                    break;
            }
        };
    }

    public static void ResetSync<TDst>(this ObservableCollection<TDst> target) =>
        synchronizedObservableCollections[target]();

    public static void RemoveAll<T>(this ObservableCollection<T> collection, Func<T, bool> predicate)
    {
        for (int i = collection.Count - 1; i >= 0; i--)
            if (predicate(collection[i]))
                collection.RemoveAt(i);
    }
}