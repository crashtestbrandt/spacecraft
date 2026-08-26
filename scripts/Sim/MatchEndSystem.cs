// Ends the match once at most one player still has ships: records it in MatchStateComponent and
// raises a synced GameOverEvent exactly once.
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Spacecraft.Sim
{
    public sealed class MatchEndSystem : ISystem
    {
        public void Update(ref Frame frame)
        {
            if (frame.Tick < Rules.MinMatchTicks) return;
            ref var state = ref frame.GetSingleton<MatchStateComponent>();
            if (state.Ended != 0) return;

            int aliveMask = 0;
            var f = frame.Filter<ShipComponent, OwnerComponent>();
            while (f.Next(out var e))
                aliveMask |= 1 << frame.GetReadOnly<OwnerComponent>(e).OwnerId;

            int aliveCount = 0, winner = -1;
            for (int p = 0; p < Rules.MaxPlayers; p++)
                if ((aliveMask & (1 << p)) != 0) { aliveCount++; winner = p; }
            if (aliveCount > 1) return;
            if (aliveCount == 0) winner = -1;

            state.Ended = 1;
            state.WinnerPlayerId = winner;

            var evt = EventPool.Get<GameOverEvent>();
            evt.WinnerPlayerId = winner;
            frame.EventRaiser.RaiseEvent(evt);
        }
    }
}
