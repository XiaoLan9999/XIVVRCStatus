using System;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace XIVVRCStatus.Services;

public sealed unsafe class ActionTrackerService : IDisposable
{
    private static readonly TimeSpan SkillDisplayDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GcdDuplicateWindow = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan GcdQueueWindow = TimeSpan.FromMilliseconds(750);
    private const double MinimumGcdIntervalSeconds = 0.6d;

    private readonly Configuration configuration;
    private readonly Hook<UseActionDelegate>? useActionHook;
    private readonly Hook<StartCooldownDelegate>? startCooldownHook;

    private string lastSkill = string.Empty;
    private DateTime lastSkillUtc = DateTime.MinValue;

    private DateTime? firstGcdUtc;
    private DateTime? lastGcdUtc;
    private DateTime? lastGcdMeasurementUtc;
    private double lastGcdRecastSeconds;
    private double gcdMeasuredSeconds;
    private double gcdDowntimeSeconds;
    private DateTime lastGcdObservedUtc = DateTime.MinValue;

    private uint lastObservedCastActionId;
    private ActionType lastObservedCastActionType;
    private uint lastSkillActionId;
    private ActionType lastSkillActionType;

    public ActionTrackerService(Configuration configuration)
    {
        this.configuration = configuration;

        try
        {
            useActionHook = Plugin.GameInteropProvider.HookFromAddress<UseActionDelegate>(
                (void*)ActionManager.MemberFunctionPointers.UseAction,
                UseActionDetour);
            useActionHook.Enable();

            startCooldownHook = Plugin.GameInteropProvider.HookFromAddress<StartCooldownDelegate>(
                (void*)ActionManager.MemberFunctionPointers.StartCooldown,
                StartCooldownDetour);
            startCooldownHook.Enable();
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Failed to install action hooks. Cast polling fallback will still be used.");
        }
    }

    public event Action? StatusChanged;

    private delegate bool UseActionDelegate(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted);

    private delegate void StartCooldownDelegate(ActionManager* actionManager, ActionType actionType, uint actionId);

    public void Dispose()
    {
        useActionHook?.Dispose();
        startCooldownHook?.Dispose();
    }

    public void Update()
    {
        var now = DateTime.UtcNow;

        UpdateCombatState(now);
        CaptureCastFallback(now);

        if (lastSkill.Length > 0 && now - lastSkillUtc > SkillDisplayDuration)
        {
            lastSkill = string.Empty;
            lastSkillActionId = 0;
            StatusChanged?.Invoke();
        }
    }

    public ActionTrackerSnapshot GetSnapshot()
    {
        var now = DateTime.UtcNow;
        var skill = lastSkillActionId != 0 && now - lastSkillUtc <= SkillDisplayDuration
            ? ResolveActionName(lastSkillActionType, lastSkillActionId)
            : string.Empty;

        var uptimeText = GetGcdUptimeText(now);
        var statusText = string.IsNullOrEmpty(uptimeText)
            ? string.Empty
            : configuration.OscDisplayLanguage switch
            {
                OscDisplayLanguage.SimplifiedChinese => $"GCD利用率 {uptimeText}",
                OscDisplayLanguage.TraditionalChinese => $"GCD利用率 {uptimeText}",
                OscDisplayLanguage.Japanese => $"GCD稼働率 {uptimeText}",
                _ => $"GCD {uptimeText}",
            };

        return new ActionTrackerSnapshot(skill, uptimeText, statusText);
    }

