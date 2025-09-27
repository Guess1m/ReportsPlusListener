using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Rage;

namespace ReportsPlus.Utils.Config
{
    public sealed class ConfigManager
    {
        private readonly string _filePath;
        private InitializationFile _iniFile;

        public ConfigManager(string filePath)
        {
            _filePath = filePath;
            _iniFile = new InitializationFile(_filePath);
            _iniFile.Create();
        }

        public T GetValue<T>(string section, string key, T defaultValue, string comment = "")
        {
            if (_iniFile.DoesKeyExist(section, key)) return ReadValue(section, key, defaultValue);
            Game.LogTrivial($"ReportsPlus {{CONFIG}}: Key '{key}' not found in section '{section}'. Creating with default value '{defaultValue}'.");

            // Convert the default value to its string representation for the INI file
            string valueStr;
            if (defaultValue is Color color)
                valueStr = $"{color.A},{color.R},{color.G},{color.B}";
            else
                valueStr = Convert.ToString(defaultValue, CultureInfo.InvariantCulture);

            // Manually write the comment and key=value pair to the file
            WriteIniEntryManual(_filePath, section, key, valueStr, comment);

            // Re-initialize the INI reader to make it aware of the new entry
            _iniFile = new InitializationFile(_filePath);

            return ReadValue(section, key, defaultValue);
        }

        private void WriteIniEntryManual(string path, string section, string key, string value, string comment)
        {
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            var formattedSection = $"[{section}]";
            var sectionIndex = lines.FindIndex(line => line.Trim().Equals(formattedSection, StringComparison.OrdinalIgnoreCase));

            var linesToAdd = new List<string>();
            if (!string.IsNullOrWhiteSpace(comment)) linesToAdd.Add($"; {comment}");

            linesToAdd.Add($"{key}={value}");

            if (sectionIndex != -1) // Section exists, add to the end of it
            {
                var nextSectionIndex = lines.FindIndex(sectionIndex + 1, line => line.Trim().StartsWith("["));
                var insertIndex = nextSectionIndex == -1 ? lines.Count : nextSectionIndex;

                if (insertIndex > sectionIndex + 1 && !string.IsNullOrWhiteSpace(lines[insertIndex - 1])) lines.Insert(insertIndex, ""); // Add a blank line for spacing

                lines.InsertRange(insertIndex, linesToAdd);
            }
            else // Section doesn't exist, add it to the end of the file
            {
                if (lines.Any() && !string.IsNullOrWhiteSpace(lines.Last())) lines.Add(""); // Add a blank line before new section

                lines.Add(formattedSection);
                lines.AddRange(linesToAdd);
            }

            File.WriteAllLines(path, lines);
        }

        private T ReadValue<T>(string section, string key, T defaultValue)
        {
            try
            {
                var type = typeof(T);

                if (type.IsEnum) return _iniFile.ReadEnum(section, key, defaultValue);

                if (type != typeof(Color)) return (T)Convert.ChangeType(_iniFile.ReadString(section, key, defaultValue.ToString()), typeof(T), CultureInfo.InvariantCulture);
                var colorStr = _iniFile.ReadString(section, key, "");
                var parts = colorStr.Split(',');
                if (parts.Length == 4 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var r) && int.TryParse(parts[2], out var g) && int.TryParse(parts[3], out var b))
                    return (T)(object)Color.FromArgb(a, r, g, b);

                return defaultValue;
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"ReportsPlus {{CONFIG}}: Failed to read or convert key '{key}' in section '{section}'. Using default value. Error: {ex.Message}");
                return defaultValue;
            }
        }
    }
}