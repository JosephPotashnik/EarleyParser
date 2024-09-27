using System.Collections.Generic;

namespace EarleyParser
{
    /// <summary>
    /// this class implements the priority queue that is responsible for the correct BFS order over the completed Items
    /// in the Completed Items Agenda.
    /// </summary>
    internal class CompletedStatesHeap
    {
        private readonly MaxHeap _indicesHeap = new MaxHeap();
        private readonly Dictionary<int, Queue<EarleyState>> _items = [];
        internal int Count => _indicesHeap.Count;

        internal void Enqueue(EarleyState state)
        {
            var index = state.StartColumn.Index;
            if (!_items.TryGetValue(index, out var queue))
            {
                _indicesHeap.Add(index);
                queue = new Queue<EarleyState>();
                _items.Add(index, queue);
            }

            queue.Enqueue(state);
        }

        internal void Clear()
        {
            _indicesHeap.Clear();
            _items.Clear();
        }

        internal EarleyState Dequeue()
        {
            var index = _indicesHeap.Max;
            var queue = _items[index];

            var state = queue.Dequeue();
            if (queue.Count == 0)
            {
                _items.Remove(index);
                _indicesHeap.PopMax();
            }

            return state;
        }
    }
}