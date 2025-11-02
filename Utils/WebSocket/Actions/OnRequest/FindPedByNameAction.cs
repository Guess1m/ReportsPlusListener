using CommonDataFramework.Modules.PedDatabase;
using Rage;
using ReportsPlus.Utils.WebSocket.Messages;

namespace ReportsPlus.Utils.WebSocket.Actions.OnRequest{
    public class FindPedByNameAction : IRequestAction{
        public string Name => "findPedByName";

        public void Execute(GameClientSocket client, IncomingRequest request)
        {
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
        }
    }
}