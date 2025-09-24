using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Rage;
using ReportsPlus.Utils.Data;
using static ReportsPlus.Main;
using static ReportsPlus.Utils.ConfigUtils;
using static ReportsPlus.Utils.Menu.MenuProcessing;

namespace ReportsPlus.Utils.ALPR
{
    public static partial class ALPRUtils
    {
        //BUG: when alpr is active the owner is being changed and isnt accurate to the driver even when config is set to 100% for the driver being the owner of the vehicle
        //TODO: requires testing ^

        public static GameFiber AlprFiber;
        private static DateTime _lastEntityUpdate = DateTime.MinValue;
        private static List<VehicleData> _cachedVehicleData = new List<VehicleData>();

        private static readonly List<Tuple<Blip, GameFiber>> ActiveBlipFibers = new List<Tuple<Blip, GameFiber>>();
        private static readonly object BlipLock = new object();

        public static void ToggleAlpr()
        {
            ALPRActive = !ALPRActive;

            Game.DisplaySubtitle("~y~ALPR Status: " + (ALPRActive ? "~g~Enabled" : "~r~Disabled"));
            Game.LogTrivial("ReportsPlusListener: ALPR Status: " + (ALPRActive ? "Enabled" : "Disabled"));

            if (ALPRButton != null)
            {
                ALPRButton.ForeColor = ALPRActive ? Color.FromArgb(59, 171, 44) : Color.FromArgb(204, 73, 62);
                ALPRButton.HighlightedForeColor = ALPRActive ? Color.FromArgb(0, 108, 18) : Color.FromArgb(170, 30, 32);
            }

            if (AlprFiber is { IsAlive: true }) AlprFiber.Abort();
            AlprFiber = null;

            try
            {
                Game.RawFrameRender -= LicensePlateDisplay.OnFrameRender;
            }
            catch (Exception)
            {
                Game.LogTrivial("ReportsPlusListener [ERROR]: Error clearing previous framerender");
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~r~Error", "~o~Error clearing previous framerender");

                return;
            }

            if (!ALPRActive) return;
            AlprFiber = GameFiber.StartNew(() => AlprProcess(ConfigUtils.AlprSetupType), "AlprFiber");

            if (!LicensePlateDisplay.EnablePlateDisplay) return;
            if (LicensePlateDisplay.PlateImage == null || LicensePlateDisplay.BackgroundImg == null)
            {
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~r~Error Loading Images", "~o~Failed to load license plate or background image");
                Game.LogTrivial("ReportsPlusListener [ERROR]: Failed to load license plate images or background image when starting platedisplay!");
                return;
            }

            Game.RawFrameRender += LicensePlateDisplay.OnFrameRender;
        }

        private static void AlprProcess(AlprSetupType currentAlprSetup)
        {
            _cachedVehicleData = new List<VehicleData>();
            _lastEntityUpdate = DateTime.MinValue;

            while (IsOnDuty)
            {
                GameFiber.Wait(ALPRUpdateDelay);

                var patrolCar = Game.LocalPlayer.Character.CurrentVehicle;
                if (patrolCar == null || !patrolCar.IsValid() || !LocalPlayer.IsInAnyVehicle(false)) continue;

                RunVehicleALPR(currentAlprSetup, patrolCar);
            }
        }

        private static void RunVehicleALPR(AlprSetupType setupType, Vehicle patrolCar)
        {
            var scanners = GetScannerConfigurations(patrolCar, setupType);
            if (scanners.Count == 0) return;

            if ((DateTime.Now - _lastEntityUpdate).TotalMilliseconds >= 250)
            {
                var maxScannerDistance = scanners.Max(s => Vector3.Distance(patrolCar.Position, s.Position));
                var combinedRadius = maxScannerDistance + scanners[0].Radius;

                _cachedVehicleData = World.GetEntities(patrolCar.Position, combinedRadius, GetEntitiesFlags.ConsiderCars).OfType<Vehicle>().Where(v => v.IsValid() && v != patrolCar).Select(v => new VehicleData
                {
                    Vehicle = v,
                    LicensePlateLower = v.LicensePlate?.ToLower() ?? "",
                    IsPolice = v.IsPoliceVehicle,
                    Position = v.Position
                }).ToList();

                _lastEntityUpdate = DateTime.Now;
            }

            foreach (var scanner in scanners) ProcessScanner(scanner, ShowAlprDebug, _cachedVehicleData);
        }

