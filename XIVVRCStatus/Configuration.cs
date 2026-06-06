using Dalamud.Configuration;
using System;

namespace XIVVRCStatus;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    public bool Enabled { get; set; }
    public DisplayLanguage DisplayLanguage { get; set; } = DisplayLanguage.English;
    public OscDisplayLanguage OscDisplayLanguage { get; set; } = OscDisplayLanguage.English;
    public string TargetIp { get; set; } = "127.0.0.1";
    public int TargetPort { get; set; } = 9000;
    public int RefreshIntervalSeconds { get; set; } = 15;
    public string StatusTemplate { get; set; } = "{game} | {job} Lv{level} | {server_status} | {activity}";
    public string ChineseStatusTemplate { get; set; } = "{game}\n{job} Lv{level} | {server_status}\n{activity}\n{duty_progress} {boss_status}";
    public bool SendImmediately { get; set; } = true;
    public bool PlayNotificationSound { get; set; }

    public string ActiveStatusTemplate
    {
        get => StatusTemplate;
        set => StatusTemplate = value;
    }

    public void Normalize()
    {
        if (Version >= 3)
        {
            return;
        }

        if (DisplayLanguage == DisplayLanguage.Chinese && !string.IsNullOrWhiteSpace(ChineseStatusTemplate))
        {
            StatusTemplate = ChineseStatusTemplate;
            OscDisplayLanguage = OscDisplayLanguage.SimplifiedChinese;
        }
        else
        {
            OscDisplayLanguage = OscDisplayLanguage.English;
        }

        Version = 3;
        Save();
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

public enum DisplayLanguage
{
    English,
    Chinese,
}

public enum OscDisplayLanguage
{
    English,
    SimplifiedChinese,
    TraditionalChinese,
    Japanese,
}
