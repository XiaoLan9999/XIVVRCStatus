using System.Collections.Generic;
using Dalamud.Game;

namespace XIVVRCStatus.Services;

public static class OscDisplayLanguageExtensions
{
    private static readonly IReadOnlyDictionary<char, char> TraditionalCharacters = new Dictionary<char, char>
    {
        ['术'] = '術',
        ['师'] = '師',
        ['剑'] = '劍',
        ['枪'] = '槍',
        ['锻'] = '鍛',
        ['铁'] = '鐵',
        ['铸'] = '鑄',
        ['炼'] = '煉',
        ['调'] = '調',
        ['矿'] = '礦',
        ['园'] = '園',
        ['艺'] = '藝',
        ['鱼'] = '魚',
        ['骑'] = '騎',
        ['战'] = '戰',
        ['龙'] = '龍',
        ['诗'] = '詩',
        ['双'] = '雙',
        ['机'] = '機',
        ['贤'] = '賢',
        ['镰'] = '鐮',
        ['绘'] = '繪',
        ['灵'] = '靈',
        ['驯'] = '馴',
        ['兽'] = '獸',
        ['绝'] = '絕',
        ['级'] = '級',
        ['务'] = '務',
        ['场'] = '場',
        ['区'] = '區',
        ['进'] = '進',
        ['状'] = '狀',
        ['态'] = '態',
        ['与'] = '與',
        ['计'] = '計',
        ['时'] = '時',
        ['当'] = '當',
        ['斗'] = '鬥',
        ['剧'] = '劇',
        ['结'] = '結',
        ['阶'] = '階',
        ['领'] = '領',
        ['标'] = '標',
        ['体'] = '體',
        ['萨'] = '薩',
        ['罗'] = '羅',
        ['层'] = '層',
        ['风'] = '風',
        ['岛'] = '島',
        ['陆'] = '陸',
        ['鸟'] = '鳥',
        ['宫'] = '宮',
        ['图'] = '圖',
        ['线'] = '線',
        ['门'] = '門',
        ['备'] = '備',
        ['称'] = '稱',
        ['个'] = '個',
        ['从'] = '從',
        ['发'] = '發',
        ['这'] = '這',
        ['过'] = '過',
        ['间'] = '間',
    };

    public static string GetDisplayName(this OscDisplayLanguage language)
    {
        return language switch
        {
            OscDisplayLanguage.English => "English",
            OscDisplayLanguage.SimplifiedChinese => "简体中文",
            OscDisplayLanguage.TraditionalChinese => "繁體中文",
            OscDisplayLanguage.Japanese => "日本語",
            _ => language.ToString(),
        };
    }

    public static ClientLanguage? ToClientLanguage(this OscDisplayLanguage language)
    {
        return language switch
        {
            OscDisplayLanguage.English => ClientLanguage.English,
            OscDisplayLanguage.Japanese => ClientLanguage.Japanese,
            _ => null,
        };
    }

    public static string ApplyScript(this OscDisplayLanguage language, string text)
    {
        return language == OscDisplayLanguage.TraditionalChinese ? ToTraditionalChinese(text) : text;
    }

    public static bool IsChinese(this OscDisplayLanguage language)
    {
        return language is OscDisplayLanguage.SimplifiedChinese or OscDisplayLanguage.TraditionalChinese;
    }

    private static string ToTraditionalChinese(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (TraditionalCharacters.TryGetValue(chars[i], out var traditional))
            {
                chars[i] = traditional;
            }
        }

        return new string(chars);
    }
}