        private static List<ScannerConfig> GetScannerConfigurations(Vehicle vehicle, AlprSetupType setupType)
        {
            var scanners = new List<ScannerConfig>();
            vehicle.Model.GetDimensions(out _, out var max);

            const float scannerHeight = -0.1f;
            const float sideOffset = 0.4f;
            const float frontBackOffset = 0.3f;

            var allScanners = new[]
            {
                // Front-Right
                new ScannerConfig(vehicle.GetOffsetPosition(new Vector3(sideOffset, max.Y * frontBackOffset - 0.75f, max.Z + scannerHeight)), (vehicle.RightVector * 0.8f + vehicle.ForwardVector * 0.7f).ToNormalized(), ScanRadius, PlateScanLocation.FrontRight, ScannerPositionType.Front),
                // Front-Left
                new ScannerConfig(vehicle.GetOffsetPosition(new Vector3(-sideOffset, max.Y * frontBackOffset - 0.75f, max.Z + scannerHeight)), (-vehicle.RightVector * 0.8f + vehicle.ForwardVector * 0.7f).ToNormalized(), ScanRadius, PlateScanLocation.FrontLeft, ScannerPositionType.Front),
                // Rear-Right
                new ScannerConfig(vehicle.GetOffsetPosition(new Vector3(sideOffset, -max.Y * frontBackOffset, max.Z + scannerHeight)), (vehicle.RightVector * 0.8f - vehicle.ForwardVector * 0.6f).ToNormalized(), ScanRadius, PlateScanLocation.RearRight, ScannerPositionType.Rear),
                // Rear-Left
                new ScannerConfig(vehicle.GetOffsetPosition(new Vector3(-sideOffset, -max.Y * frontBackOffset, max.Z + scannerHeight)), (-vehicle.RightVector * 0.8f - vehicle.ForwardVector * 0.6f).ToNormalized(), ScanRadius, PlateScanLocation.RearLeft, ScannerPositionType.Rear)
            };

            scanners.AddRange(setupType switch
            {
                AlprSetupType.All => allScanners,
                AlprSetupType.Rear => allScanners.Where(s => s.ScannerPositionType == ScannerPositionType.Rear),
                AlprSetupType.Front => allScanners.Where(s => s.ScannerPositionType == ScannerPositionType.Front),
                _ => Array.Empty<ScannerConfig>()
            });

            return scanners;
        }

        private static void ProcessScanner(ScannerConfig scanner, bool showDebug, List<VehicleData> allVehicles)
        {
            if (showDebug)
            {
                DrawScanCone(scanner.Position, scanner.Forward, scanner.Radius, MaxScanAngle, Color.White);
                Debug.DrawSphere(scanner.Position, 0.1f, Color.Blue);
            }

            var radiusSq = scanner.Radius * scanner.Radius;
            var scannerPos = scanner.Position;

            var nearbyVehicles = new List<(Vehicle Vehicle, float DistanceSq)>();
            foreach (var vd in allVehicles)
            {
                var distanceSq = Vector3.DistanceSquared(scannerPos, vd.Position);
                if (distanceSq > radiusSq) continue;

                if (vd.IsPolice || vd.LicensePlateLower == "46eek572") continue;

                InsertClosestVehicle(nearbyVehicles, vd.Vehicle, distanceSq, 3);
            }

            var alprFilePath = $"{FileDataFolder}/alpr.data";
            var existingPlates = new HashSet<string>(File.ReadLines(alprFilePath).Where(line => line.Contains("licenseplate=")).Select(line => line.Split('=')[1].Split('&')[0]));

            foreach (var targetVehicle in nearbyVehicles.Select(v => v.Vehicle))
            {
                if (existingPlates.Contains(targetVehicle.LicensePlate)) continue;

                var platePositions = GetPlatePositions(targetVehicle);
                var validPlates = new Dictionary<Vector3, VehiclePlateType>();

                foreach (var plateInfo in platePositions)
                {
                    var platePos = plateInfo.Key;
                    var plateType = plateInfo.Value;

                    var distance = Vector3.Distance(scanner.Position, platePos);
                    if (distance > scanner.Radius) continue;

                    var directionToPlate = (platePos - scanner.Position).ToNormalized();
                    var horizontalAngle = (float)(Math.Acos(Vector3.Dot(scanner.Forward.ToNormalized(), directionToPlate.ToNormalized())) * (180 / Math.PI));
                    if (horizontalAngle > MaxScanAngle) continue;

                    var hit = World.TraceLine(scanner.Position, platePos + new Vector3(0, 0, 0.5f), TraceFlags.IntersectEverything, scanner.Vehicle);

                    if (!hit.Hit || hit.HitEntity != targetVehicle) continue;

                    switch (plateType)
                    {
                        case VehiclePlateType.Front:
                            LicensePlateDisplay.FrontPlateText = targetVehicle.LicensePlate;
                            break;
                        case VehiclePlateType.Rear:
                            LicensePlateDisplay.RearPlateText = targetVehicle.LicensePlate;
                            break;
                    }

                    validPlates.Add(platePos, plateType);
                }

                if (!validPlates.Any()) continue;
                var closest = validPlates.OrderBy(p => Vector3.Distance(scanner.Position, p.Key)).First();

                ProcessPlateDetection(targetVehicle, closest.Key, closest.Value, scanner, showDebug);
            }
        }

