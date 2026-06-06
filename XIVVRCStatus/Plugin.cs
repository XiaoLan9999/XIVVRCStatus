using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using XIVVRCStatus.Services;
using XIVVRCStatus.Windows;

namespace XIVVRCStatus;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/xivvrc";

    public Configuration Configuration { get; init; }
    public VrcChatboxService ChatboxService { get; init; }
    public ActionTrackerService ActionTracker { get; init; }

    public readonly WindowSystem WindowSystem = new("XIVVRCStatus");
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Normalize();
        ActionTracker = new ActionTrackerService(Configuration);
        ChatboxService = new VrcChatboxService(Configuration, ActionTracker);
        ActionTracker.StatusChanged += ChatboxService.RequestImmediateSend;
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings, or use /xivvrc send|clear|on|off",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;

        Log.Information("XIV VRC Status loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        Framework.Update -= OnFrameworkUpdate;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        ActionTracker.StatusChanged -= ChatboxService.RequestImmediateSend;
        ActionTracker.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "send":
                PrintResult("send", ChatboxService.SendStatus());
                break;
            case "clear":
                PrintResult("clear", ChatboxService.ClearChatbox());
                break;
            case "on":
                Configuration.Enabled = true;
                Configuration.Save();
                ChatboxService.RequestImmediateSend();
                ChatGui.Print(IsChinese
                    ? "[XIV VRC Status] 已启用自动状态刷新。"
                    : "[XIV VRC Status] Automatic status updates enabled.");
                break;
            case "off":
                Configuration.Enabled = false;
                Configuration.Save();
                ChatGui.Print(IsChinese
                    ? "[XIV VRC Status] 已关闭自动状态刷新。"
                    : "[XIV VRC Status] Automatic status updates disabled.");
                break;
            case "help":
                ChatGui.Print(IsChinese
                    ? "[XIV VRC Status] /xivvrc send|clear|on|off"
                    : "[XIV VRC Status] /xivvrc send|clear|on|off");
                break;
            default:
                MainWindow.Toggle();
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        ActionTracker.Update();
        ChatboxService.Update();
    }

    private void PrintResult(string action, bool success)
    {
        var actionName = IsChinese
            ? action switch
            {
                "send" => "发送",
                "clear" => "清空",
                _ => action,
            }
            : action;
        var result = success
            ? (IsChinese ? $"{actionName}成功。" : $"{actionName} succeeded.")
            : (IsChinese ? $"{actionName}失败：{ChatboxService.LastError}" : $"{actionName} failed: {ChatboxService.LastError}");
        ChatGui.Print($"[XIV VRC Status] {result}");
    }

    private bool IsChinese => Configuration.DisplayLanguage == DisplayLanguage.Chinese;

    public void ToggleMainUi() => MainWindow.Toggle();
}
