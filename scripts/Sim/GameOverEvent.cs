// Raised once on the verified timeline when at most one army is left. WinnerPlayerId -1 = draw.
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Spacecraft.Sim
{
    [KlothoSerializable(101)]
    public partial class GameOverEvent : SimulationEvent
    {
        public override EventMode Mode => EventMode.Synced;

        [KlothoOrder(0)] public int WinnerPlayerId;
    }
}