        private static void ProcessPlateDetection(Vehicle targetVehicle, Vector3 platePos, VehiclePlateType plateType, ScannerConfig scanner, bool showDebug)
        {
            var plate = targetVehicle.LicensePlate;

            var alprFilePath = $"{FileDataFolder}/alpr.data";
            var entries = MathUtils.ParseVehicleData(File.ReadAllText(alprFilePath));
            var now = DateTimeOffset.Now;
            var isAlreadyScanned = entries.Any(entry => entry.TryGetValue("licenseplate", out var existingPlate) && existingPlate == plate && entry.TryGetValue("timescanned", out var timeScannedStr) && DateTimeOffset.TryParse(timeScannedStr, out var timeScanned) && (now - timeScanned).TotalMilliseconds <= ReScanPlateInterval);

            if (isAlreadyScanned) return;

            var vehFlags = new StringBuilder();
            var cleanedFlags = "";

            var vehicleProperties = WorldDataUtils.GetVehicleDataFromWorldCars(targetVehicle.LicensePlate);

            if (vehicleProperties == null)
            {
                var vehicleDataString = WorldDataUtils.GetWorldCarData(targetVehicle);
                if (!string.IsNullOrEmpty(vehicleDataString)) vehicleProperties = WorldDataUtils.ParseEntry(vehicleDataString);
            }

            if (vehicleProperties != null)
            {
                if (vehicleProperties.TryGetValue("registration", out var registration))
                    switch (registration.ToLower())
                    {
                        case "expired":
                            vehFlags.Append("~o~Expired Registration\n");
                            break;
                        case "none":
                            vehFlags.Append("~r~No Registration\n");
                            break;
                    }

                if (vehicleProperties.TryGetValue("insurance", out var insurance))
                    switch (insurance.ToLower())
                    {
                        case "expired":
                            vehFlags.Append("~o~Expired Insurance\n");
                            break;
                        case "none":
                            vehFlags.Append("~r~No Insurance\n");
                            break;
                    }

                if (vehicleProperties.TryGetValue("isstolen", out var stolen) && stolen.ToLower() == "true") vehFlags.Append("~r~Stolen Vehicle\n");
            }

            cleanedFlags = Regex.Replace(vehFlags.ToString(), "~[^~]+~", "");

            var oldContent = File.ReadAllText(alprFilePath);
            var delimiter = oldContent.Length > 0 ? "|" : "";
            var data = $"licenseplate={plate}&plateType={plateType}&speed={targetVehicle.Speed:F}&distance={Vector3.Distance(scanner.Position, platePos):0.0}&scanner={scanner.ScanLocation}&flags={cleanedFlags}&timescanned={DateTime.Now:o}";
            File.WriteAllText(alprFilePath, $"{oldContent}{delimiter}{data}");

            if (!string.IsNullOrEmpty(vehFlags.ToString()))
            {
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", $"~b~ALPR Scan [{scanner.ScanLocation}]~s~\n", $"~y~Plate:~w~ {plate}\n" + $"~y~Plate Type:~w~ {plateType}\n" + $"~y~Distance:~w~ {Vector3.Distance(scanner.Position, platePos):0.0}\n" + $"{vehFlags}");

                CreateTemporaryBlip(targetVehicle);
            }

            MathUtils.RemoveOldPlates("alpr.data", ReScanPlateInterval);

            if (!showDebug) return;
            Debug.DrawLineDebug(scanner.Position, platePos, Color.Green);
            Debug.DrawSphere(platePos, 0.1f, Color.Green);
        }