    private bool UseActionDetour(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted)
    {
        try
        {
            RecordSkill(actionType, actionId);
        }
        catch (Exception exception)
        {
            Plugin.Log.Verbose(exception, "Failed to record used action.");
        }

        var result = useActionHook!.Original(
            actionManager,
            actionType,
            actionId,
            targetId,
            extraParam,
            mode,
            comboRouteId,
            outOptAreaTargeted);

        if (result)
        {
            try
            {
                RecordGcdAction(actionType, actionId, DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                Plugin.Log.Verbose(exception, "Failed to record used GCD action.");
            }
        }

        return result;
    }

    private void StartCooldownDetour(ActionManager* actionManager, ActionType actionType, uint actionId)
    {
        startCooldownHook!.Original(actionManager, actionType, actionId);

        try
        {
            RecordGcdAction(actionType, actionId, DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            Plugin.Log.Verbose(exception, "Failed to record GCD cooldown.");
        }
    }

    private void RecordSkill(ActionType actionType, uint actionId)
    {
        if (actionId == 0 || !IsDisplayableActionType(actionType))
        {
            return;
        }

        var name = ResolveActionName(actionType, actionId);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lastSkill = name;
        lastSkillActionType = actionType;
        lastSkillActionId = actionId;
        lastSkillUtc = DateTime.UtcNow;
        StatusChanged?.Invoke();
    }

    private void RecordGcdAction(ActionType actionType, uint actionId, DateTime now)
    {
        if (!Plugin.Condition[ConditionFlag.InCombat])
        {
            return;
        }

        if (!TryGetGcdRecastSeconds(actionType, actionId, out var recastSeconds))
        {
            return;
        }

        if (now - lastGcdObservedUtc < GcdDuplicateWindow)
        {
            return;
        }

        if (firstGcdUtc == null || lastGcdUtc == null)
        {
            StartGcdMeasurement(now, recastSeconds);
            lastGcdObservedUtc = now;
            StatusChanged?.Invoke();
            return;
        }

        UpdateGcdMeasurement(now);

        var previousGcdUtc = lastGcdUtc.Value;
        var availableAtUtc = previousGcdUtc.AddSeconds(lastGcdRecastSeconds);
        var effectiveGcdUtc = now;
        if (effectiveGcdUtc < availableAtUtc)
        {
            var queuedEarlyBy = availableAtUtc - effectiveGcdUtc;
            if (queuedEarlyBy > GcdQueueWindow)
            {
                return;
            }

            effectiveGcdUtc = availableAtUtc;
        }

        if ((effectiveGcdUtc - previousGcdUtc).TotalSeconds < MinimumGcdIntervalSeconds)
        {
            return;
        }

        lastGcdUtc = effectiveGcdUtc;
        lastGcdMeasurementUtc = now;
        lastGcdRecastSeconds = recastSeconds;
        lastGcdObservedUtc = now;
        StatusChanged?.Invoke();
    }

    private void UpdateCombatState(DateTime now)
    {
        if (!Plugin.Condition[ConditionFlag.InCombat])
        {
            ResetCombat();
            return;
        }

        UpdateGcdMeasurement(now);
    }

    private void CaptureCastFallback(DateTime now)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } localPlayer)
        {
            return;
        }

        if (localPlayer.CastActionId == 0)
        {
            lastObservedCastActionId = 0;
            lastObservedCastActionType = default;
            return;
        }

        var actionType = (ActionType)localPlayer.CastActionType;
        var actionId = localPlayer.CastActionId;
        if (actionId == lastObservedCastActionId && actionType == lastObservedCastActionType)
        {
            return;
        }

