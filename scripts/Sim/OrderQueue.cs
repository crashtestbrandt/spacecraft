// View -> sim handoff for the local player's orders. The view enqueues on mouse input; OnPollInput
// dequeues one order per tick. Main-thread only. Nothing here runs inside the deterministic step.
using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;

namespace Spacecraft.Sim
{
    public sealed class OrderQueue
    {
        public struct Order { public int SelectionMask; public FP64 TargetX; public FP64 TargetY; }

        readonly Queue<Order> _pending = new();

        public void EnqueueMove(int selectionMask, FP64 x, FP64 y)
        {
            if (selectionMask == 0) return;
            _pending.Enqueue(new Order { SelectionMask = selectionMask, TargetX = x, TargetY = y });
        }

        public bool TryDequeue(out Order order) => _pending.TryDequeue(out order);

        public void Clear() => _pending.Clear();
    }
}
