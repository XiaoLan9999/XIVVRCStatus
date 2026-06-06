using System;
using System.Globalization;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace XIVVRCStatus.Services;

public sealed class StatusProvider
{
    private readonly Configuration configuration;
    private readonly ActionTrackerService actionTracker;
    private DateTime? dutyStartedUtc;

    public StatusProvider(Configuration configuration, ActionTrackerService actionTracker)
    {
        this.configuration = configuration;
        this.actionTracker = actionTracker;
    }

    public bool TryCapture(out StatusSnapshot snapshot, out string error)
    {
        snapshot = null!;
        error = string.Empty;

        var playerState = Plugin.PlayerState;
        if (!playerState.IsLoaded || !playerState.ClassJob.IsValid)
        {
            error = IsUiChinese ? "正在等待已登录的最终幻想14角色。" : "Waiting for a logged-in FFXIV character.";
            return false;
        }

        var job = LocalizeJob(playerState.ClassJob.Value.Abbreviation.ToString());
        var currentWorld = string.Empty;
        var currentDataCenter = string.Empty;
        if (playerState.CurrentWorld.IsValid)
        {
            var world = playerState.CurrentWorld.Value;
            currentWorld = LocalizeWorldName(world.RowId, world.Name.ToString());
            if (world.DataCenter.IsValid)
            {
                var dataCenter = world.DataCenter.Value;
                currentDataCenter = LocalizeDataCenterName(dataCenter.RowId, dataCenter.Name.ToString());
            }
        }

        var homeWorld = playerState.HomeWorld.IsValid
            ? LocalizeWorldName(playerState.HomeWorld.Value.RowId, playerState.HomeWorld.Value.Name.ToString())
            : string.Empty;
        var serverStatus = BuildServerStatus(currentDataCenter, currentWorld);

        var location = string.Empty;
        if (Plugin.DataManager.GetExcelSheet<TerritoryType>(DataLanguage).TryGetRow(Plugin.ClientState.TerritoryType, out var territory))
        {
            location = LocalizePlaceName(territory.PlaceName.RowId, territory.PlaceName.Value.Name.ToString());
        }

        var duty = Plugin.DutyState.ContentFinderCondition.IsValid
            ? LocalizeKnownGameText(LocalizeGameText(GetDutyName()))
            : string.Empty;

        var hasDuty = !string.IsNullOrWhiteSpace(duty);
        var activity = hasDuty ? duty : location;
        var (dutyProgress, dutyElapsed) = CaptureDutyProgress(hasDuty);
        var boss = CaptureBossStatus();
        var actionState = actionTracker.GetSnapshot();
        var instance = Plugin.ClientState.Instance > 0
            ? Plugin.ClientState.Instance.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        snapshot = new StatusSnapshot(
            GameName,
            playerState.CharacterName,
            job,
            playerState.Level.ToString(CultureInfo.InvariantCulture),
            playerState.EffectiveLevel.ToString(CultureInfo.InvariantCulture),
            currentWorld,
            homeWorld,
            serverStatus,
            location,
            duty,
            dutyProgress,
            dutyElapsed,
            activity,
            Plugin.Condition[ConditionFlag.InCombat]
                ? L("In combat", "战斗中", "戰鬥中", "戦闘中")
                : L("Exploring", "探索中", "探索中", "探索中"),
            boss.Name,
            boss.HpText,
            boss.StatusText,
            instance,
            actionState.Skill,
            actionState.GcdUptime,
            actionState.GcdStatus);

        return true;
    }

    private bool IsUiChinese => configuration.DisplayLanguage == DisplayLanguage.Chinese;

    private OscDisplayLanguage OutputLanguage => configuration.OscDisplayLanguage;

    private ClientLanguage? DataLanguage => OutputLanguage.ToClientLanguage();

    private string GameName => L("FFXIV", "最终幻想14", "最終幻想14", "ファイナルファンタジーXIV");

    private static string BuildServerStatus(string dataCenter, string currentWorld)
    {
        if (string.IsNullOrWhiteSpace(currentWorld))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(dataCenter))
        {
            return currentWorld;
        }