        private static void CreateTemporaryBlip(Vehicle vehicle)
        {
            var targetBlip = vehicle.AttachBlip();
            targetBlip.Color = Color.Red;
            targetBlip.Scale = 0.7f;

            var deletionFiber = GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(BlipDisplayTime);

                lock (BlipLock)
                {
                    if (targetBlip) targetBlip.Delete();

                    ActiveBlipFibers.RemoveAll(bf => bf.Item1 == targetBlip);
                }
            }, "DeleteALPRBlip");

            lock (BlipLock)
            {
                ActiveBlipFibers.Add(Tuple.Create(targetBlip, deletionFiber));
            }
        }

        public static void CleanupAllBlips()
        {
            lock (BlipLock)
            {
                foreach (var (blip, fiber) in ActiveBlipFibers.ToList())
                {
                    if (fiber.IsAlive) fiber.Abort();
                    if (blip) blip.Delete();

                    ActiveBlipFibers.RemoveAll(bf => bf.Item1 == blip);
                }
            }
        }

        private static Dictionary<Vector3, VehiclePlateType> GetPlatePositions(Vehicle vehicle)
        {
            var plates = new Dictionary<Vector3, VehiclePlateType>();
            vehicle.Model.GetDimensions(out var min, out var max);

            if (!vehicle.IsBike)
                plates.Add(vehicle.GetOffsetPosition(new Vector3(0, max.Y - 0.2f, min.Z)), VehiclePlateType.Front);

            plates.Add(vehicle.GetOffsetPosition(new Vector3(0, min.Y + 0.2f, min.Z)), VehiclePlateType.Rear);

            return plates;
        }

        private static void DrawScanCone(Vector3 position, Vector3 forward, float radius, float angleDegrees, Color scanColor)
        {
            const int segments = 12;
            var angleRad = MathHelper.ConvertDegreesToRadians(angleDegrees);
            var forwardNormalized = forward.ToNormalized();

            var leftRot = Quaternion.RotationAxis(Vector3.WorldUp, angleRad);
            var rightRot = Quaternion.RotationAxis(Vector3.WorldUp, -angleRad);

            var leftEdge4 = Vector3.Transform(forward, leftRot);
            var rightEdge4 = Vector3.Transform(forward, rightRot);

            var leftEdge = new Vector3(leftEdge4.X, leftEdge4.Y, leftEdge4.Z) * radius;
            var rightEdge = new Vector3(rightEdge4.X, rightEdge4.Y, rightEdge4.Z) * radius;

            Debug.DrawLine(position, position + leftEdge, Color.White);
            Debug.DrawLine(position, position + rightEdge, Color.White);

            var globalUp = Vector3.WorldUp;
            var right = Vector3.Cross(forwardNormalized, globalUp);
            if (right.Length() < 0.01f)
            {
                globalUp = Vector3.WorldEast;
                right = Vector3.Cross(forwardNormalized, globalUp);
            }

            right = right.ToNormalized();
            var localUp = Vector3.Cross(right, forwardNormalized).ToNormalized();

            var prevDir = Vector3.Zero;
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var currentAngle = MathHelper.Lerp(-angleRad, angleRad, t);
                var currentRot = Quaternion.RotationAxis(localUp, currentAngle);

                var currentDir4 = Vector3.Transform(forwardNormalized, currentRot);
                var currentDir = new Vector3(currentDir4.X, currentDir4.Y, currentDir4.Z) * radius;

                if (i > 0) Debug.DrawLineDebug(position + prevDir, position + currentDir, scanColor);

                prevDir = currentDir;
            }
        }

        private static void InsertClosestVehicle(List<(Vehicle Vehicle, float DistanceSq)> list, Vehicle vehicle, float distanceSq, int maxSize)
        {
            var index = list.Count;
            while (index > 0 && distanceSq < list[index - 1].DistanceSq)
                index--;

            if (index >= maxSize) return;
            list.Insert(index, (vehicle, distanceSq));
            if (list.Count > maxSize)
                list.RemoveAt(maxSize);
        }
    }
}