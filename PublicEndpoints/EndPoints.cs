using System;
using Newtonsoft.Json.Linq;
using ReportsPlus.Utils.Logging;
using ReportsPlus.Utils.WebSocket.Actions.Events;

namespace ReportsPlus.PublicEndpoints
{
    public class EndPoints
    {
        /// <summary>
        ///     Sends a TenCodeUpdate message to the connected CAD server.
        /// </summary>
        /// <param name="tenCode">The ten code string to send.</param>
        /// <param name="sendActionAssociated">Whether or not to send the action associated with the ten code.</param>
        public static void SendTenCodeUpdate(string tenCode, bool sendActionAssociated = true)
        {
            try
            {
                Logger.LogInfo($"EndPoints: Attempting to send TenCodeUpdate with code: '{tenCode}'.");

                if (string.IsNullOrWhiteSpace(tenCode))
                {
                    Logger.LogWarning("EndPoints: SendTenCodeUpdate aborted. The provided tenCode is null or empty.");
                    return;
                }

                if (!Main.IsConnected)
                {
                    Logger.LogWarning($"EndPoints: TenCodeUpdate for '{tenCode}' aborted. ReportsPlus is not connected to the server.");
                    return;
                }

                var payload = new JObject
                {
                    ["tenCode"] = tenCode,
                    ["sendActionAssociated"] = sendActionAssociated
                };

                EventManager.SendTenCodeUpdate(payload);
                Logger.LogInfo($"EndPoints: Enqueued TenCodeUpdate '{tenCode}' with sendActionAssociated: '{sendActionAssociated}', to send.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"EndPoints: Critical exception during SendTenCodeUpdate for '{tenCode}': {ex.Message}");
                Logger.LogError($"StackTrace: {ex.StackTrace}");
            }
        }
    }
}