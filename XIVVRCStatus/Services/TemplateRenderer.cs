using System;
using System.Collections.Generic;

namespace XIVVRCStatus.Services;

public static class TemplateRenderer
{
    public static IReadOnlyList<TemplateToken> Tokens { get; } =
    [
        new("{game}", "game name"),
        new("{name}", "character name"),
        new("{job}", "current job abbreviation"),
        new("{level}", "current job level"),
        new("{effective_level}", "level after level sync"),
        new("{server}", "current world/server alias"),
        new("{home_server}", "home world/server alias"),
        new("{server_status}", "current data center and server"),
        new("{world}", "current world"),
        new("{home_world}", "home world"),
        new("{location}", "current area"),
        new("{duty}", "current duty, when available"),
        new("{duty_progress}", "duty timer/status"),
        new("{duty_elapsed}", "time since the plugin detected duty start"),
        new("{activity}", "current duty or area"),
        new("{combat}", "combat state"),
        new("{boss}", "auto-detected boss or current target name"),
        new("{boss_hp}", "auto-detected boss or current target HP percent"),
        new("{boss_status}", "combined boss/target name and HP percent, or phase text"),
        new("{instance}", "area instance number"),
        new("{skill}", "last clicked action for 5 seconds"),
        new("{gcd}", "GCD utilization with label"),
        new("{gcd_uptime}", "GCD utilization percent"),
    ];

    public static IReadOnlyList<TemplateToken> ChineseTokens { get; } =
    [
        new("{game}", "游戏名"),
        new("{name}", "角色名"),
        new("{job}", "当前职业中文名"),
        new("{level}", "当前职业等级"),
        new("{effective_level}", "等级同步后的等级"),
        new("{server}", "当前区服，等同于 {world}"),
        new("{home_server}", "角色所在区服，等同于 {home_world}"),
        new("{server_status}", "当前大区和服务器"),
        new("{world}", "当前服务器"),
        new("{home_world}", "角色所在服务器"),
        new("{location}", "当前地区"),
        new("{duty}", "当前副本，可用时显示"),
        new("{duty_progress}", "副本进行状态与计时"),
        new("{duty_elapsed}", "插件检测到副本开始后的时间"),
        new("{activity}", "当前副本或地区"),
        new("{combat}", "战斗状态"),
        new("{boss}", "自动识别的首领或当前目标名"),
        new("{boss_hp}", "自动识别的首领或当前目标血量百分比"),
        new("{boss_status}", "首领/目标名与血量组合文本，或阶段提示"),
        new("{instance}", "地图分线编号"),
        new("{skill}", "最近点击的技能，5 秒后消失"),
        new("{技能}", "同 {skill}，最近点击的技能"),
        new("{gcd}", "GCD 利用率文本"),
        new("{gcd_uptime}", "GCD 利用率百分比"),
        new("{GCD利用率}", "同 {gcd_uptime}，GCD 利用率百分比"),
    ];

    public static IReadOnlyList<TemplateToken> GetTokens(DisplayLanguage language)
    {
        return language == DisplayLanguage.Chinese ? ChineseTokens : Tokens;
    }

    public static string Render(string template, StatusSnapshot snapshot)
    {
        var replacements = new Dictionary<string, string>
        {
            ["{game}"] = snapshot.GameName,
            ["{name}"] = snapshot.CharacterName,
            ["{job}"] = snapshot.Job,
            ["{level}"] = snapshot.Level,
            ["{effective_level}"] = snapshot.EffectiveLevel,
            ["{server}"] = snapshot.CurrentWorld,
            ["{home_server}"] = snapshot.HomeWorld,
            ["{server_status}"] = snapshot.ServerStatus,
            ["{world}"] = snapshot.CurrentWorld,
            ["{home_world}"] = snapshot.HomeWorld,
            ["{location}"] = snapshot.Location,
            ["{duty}"] = snapshot.Duty,
            ["{duty_progress}"] = snapshot.DutyProgress,
            ["{duty_elapsed}"] = snapshot.DutyElapsed,
            ["{activity}"] = snapshot.Activity,
            ["{combat}"] = snapshot.Combat,
            ["{boss}"] = snapshot.Boss,
            ["{boss_hp}"] = snapshot.BossHp,
            ["{boss_status}"] = snapshot.BossStatus,
            ["{instance}"] = snapshot.Instance,
            ["{skill}"] = snapshot.Skill,
            ["{技能}"] = snapshot.Skill,
            ["{gcd}"] = snapshot.GcdStatus,
            ["{gcd_status}"] = snapshot.GcdStatus,
            ["{gcd_uptime}"] = snapshot.GcdUptime,
            ["{GCD利用率}"] = snapshot.GcdUptime,
        };

        var result = template;
        foreach (var replacement in replacements)
        {
            result = result.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
        }

        return ChatboxText.Sanitize(result);
    }
}

public sealed record TemplateToken(string Name, string Description);
