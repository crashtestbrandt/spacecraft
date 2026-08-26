// A move order: the issuing player's selected slots (bitmask over army slots) and a target point.
// Discrete (not continuous) input: the predictor assumes "no new order" for a tick it has not seen.
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Spacecraft.Sim
{
    [KlothoSerializable(100)]
    public partial class MoveOrderCommand : CommandBase
    {
        public override bool IsContinuousInput => false;

        [KlothoOrder(0)] public int  SelectionMask;
        [KlothoOrder(1)] public FP64 TargetX;
        [KlothoOrder(2)] public FP64 TargetY;
    }
}
