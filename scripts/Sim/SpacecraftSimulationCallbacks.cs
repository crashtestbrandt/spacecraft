// Deterministic side of the game: registers systems, spawns each participant's fixed army at tick 0,
// and turns the local player's queued orders into commands each tick.
using System.Collections.Generic;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Systems;

namespace Spacecraft.Sim
{
    public sealed class SpacecraftSimulationCallbacks : ISimulationCallbacks
    {
        readonly OrderQueue _orders;

        public SpacecraftSimulationCallbacks(OrderQueue orders)
        {
            _orders = orders;
        }

        public void RegisterSystems(EcsSimulation sim)
        {
            sim.AddSystem(new OrderSystem(),     SystemPhase.PreUpdate);
            sim.AddSystem(new ShipMoveSystem(),  SystemPhase.Update);
            sim.AddSystem(new TargetingSystem(), SystemPhase.Update);
            sim.AddSystem(new BulletSystem(),    SystemPhase.Update);
            sim.AddSystem(new DeathSystem(),     SystemPhase.LateUpdate);
            sim.AddSystem(new MatchEndSystem(),  SystemPhase.LateUpdate);
            sim.AddSystem(new EventSystem(),     SystemPhase.LateUpdate);
        }

        public void OnInitializeWorld(IKlothoEngine engine)
        {
            var frame = engine.InitFrame;

            var state = frame.CreateEntity();
            frame.Add(state, new MatchStateComponent { Ended = 0, WinnerPlayerId = -1 });

            // Spawn for the authoritative participant set (engine-written slots), not MaxPlayers, so a
            // 2-player match on a 4-slot session seeds the same entities on every peer.
            var participants = new List<int>();
            var pf = frame.Filter<SessionParticipantComponent>();
            while (pf.Next(out var slot))
                participants.Add(frame.GetReadOnly<SessionParticipantComponent>(slot).PlayerId);
            participants.Sort();

            for (int i = 0; i < participants.Count; i++)
                SpawnArmy(frame, participants[i]);
        }

        static void SpawnArmy(Frame frame, int playerId)
        {
            FP64 facing = Rules.SpawnFacing(playerId);
            for (int slot = 0; slot < Rules.ArmySize; slot++)
            {
                int kind = Rules.KindOf(slot);
                var pos = Rules.SpawnPosition(playerId, slot);
                var e = frame.CreateEntity();
                frame.Add(e, new TransformComponent
                {
                    Position = new FPVector3(pos.x, pos.y, FP64.Zero),
                    Rotation = facing,
                    Scale = FPVector3.One,
                });
                frame.Add(e, new OwnerComponent { OwnerId = playerId });
                frame.Add(e, new ShipComponent
                {
                    Slot = slot,
                    Kind = kind,
                    Hp = Rules.Hp[kind],
                    HasTarget = 0,
                    TargetX = pos.x,
                    TargetY = pos.y,
                    FireCooldown = Rules.CooldownTicks[kind] + slot,   // stagger the opening volley
                });
            }
        }

        // One queued order per tick for the local player; no order -> Klotho injects an empty command.
        public void OnPollInput(int playerId, int tick, ICommandSender sender)
        {
            if (!_orders.TryDequeue(out var order)) return;
            var cmd = CommandPool.Get<MoveOrderCommand>();
            cmd.PlayerId = playerId;
            cmd.SelectionMask = order.SelectionMask;
            cmd.TargetX = order.TargetX;
            cmd.TargetY = order.TargetY;
            sender.Send(cmd);
        }

        public void OnPlayerJoinedWorld(IKlothoEngine engine, Frame frame, int playerId) { }   // late join is off
    }
}
