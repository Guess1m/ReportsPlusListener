using CommonDataFramework.Modules.PedDatabase;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Messages;
using ReportsPlus.Utils.WorldData;

namespace ReportsPlus.Utils.WebSocket.Actions.OnRequest{
    public class FindPedByNameAction : IRequestAction{
        public string Name => "findPedByName";

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
            Logger.LogInfo("Running FindPedByNameAction ...");
            Logger.LogInfo("Args: " + request.Args);
            var nameToFind = request.Args;
            if (string.IsNullOrEmpty(nameToFind)) return;

            foreach (var ped in World.GetAllPeds())
            {
                if (!ped || !ped.Exists()) continue;

                var isMatch = false;

                var pedData = ped.GetPedData();
                if (pedData == null) continue;

                var pedName = pedData.FullName ?? null;
                if (pedName == null) continue;

                if (pedName.ToLower().Contains(nameToFind.ToLower())) isMatch = true;

                if (!isMatch) continue;
                var pedDataJson = PedDataHelper.GeneratePedData(ped);
                if (pedDataJson == null) continue;

                client.Send("pedUpdated", pedDataJson);
                return;
            }

            client.Send("pedNotFound", new JValue(nameToFind));
        }
    }
}