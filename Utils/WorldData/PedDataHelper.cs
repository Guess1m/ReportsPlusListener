using System.Text;
using System.Text.RegularExpressions;
using CommonDataFramework.Modules.PedDatabase;
using LSPD_First_Response.Engine.Scripting.Entities;
using LSPD_First_Response.Mod.API;
using Newtonsoft.Json.Linq;
using Rage;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.WorldData
{
    public static class PedDataHelper
    {
        /// <summary>
        ///     Retrieves the internal model name and variation indices for a pedestrian.
        /// </summary>
        /// <param name="ped">The pedestrian entity to inspect.</param>
        /// <returns>A formatted string containing the model name, drawable index, and texture index.</returns>
        private static string FindPedModel(Ped ped)
        {
            try
            {
                if (ped == null || !ped.IsValid()) return "";
                ped.GetVariation(0, out var drawable, out var texture);
                return $"[{ped.Model.Name.ToLower()}][{drawable}][{texture}]";
            }
            catch
            {
                Logger.LogError("ReportsPlusListener: Error fetching model for ped: " + ped);
                return "";
            }
        }

        /// <summary>
        ///     Formats a physical address string from a <see cref="PedData" /> object.
        /// </summary>
        /// <param name="pedData">The data object containing address components.</param>
        /// <returns>A string representing the full address (Number, Street, Area, County).</returns>
        private static string GetPedAddress(PedData pedData)
        {
            if (pedData == null) return null;

            var addressBuilder = new StringBuilder();
            addressBuilder.Append(pedData.Address.AddressPostal.Number).Append(" ");
            addressBuilder.Append(pedData.Address.StreetName).Append(", ");
            addressBuilder.Append(pedData.Address.Zone.RealAreaName).Append(", ");
            addressBuilder.Append(Regex.Replace(pedData.Address.Zone.County.ToString(), "(?<!^)([A-Z])", " $1"));

            return addressBuilder.ToString();
        }

        #region PolicingRedefined PedData

        /// <summary>
        ///     Generates a comprehensive JSON representation of a pedestrian using Policing Redefined data.
        /// </summary>
        /// <param name="ped">The physical pedestrian entity to process.</param>
        /// <returns>
        ///     A <see cref="JObject" /> containing identification, judicial, and license data; null if the pedestrian is
        ///     invalid.
        /// </returns>
        public static JObject GeneratePedDataPR(Ped ped)
        {
            if (!ped || !ped.Exists()) return null;

            var pedData = ped.GetPedData();
            return pedData == null
                ? null
                :
                // Pass the handle if the ped exists, otherwise null
                GeneratePedDataFromObjectPR(pedData, (int)ped.Handle.Value, ped);
        }

        /// <summary>
        ///     Constructs a pedestrian JSON object from a raw <see cref="PedData" /> object, typically for entities not physically
        ///     present.
        /// </summary>
        /// <param name="pedData">The source data object.</param>
        /// <param name="entityHandle">An optional handle ID for the entity.</param>
        /// <param name="physicalPed">An optional physical entity to determine relationship-based flags like 'isPolice'.</param>
        /// <returns>A populated <see cref="JObject" /> containing the source data.</returns>
        public static JObject GeneratePedDataFromObjectPR(PedData pedData, int? entityHandle, Ped physicalPed = null)
        {
            if (pedData == null) return null;

            var pedJson = new JObject
            {
                // -- Identification --
                ["entityId"] = entityHandle ?? -1,
                ["identification"] = new JObject
                {
                    ["name"] = pedData.FullName ?? string.Empty,
                    ["address"] = GetPedAddress(pedData) ?? string.Empty,
                    ["pedModel"] = physicalPed != null ? FindPedModel(physicalPed) ?? string.Empty : string.Empty,
                    ["birthday"] = pedData.Birthday.Month.ToString("D2") + "/" + pedData.Birthday.Day.ToString("D2") + "/" + pedData.Birthday.Year,
                    ["gender"] = pedData.Gender.ToString() ?? string.Empty,
                    ["height"] = string.Empty,
                    ["weight"] = string.Empty,
                    ["eyeColor"] = string.Empty,
                    ["hairColor"] = string.Empty,
                    ["knownAliases"] = string.Empty,
                    ["ethnicity"] = string.Empty,
                    ["distinguishingMarks"] = string.Empty,
                    ["citizenshipStatus"] = string.Empty,
                    ["maritalStatus"] = string.Empty,
                    ["disabilityStatus"] = string.Empty,
                    ["isPolice"] = physicalPed != null && physicalPed.RelationshipGroup == "COP" ? "true" : "false"
                },

                // -- Criminal History --
                ["judicialStatus"] = new JObject
                {
                    ["isWanted"] = pedData.Wanted.ToString() ?? string.Empty,
                    ["warrantInfo"] = new JObject
                    {
                        ["warrantNumber"] = string.Empty,
                        ["dateIssued"] = string.Empty,
                        ["issuingAgency"] = string.Empty,
                        ["warrantCharge"] = string.Empty,
                        ["bailAmount"] = string.Empty
                    },
                    ["paroleInfo"] = new JObject
                    {
                        ["isOnParole"] = pedData.IsOnParole.ToString() ?? string.Empty,
                        ["paroleStartDate"] = string.Empty,
                        ["paroleEndDate"] = string.Empty,
                        ["paroleRestrictions"] = string.Empty,
                        ["paroleAgency"] = string.Empty,
                        ["paroleOfficer"] = string.Empty,
                        ["paroleOfficerEmail"] = string.Empty
                    },
                    ["probationInfo"] = new JObject
                    {
                        ["isOnProbation"] = pedData.IsOnProbation.ToString() ?? string.Empty,
                        ["probationStartDate"] = string.Empty,
                        ["probationEndDate"] = string.Empty,
                        ["probationRestrictions"] = string.Empty,
                        ["probationAgency"] = string.Empty,
                        ["probationOfficer"] = string.Empty,
                        ["probationOfficerEmail"] = string.Empty
                    },
                    ["timesStopped"] = pedData.TimesStopped.ToString() ?? string.Empty,
                    ["restrainingOrderActive"] = string.Empty
                },

                // -- Licenses and Permits --
                ["licensesAndPermits"] = new JObject
                {
                    ["driversLicense"] = new JObject
                    {
                        ["status"] = pedData.DriversLicenseState.ToString() ?? string.Empty,
                        ["expiration"] = pedData.DriversLicenseExpiration?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty,
                        ["dlclass"] = string.Empty
                    },
                    ["weaponPermit"] = new JObject
                    {
                        ["type"] = pedData.WeaponPermit?.PermitType.ToString() ?? string.Empty,
                        ["status"] = pedData.WeaponPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"] = pedData.WeaponPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty,
                        ["wpclass"] = string.Empty
                    },
                    ["fishingPermit"] = new JObject
                    {
                        ["status"] = pedData.FishingPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"] = pedData.FishingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty
                    },
                    ["huntingPermit"] = new JObject
                    {
                        ["status"] = pedData.HuntingPermit?.Status.ToString() ?? string.Empty,
                        ["expiration"] = pedData.HuntingPermit?.ExpirationDate?.ToString("MM-dd-yyyy") ?? string.Empty,
                        ["licenseNumber"] = string.Empty
                    },
                    ["boatingPermit"] = new JObject
                    {
                        ["status"] = string.Empty,
                        ["expiration"] = string.Empty,
                        ["licenseNumber"] = string.Empty
                    }
                }
            };
            return pedJson;
        }

        #endregion

        #region STP/BaseGame PedData

        /// <summary>
        ///     Generates a JSON representation of a pedestrian using standard LSPDFR/STP persona data.
        /// </summary>
        /// <param name="ped">The physical pedestrian entity to process.</param>
        /// <returns>A <see cref="JObject" /> containing the persona information; null if the pedestrian is invalid.</returns>
        public static JObject GeneratePedData(Ped ped)
        {
            if (!ped || !ped.Exists()) return null;

            var pedPersona = Functions.GetPersonaForPed(ped);
            return pedPersona == null
                ? null
                :
                // Pass the handle if the ped exists, otherwise null
                GeneratePedDataFromObject(pedPersona, (int)ped.Handle.Value, ped);
        }

        /// <summary>
        ///     Constructs a pedestrian JSON object from a raw <see cref="Persona" /> object.
        /// </summary>
        /// <param name="pedPersona">The source persona object.</param>
        /// <param name="entityHandle">An optional handle ID for the entity.</param>
        /// <param name="physicalPed">An optional physical entity to determine relationship-based flags.</param>
        /// <returns>A populated <see cref="JObject" /> based on the persona details.</returns>
        private static JObject GeneratePedDataFromObject(Persona pedPersona, int? entityHandle, Ped physicalPed = null)
        {
            if (pedPersona == null) return null;

            var pedJson = new JObject
            {
                // -- Identification --
                ["entityId"] = entityHandle ?? -1,
                ["identification"] = new JObject
                {
                    ["name"] = pedPersona.FullName ?? string.Empty,
                    ["address"] = string.Empty,
                    ["pedModel"] = physicalPed != null ? FindPedModel(physicalPed) ?? string.Empty : string.Empty,
                    ["birthday"] = pedPersona.Birthday.Month.ToString("D2") + "/" + pedPersona.Birthday.Day.ToString("D2") + "/" + pedPersona.Birthday.Year,
                    ["gender"] = pedPersona.Gender.ToString() ?? string.Empty,
                    ["height"] = string.Empty,
                    ["weight"] = string.Empty,
                    ["eyeColor"] = string.Empty,
                    ["hairColor"] = string.Empty,
                    ["knownAliases"] = string.Empty,
                    ["ethnicity"] = string.Empty,
                    ["distinguishingMarks"] = string.Empty,
                    ["citizenshipStatus"] = string.Empty,
                    ["maritalStatus"] = string.Empty,
                    ["disabilityStatus"] = string.Empty,
                    ["isPolice"] = physicalPed != null && physicalPed.RelationshipGroup == "COP" ? "true" : "false"
                },

                // -- Criminal History --
                ["judicialStatus"] = new JObject
                {
                    ["isWanted"] = pedPersona.Wanted.ToString() ?? string.Empty,
                    ["warrantInfo"] = new JObject
                    {
                        ["warrantNumber"] = string.Empty,
                        ["dateIssued"] = string.Empty,
                        ["issuingAgency"] = string.Empty,
                        ["warrantCharge"] = string.Empty,
                        ["bailAmount"] = string.Empty
                    },
                    ["paroleInfo"] = new JObject
                    {
                        ["isOnParole"] = string.Empty,
                        ["paroleStartDate"] = string.Empty,
                        ["paroleEndDate"] = string.Empty,
                        ["paroleRestrictions"] = string.Empty,
                        ["paroleAgency"] = string.Empty,
                        ["paroleOfficer"] = string.Empty,
                        ["paroleOfficerEmail"] = string.Empty
                    },
                    ["probationInfo"] = new JObject
                    {
                        ["isOnProbation"] = string.Empty,
                        ["probationStartDate"] = string.Empty,
                        ["probationEndDate"] = string.Empty,
                        ["probationRestrictions"] = string.Empty,
                        ["probationAgency"] = string.Empty,
                        ["probationOfficer"] = string.Empty,
                        ["probationOfficerEmail"] = string.Empty
                    },
                    ["timesStopped"] = pedPersona.TimesStopped.ToString() ?? string.Empty,
                    ["restrainingOrderActive"] = string.Empty
                },

                // -- Licenses and Permits --
                ["licensesAndPermits"] = new JObject
                {
                    ["driversLicense"] = new JObject
                    {
                        ["status"] = pedPersona.ELicenseState.ToString() ?? string.Empty,
                        ["expiration"] = string.Empty,
                        ["licenseNumber"] = string.Empty,
                        ["dlclass"] = string.Empty
                    },
                    ["weaponPermit"] = new JObject
                    {
                        ["type"] = string.Empty,
                        ["status"] = string.Empty,
                        ["expiration"] = string.Empty,
                        ["licenseNumber"] = string.Empty,
                        ["wpclass"] = string.Empty
                    },
                    ["fishingPermit"] = new JObject
                    {
                        ["status"] = string.Empty,
                        ["expiration"] = string.Empty,
                        ["licenseNumber"] = string.Empty
                    },
                    ["huntingPermit"] = new JObject
                    {
                        ["status"] = string.Empty,
                        ["expiration"] = string.Empty,
                        ["licenseNumber"] = string.Empty
                    },
                    ["boatingPermit"] = new JObject
                    {
                        ["status"] = string.Empty,
                        ["expiration"] = string.Empty,
                        ["licenseNumber"] = string.Empty
                    }
                }
            };
            return pedJson;
        }

        #endregion
    }
}