        return $"{dataCenter} {currentWorld}";
    }

    private (string Progress, string Elapsed) CaptureDutyProgress(bool hasDuty)
    {
        if (!hasDuty)
        {
            dutyStartedUtc = null;
            return (string.Empty, string.Empty);
        }

        if (IsInCutscene())
        {
            return (L("Cutscene", "剧情中", "劇情中", "カットシーン中"), string.Empty);
        }

        if (!Plugin.DutyState.IsDutyStarted)
        {
            var hadStarted = dutyStartedUtc != null;
            dutyStartedUtc = null;
            return hadStarted
                ? (L("Duty complete", "副本结束", "副本結束", "コンテンツ終了"), string.Empty)
                : (string.Empty, string.Empty);
        }

        dutyStartedUtc ??= DateTime.UtcNow;
        var elapsed = DateTime.UtcNow - dutyStartedUtc.Value;
        var elapsedText = elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : elapsed.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

        return (L($"In duty {elapsedText}", $"副本中 {elapsedText}", $"副本中 {elapsedText}", $"コンテンツ中 {elapsedText}"), elapsedText);
    }

    private static bool IsInCutscene()
    {
        return Plugin.Condition[ConditionFlag.WatchingCutscene]
            || Plugin.Condition[ConditionFlag.WatchingCutscene78]
            || Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent];
    }

    private BossStatus CaptureBossStatus()
    {
        return TryCaptureBossStatus(Plugin.TargetManager.Target)
            ?? CaptureLargestEnemyStatus()
            ?? TryCaptureBossStatus(Plugin.TargetManager.FocusTarget)
            ?? CapturePhaseStatus();
    }

    private BossStatus? CaptureLargestEnemyStatus()
    {
        IBattleNpc? bestEnemy = null;
        double bestScore = 0;

        foreach (var gameObject in Plugin.ObjectTable.CharacterManagerObjects)
        {
            if (gameObject is not IBattleNpc enemy || !IsUsableCombatTarget(enemy) || !IsEnemyBattleNpc(enemy))
            {
                continue;
            }

            var score = GetEnemyScore(enemy);
            if (score <= bestScore)
            {
                continue;
            }

            bestEnemy = enemy;
            bestScore = score;
        }

        return bestEnemy == null ? null : BuildBossStatus(bestEnemy, L("Boss", "首领", "首領", "ボス"));
    }

    private BossStatus? TryCaptureBossStatus(IGameObject? target)
    {
        if (target is not IBattleNpc battleNpc || !IsUsableCombatTarget(battleNpc) || !IsEnemyBattleNpc(battleNpc))
        {
            return null;
        }

        return BuildBossStatus(battleNpc, L("Target", "目标", "目標", "ターゲット"));
    }

    private BossStatus BuildBossStatus(IBattleNpc battleNpc, string label)
    {
        var percentage = Math.Clamp((double)battleNpc.CurrentHp / battleNpc.MaxHp * 100d, 0d, 100d);
        var hpText = $"{percentage:0.0}%";
        var name = LocalizeGameText(battleNpc.Name.ToString());
        if (string.IsNullOrWhiteSpace(name))
        {
            name = L("Target", "当前目标", "當前目標", "ターゲット");
        }

        return new BossStatus(name, hpText, $"{label} {name} {hpText}");
    }

    private BossStatus CapturePhaseStatus()
    {
        if (!Plugin.DutyState.IsDutyStarted || !Plugin.Condition[ConditionFlag.InCombat])
        {
            return BossStatus.Empty;
        }

        return new BossStatus(string.Empty, string.Empty, L("Phase transition", "阶段中", "階段中", "フェーズ移行中"));
    }

    private static bool IsUsableCombatTarget(ICharacter target)
    {
        if (target.CurrentHp <= 0 || target.MaxHp <= 0 || target.IsDead || !target.IsTargetable)
        {
            return false;
        }

        return true;
    }

    private static bool IsEnemyBattleNpc(IBattleNpc battleNpc)
    {
        return battleNpc.ObjectKind.ToString().Contains("BattleNpc", StringComparison.OrdinalIgnoreCase)
            && battleNpc.BattleNpcKind.ToString().Contains("Enemy", StringComparison.OrdinalIgnoreCase);
    }

    private static double GetEnemyScore(IBattleNpc enemy)
    {
        var score = enemy.MaxHp;

        if (Plugin.Condition[ConditionFlag.InCombat])
        {
            score += 10_000;
        }

        return score;
    }

    private string LocalizeJob(string abbreviation)
    {
        return OutputLanguage switch
        {
            OscDisplayLanguage.SimplifiedChinese => abbreviation switch
            {
                "GLA" => "剑术师",
                "PGL" => "格斗家",
                "MRD" => "斧术师",
                "LNC" => "枪术师",
                "ARC" => "弓箭手",
                "CNJ" => "幻术师",
                "THM" => "咒术师",
                "CRP" => "刻木匠",
                "BSM" => "锻铁匠",
                "ARM" => "铸甲匠",
                "GSM" => "雕金匠",
                "LTW" => "制革匠",
                "WVR" => "裁衣匠",
                "ALC" => "炼金术士",
                "CUL" => "烹调师",
                "MIN" => "采矿工",
                "BTN" => "园艺工",
                "FSH" => "捕鱼人",
                "PLD" => "骑士",
                "MNK" => "武僧",
                "WAR" => "战士",
                "DRG" => "龙骑",
                "BRD" => "诗人",
                "WHM" => "白魔",
                "BLM" => "黑魔",
                "ACN" => "秘术师",
                "SMN" => "召唤",
                "SCH" => "学者",
                "ROG" => "双剑师",
                "NIN" => "忍者",
                "MCH" => "机工",
                "DRK" => "暗骑",
                "AST" => "占星",
                "SAM" => "武士",
                "RDM" => "赤魔",
                "BLU" => "青魔",
                "GNB" => "绝枪",
                "DNC" => "舞者",
                "RPR" => "镰刀",
                "SGE" => "贤者",
                "VPR" => "蝰蛇",
                "PCT" => "绘灵",
                "BST" => "驯兽",
                _ => abbreviation,
            },
            OscDisplayLanguage.TraditionalChinese => abbreviation switch
            {
                "GLA" => "劍術師",
                "PGL" => "格鬥家",
                "MRD" => "斧術師",
                "LNC" => "槍術師",
                "ARC" => "弓箭手",
                "CNJ" => "幻術師",
                "THM" => "咒術師",
                "CRP" => "刻木匠",
                "BSM" => "鍛鐵匠",
                "ARM" => "鑄甲匠",
                "GSM" => "雕金匠",
                "LTW" => "製革匠",
                "WVR" => "裁衣匠",
                "ALC" => "煉金術士",
                "CUL" => "烹調師",
                "MIN" => "採礦工",
                "BTN" => "園藝工",
                "FSH" => "捕魚人",
                "PLD" => "騎士",
                "MNK" => "武僧",
                "WAR" => "戰士",
                "DRG" => "龍騎",
                "BRD" => "詩人",
                "WHM" => "白魔",
                "BLM" => "黑魔",
                "ACN" => "秘術師",
                "SMN" => "召喚",
                "SCH" => "學者",
                "ROG" => "雙劍師",
                "NIN" => "忍者",
                "MCH" => "機工",
                "DRK" => "暗騎",
                "AST" => "占星",
                "SAM" => "武士",
                "RDM" => "赤魔",
                "BLU" => "青魔",
                "GNB" => "絕槍",
                "DNC" => "舞者",
                "RPR" => "鐮刀",
                "SGE" => "賢者",
                "VPR" => "蝰蛇",
                "PCT" => "繪靈",
                "BST" => "馴獸",
                _ => abbreviation,
            },
            OscDisplayLanguage.Japanese => abbreviation switch
            {
                "GLA" => "剣術士",
                "PGL" => "格闘士",
                "MRD" => "斧術士",
                "LNC" => "槍術士",
                "ARC" => "弓術士",
                "CNJ" => "幻術士",
                "THM" => "呪術士",
                "CRP" => "木工師",
                "BSM" => "鍛冶師",
                "ARM" => "甲冑師",
                "GSM" => "彫金師",
                "LTW" => "革細工師",
                "WVR" => "裁縫師",
                "ALC" => "錬金術師",
                "CUL" => "調理師",
                "MIN" => "採掘師",
                "BTN" => "園芸師",
                "FSH" => "漁師",
                "PLD" => "ナイト",
                "MNK" => "モンク",
                "WAR" => "戦士",
                "DRG" => "竜騎士",
                "BRD" => "吟遊詩人",
                "WHM" => "白魔道士",
                "BLM" => "黒魔道士",
                "ACN" => "巴術士",
                "SMN" => "召喚士",
                "SCH" => "学者",
                "ROG" => "双剣士",
                "NIN" => "忍者",
                "MCH" => "機工士",
                "DRK" => "暗黒騎士",
                "AST" => "占星術師",
                "SAM" => "侍",
                "RDM" => "赤魔道士",
                "BLU" => "青魔道士",
                "GNB" => "ガンブレイカー",
                "DNC" => "踊り子",
                "RPR" => "リーパー",
                "SGE" => "賢者",
                "VPR" => "ヴァイパー",
                "PCT" => "ピクトマンサー",
                "BST" => "魔獣使い",
                _ => abbreviation,
            },
            _ => abbreviation,
        };
    }

    private string GetDutyName()
    {
        if (!Plugin.DutyState.ContentFinderCondition.IsValid)
        {
            return string.Empty;
        }

        var rowId = Plugin.DutyState.ContentFinderCondition.RowId;
        return Plugin.DataManager.GetExcelSheet<ContentFinderCondition>(DataLanguage).TryGetRow(rowId, out var duty)
            ? duty.Name.ToString()
            : Plugin.DutyState.ContentFinderCondition.Value.Name.ToString();
    }

    private string LocalizePlaceName(uint rowId, string fallback)
    {
        if (rowId != 0 && Plugin.DataManager.GetExcelSheet<PlaceName>(DataLanguage).TryGetRow(rowId, out var placeName))
        {
            var text = LocalizeGameText(placeName.Name.ToString());
            if (!string.IsNullOrWhiteSpace(text))
            {
                return LocalizeKnownGameText(text);
            }
        }

        return LocalizeKnownGameText(LocalizeGameText(fallback));
    }

    private string LocalizeWorldName(uint rowId, string fallback)
    {
        if (rowId != 0 && Plugin.DataManager.GetExcelSheet<World>(DataLanguage).TryGetRow(rowId, out var world))
        {
            var text = LocalizeGameText(world.Name.ToString());
            if (!string.IsNullOrWhiteSpace(text))
            {
                return LocalizeKnownGameText(text);
            }
        }

        return LocalizeKnownGameText(LocalizeGameText(fallback));
    }

    private string LocalizeDataCenterName(uint rowId, string fallback)
    {
        if (rowId != 0 && Plugin.DataManager.GetExcelSheet<WorldDCGroupType>(DataLanguage).TryGetRow(rowId, out var dataCenter))
        {
            var text = LocalizeGameText(dataCenter.Name.ToString());
            if (!string.IsNullOrWhiteSpace(text))
            {
                return LocalizeKnownGameText(text);
            }
        }

        return LocalizeKnownGameText(LocalizeGameText(fallback));
    }

    private string LocalizeGameText(string text)
    {
        return OutputLanguage.ApplyScript(text);
    }

    private string LocalizeKnownGameText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return OutputLanguage switch
        {
            OscDisplayLanguage.English => text switch
            {
                "陆行鸟" => "Chocobo",
                "晨曦王座" => "Dawn Throne",
                "利姆萨·罗敏萨下层甲板" => "Limsa Lominsa Lower Decks",
                _ => text,
            },
            OscDisplayLanguage.Japanese => text switch
            {
                "陆行鸟" => "チョコボ",
                "晨曦王座" => "暁の王座",
                "利姆萨·罗敏萨下层甲板" => "リムサ・ロミンサ：下甲板",
                _ => text,
            },
            OscDisplayLanguage.TraditionalChinese => text switch
            {
                "陆行鸟" => "陸行鳥",
                "晨曦王座" => "晨曦王座",
                "利姆萨·罗敏萨下层甲板" => "利姆薩·羅敏薩下層甲板",
                _ => text,
            },
            _ => text,
        };
    }

    private string L(string english, string simplifiedChinese, string traditionalChinese, string japanese)
    {
        return OutputLanguage switch
        {
            OscDisplayLanguage.SimplifiedChinese => simplifiedChinese,
            OscDisplayLanguage.TraditionalChinese => traditionalChinese,
            OscDisplayLanguage.Japanese => japanese,
            _ => english,
        };
    }

    private sealed record BossStatus(string Name, string HpText, string StatusText)
    {
        public static BossStatus Empty { get; } = new(string.Empty, string.Empty, string.Empty);
    }
}
