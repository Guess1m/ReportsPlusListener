using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.Continuous
{
    public class EntityTrackingAction : IContinuousAction
    {
        private static readonly Dictionary<int, Ped> TrackedPeds = new Dictionary<int, Ped>();

        /// <summary>
        ///     Performs a periodic scan of all peds in the world to maintain synchronization between the game state and the
        ///     client.
        ///     Automatically handles the registration of new peds and the removal of peds that have been deleted or left the
        ///     tracking radius.
        /// </summary>
        /// <param name="client">The active <see cref="GameClientSocket" /> instance used for communication.</param>
        public void Execute(GameClientSocket client)
        {
            var nearbyPeds = new HashSet<Ped>(World.GetAllPeds());

            var nearbyPedHandles = new HashSet<int>(nearbyPeds.Select(p => (int)p.Handle.Value));
            var removedPedHandles = new List<int>();

            foreach (var trackedPedHandle in TrackedPeds.Keys)
                if (!nearbyPedHandles.Contains(trackedPedHandle))
                {
                    removedPedHandles.Add(trackedPedHandle);
                    var removedData = new JObject { ["entityId"] = trackedPedHandle };
                    client.Send("pedRemoved", removedData);
                }

            foreach (var handle in removedPedHandles) TrackedPeds.Remove(handle);

            foreach (var ped in nearbyPeds)
            {
                if (ped == null || !ped.Exists() || ped == Main.LPC) continue;
                if (TrackedPeds.ContainsKey((int)ped.Handle.Value)) continue;
                var pedData = Misc.Misc.CurrentMode == Misc.Misc.IntegrationMode.PolicingRedefined ? PedDataHelper.GeneratePedDataPR(ped) : PedDataHelper.GeneratePedData(ped);
                if (pedData == null) continue;
                client.Send("pedCreated", pedData);
                TrackedPeds.Add((int)ped.Handle.Value, ped);
            }
        }
    }
}