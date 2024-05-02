using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json.Linq;

namespace Kilo.Commons.Config;

public enum AddonType
{
    callouts,
    plugins
}

public enum KVPType
{
    String,
    Array,
    Float,
    Int
}

public class Config : JObject
{
    public static List<Config> configs = new List<Config>();
    public static EventHandlerDictionary eventHandlers;

    public static string SourceName = Assembly.GetCallingAssembly().GetName().Name.Replace(".net", "");
    public string CustomFolderName;
    private static string configString = "{}";
    private static JObject defaultConfig = JObject.Parse(configString);
    private static string kvpPrefix = SourceName + ".KiloCommons_";
    private int code; // This is the secret code for this config.
    private static string configPath;
    private static string configFileName = "config.json";
    private static string resourceName = "fivepd";
    private bool updatedConfig = false;
    public static bool DataNull = false;
    public static bool pauseOperations = false;

    public static JObject LoadConfig(AddonType addonType, string sourceName, string encodedJSONString,
        string fileName = "config.json", string resourceName = "fivepd")
    {
        JObject config = defaultConfig; // Public static default config;
        Config.resourceName = resourceName;
        Config.configFileName = fileName;
        try
        {
            string data = API.LoadResourceFile(resourceName, $"/{addonType.ToString()}/{sourceName}/{fileName}");
            if (data == null)
            {
                Debug.WriteLine("Data is null!");
                DataNull = true;
                data = encodedJSONString;
                configString = encodedJSONString;
            }

            config = JObject.Parse(data);
        }
        catch (Exception ex)
        {
            var action = new Action(async () =>
            {
                await BaseScript.Delay(5000);
                Debug.WriteLine(
                    $"^6Couldn't find config. ^5Expected path: {configPath} \n^2Returning to default config.");
                config = JObject.Parse(encodedJSONString);
            });
            action();
        }

        return config;
    }

    public Config(AddonType type, string encodedConfigJSON, string customFolderName, string fileName = "config.json",
        string resourceName = "fivepd") : base(LoadConfig(type, customFolderName, encodedConfigJSON, fileName,
        resourceName))
    {
        SourceName = Assembly.GetCallingAssembly().GetName().Name.Replace(".net", "");
        CustomFolderName = SourceName;
        if (customFolderName != "")
            CustomFolderName = customFolderName;
        configPath = $"{resourceName}/{type}/{CustomFolderName}/{fileName}";
        kvpPrefix = CustomFolderName + ".KiloCommons_";

        configString = encodedConfigJSON;
        defaultConfig = JObject.Parse(encodedConfigJSON);
        // Register Config
        configs.Add(this);
        if (this == null)
        {
            Debug.WriteLine("There was an issue with your provided default config!");
        }
    }
}

public class script : BaseScript
{
    public script()
    {
        Config.eventHandlers = EventHandlers;
    }
}