        lastObservedCastActionId = actionId;
        lastObservedCastActionType = actionType;
        RecordSkill(actionType, actionId);
        RecordGcdAction(actionType, actionId, now);
    }

    private void ResetCombat()
    {
        firstGcdUtc = null;
        lastGcdUtc = null;
        lastGcdMeasurementUtc = null;
        lastGcdRecastSeconds = 0d;
        gcdMeasuredSeconds = 0d;
        gcdDowntimeSeconds = 0d;
        lastGcdObservedUtc = DateTime.MinValue;
    }

    private static bool TryGetGcdRecastSeconds(ActionType actionType, uint actionId, out double recastSeconds)
    {
        recastSeconds = 0d;

        if (actionId == 0)
        {
            return false;
        }

        var adjustedRecastMs = ActionManager.GetAdjustedRecastTime(actionType, actionId);
        if (adjustedRecastMs <= 0)
        {
            return false;
        }

        recastSeconds = adjustedRecastMs / 1000d;
        return recastSeconds is >= 1.0d and <= 3.5d;
    }

    private string GetGcdUptimeText(DateTime now)
    {
        UpdateGcdMeasurement(now);

        if (!IsGcdMeasurementActive(now) || firstGcdUtc == null || lastGcdUtc == null)
        {
            return string.Empty;
        }

        if (gcdMeasuredSeconds <= 0.1d)
        {
            return string.Empty;
        }

        var activeSeconds = Math.Max(0d, gcdMeasuredSeconds - gcdDowntimeSeconds);
        var uptime = Math.Clamp(activeSeconds / gcdMeasuredSeconds * 100d, 0d, 100d);
        return $"{uptime:0.0}%";
    }

    private void StartGcdMeasurement(DateTime now, double recastSeconds)
    {
        firstGcdUtc = now;
        lastGcdUtc = now;
        lastGcdMeasurementUtc = now;
        lastGcdRecastSeconds = recastSeconds;
    }

    private void UpdateGcdMeasurement(DateTime now)
    {
        if (firstGcdUtc == null || lastGcdUtc == null)
        {
            return;
        }

        if (lastGcdMeasurementUtc is not { } previous)
        {
            lastGcdMeasurementUtc = now;
            return;
        }

        if (now <= previous)
        {
            return;
        }

        if (IsGcdMeasurementActive(now))
        {
            var measuredSeconds = (now - previous).TotalSeconds;
            gcdMeasuredSeconds += measuredSeconds;

            var availableAtUtc = lastGcdUtc.Value.AddSeconds(lastGcdRecastSeconds);
            var downtimeStartedUtc = previous > availableAtUtc ? previous : availableAtUtc;
            if (now > downtimeStartedUtc)
            {
                gcdDowntimeSeconds += (now - downtimeStartedUtc).TotalSeconds;
            }
        }

        lastGcdMeasurementUtc = now;
    }

    private bool IsGcdMeasurementActive(DateTime now)
    {
        if (!Plugin.Condition[ConditionFlag.InCombat] || IsInCutscene())
        {
            return false;
        }

        if (HasUsableCombatEnemy())
        {
            return true;
        }

        return lastGcdUtc is { } lastGcd
            && lastGcdRecastSeconds > 0d
            && now <= lastGcd.AddSeconds(lastGcdRecastSeconds);
    }

    private static bool IsInCutscene()
    {
        return Plugin.Condition[ConditionFlag.WatchingCutscene]
            || Plugin.Condition[ConditionFlag.WatchingCutscene78]
            || Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent];
    }

    private static bool HasUsableCombatEnemy()
    {
        if (IsUsableEnemy(Plugin.TargetManager.Target) || IsUsableEnemy(Plugin.TargetManager.FocusTarget))
        {
            return true;
        }

        foreach (var gameObject in Plugin.ObjectTable.CharacterManagerObjects)
        {
            if (gameObject is IBattleNpc enemy
                && IsUsableEnemy(enemy)
                && enemy.CurrentHp < enemy.MaxHp)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUsableEnemy(IGameObject? gameObject)
    {
        return gameObject is IBattleNpc enemy
            && IsUsableCombatTarget(enemy)
            && IsEnemyBattleNpc(enemy);
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

    private string ResolveActionName(ActionType actionType, uint actionId)
    {
        if (TryMapActionKind(actionType, out var actionKind))
        {
            var evaluated = Plugin.SeStringEvaluator.EvaluateActStr(actionKind, actionId, configuration.OscDisplayLanguage.ToClientLanguage());
            var text = configuration.OscDisplayLanguage.ApplyScript(evaluated.ToString());
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        if (Plugin.DataManager.GetExcelSheet<LuminaAction>(configuration.OscDisplayLanguage.ToClientLanguage()).TryGetRow(actionId, out var action))
        {
            var name = configuration.OscDisplayLanguage.ApplyScript(action.Name.ToString());
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return actionType == ActionType.PvPAction
            ? $"PvP Action {actionId}"
            : $"Action {actionId}";
    }

    private static bool IsDisplayableActionType(ActionType actionType)
    {
        return actionType is ActionType.Action
            or ActionType.GeneralAction
            or ActionType.EventAction
            or ActionType.BuddyAction
            or ActionType.CraftAction
            or ActionType.PetAction
            or ActionType.PvPAction
            or ActionType.BgcArmyAction;
    }

    private static bool TryMapActionKind(ActionType actionType, out ActionKind actionKind)
    {
        switch (actionType)
        {
            case ActionType.Action:
                actionKind = ActionKind.Action;
                return true;
            case ActionType.Item:
                actionKind = ActionKind.Item;
                return true;
            case ActionType.EventItem:
                actionKind = ActionKind.EventItem;
                return true;
            case ActionType.EventAction:
                actionKind = ActionKind.EventAction;
                return true;
            case ActionType.GeneralAction:
                actionKind = ActionKind.GeneralAction;
                return true;
            case ActionType.BuddyAction:
                actionKind = ActionKind.BuddyAction;
                return true;
            case ActionType.MainCommand:
                actionKind = ActionKind.MainCommand;
                return true;
            case ActionType.Companion:
                actionKind = ActionKind.Companion;
                return true;
            case ActionType.CraftAction:
                actionKind = ActionKind.CraftAction;
                return true;
            case ActionType.PetAction:
                actionKind = ActionKind.PetAction;
                return true;
            case ActionType.Mount:
                actionKind = ActionKind.Mount;
                return true;
            case ActionType.BgcArmyAction:
                actionKind = ActionKind.BgcArmyAction;
                return true;
            case ActionType.Ornament:
                actionKind = ActionKind.Ornament;
                return true;
            default:
                actionKind = default;
                return false;
        }
    }

}

public sealed record ActionTrackerSnapshot(string Skill, string GcdUptime, string GcdStatus);
