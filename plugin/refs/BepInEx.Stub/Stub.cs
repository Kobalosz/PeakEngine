// Signatures copied from BepInEx 5 (https://github.com/BepInEx/BepInEx, LGPL-2.1).
// Bodies are intentionally empty: see the comment in BepInEx.Stub.csproj.
using System;
using UnityEngine;

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BepInPlugin : Attribute
    {
        public string GUID { get; protected set; }
        public string Name { get; protected set; }
        public Version Version { get; protected set; }
        public BepInPlugin(string GUID, string Name, string Version) { }
    }

    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        protected Logging.ManualLogSource Logger { get; }
        public Configuration.ConfigFile Config { get; }
        protected BaseUnityPlugin() { }
    }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogError(object data) { }
        public void LogWarning(object data) { }
        public void LogInfo(object data) { }
    }
}

namespace BepInEx.Configuration
{
    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description) => null;
    }

    public sealed class ConfigEntry<T>
    {
        public T Value { get; set; }
    }
}
