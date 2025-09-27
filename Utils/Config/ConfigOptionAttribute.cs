using System;

namespace ReportsPlus.Utils.Config
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ConfigOptionAttribute : Attribute
    {
        public ConfigOptionAttribute(string section, string key, string comment = "")
        {
            Section = section;
            Key = key;
            Comment = comment;
        }

        public string Section { get; }
        public string Key { get; }
        public string Comment { get; }
    }
}