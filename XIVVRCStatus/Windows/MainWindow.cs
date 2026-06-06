using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using XIVVRCStatus.Services;

namespace XIVVRCStatus.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const string EnglishDefaultTemplate = "{game} | {job} Lv{level} | {server_status} | {activity}";
    private const string ChineseDefaultTemplate = "{game}\n{job} Lv{level} | {server_status}\n{activity}\n{duty_progress} {boss_status}";
    private const string ChineseSingleLineTemplate = "{game} | {job} Lv{level} | {server_status} | {activity}";
    private const string ChineseBossTemplate = "{game}\n{activity}\n{job} Lv{level} | {server_status}\n{duty_progress} {boss_status} | {gcd}\n{技能}";

    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("XIV VRC Status###XIVVRCStatusMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(540, 560),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var configuration = plugin.Configuration;
        var service = plugin.ChatboxService;
        var isChinese = configuration.DisplayLanguage == DisplayLanguage.Chinese;

        ImGui.TextWrapped(isChinese
            ? "将当前最终幻想14状态发送到 VRChat OSC 文字框。请先在 VRChat 中启用 OSC。"
            : "Sends your current FFXIV status to the VRChat OSC chatbox. VRChat OSC must be enabled.");
        ImGui.Spacing();

        ImGui.TextUnformatted("Settings UI language / 设置界面语言");
        if (ImGui.RadioButton("English", !isChinese))
        {
            configuration.DisplayLanguage = DisplayLanguage.English;
            configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("中文", isChinese))
        {
            configuration.DisplayLanguage = DisplayLanguage.Chinese;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(isChinese ? "VRC 游戏内 OSC 显示语言" : "VRChat OSC display language");
        DrawOscLanguageRadio(configuration, service, OscDisplayLanguage.English, "English");
        ImGui.SameLine();
        DrawOscLanguageRadio(configuration, service, OscDisplayLanguage.SimplifiedChinese, "简体中文");
        ImGui.SameLine();
        DrawOscLanguageRadio(configuration, service, OscDisplayLanguage.TraditionalChinese, "繁體中文");
        ImGui.SameLine();
        DrawOscLanguageRadio(configuration, service, OscDisplayLanguage.Japanese, "日本語");
        ImGui.TextDisabled(isChinese
            ? "这个选项只影响发到 VRChat 的 OSC 内容，不改变本设置窗口语言。"
            : "This only changes the text sent to VRChat, not this settings window.");

        ImGui.Spacing();

        var enabled = configuration.Enabled;
        if (ImGui.Checkbox(isChinese ? "启用自动状态刷新" : "Enable automatic status updates", ref enabled))
        {
            configuration.Enabled = enabled;
            configuration.Save();
            if (enabled)
            {
                service.RequestImmediateSend();
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted(isChinese ? "状态模板（各 OSC 语言共用）" : "Status template (shared by OSC languages)");

        var template = configuration.ActiveStatusTemplate;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##StatusTemplate", ref template, 1024, new Vector2(-1, 90)))
        {
            configuration.ActiveStatusTemplate = template;
            configuration.Save();
            service.RequestImmediateSend();
        }

        if (ImGui.Button(isChinese ? "套用默认多行模板" : "Use default template"))
        {
            configuration.ActiveStatusTemplate = isChinese ? ChineseDefaultTemplate : EnglishDefaultTemplate;
            configuration.Save();
            service.RequestImmediateSend();
        }

        if (isChinese)
        {
            ImGui.SameLine();
            if (ImGui.Button("套用单行模板"))
            {
                configuration.ActiveStatusTemplate = ChineseSingleLineTemplate;
                configuration.Save();
                service.RequestImmediateSend();
            }

            ImGui.SameLine();
            if (ImGui.Button("套用副本首领模板"))
            {
                configuration.ActiveStatusTemplate = ChineseBossTemplate;
                configuration.Save();
                service.RequestImmediateSend();
            }
        }

        ImGui.TextDisabled(isChinese ? "可用占位符（点击按钮会追加到模板末尾）：" : "Available placeholders (click a button to append it):");
        if (ImGui.Button(isChinese ? "追加换行" : "Add newline"))
        {
            AppendTemplateText(configuration, service, "\n");
        }

        ImGui.SameLine();
        if (ImGui.Button(" | "))
        {
            AppendTemplateText(configuration, service, " | ");
        }

        ImGui.SameLine();
        if (ImGui.Button(" / "))
        {
            AppendTemplateText(configuration, service, " / ");
        }

        foreach (var token in TemplateRenderer.GetTokens(configuration.DisplayLanguage))
        {
            if (ImGui.Button($"{token.Name}##append-{token.Name}"))
            {
                AppendTemplateToken(configuration, service, token.Name);
            }

            ImGui.SameLine();
            ImGui.TextDisabled(token.Description);
        }

        ImGui.Separator();
        ImGui.TextUnformatted(isChinese ? "预览" : "Preview");
        var preview = service.GetPreview();
        ImGui.TextWrapped(preview);
        ImGui.TextDisabled($"{new StringInfo(preview).LengthInTextElements}/{ChatboxText.MaxCharacters} characters");

        if (ImGui.Button(isChinese ? "立即发送" : "Send now"))
        {
            service.SendStatus();
        }

        ImGui.SameLine();
        if (ImGui.Button(isChinese ? "清空 VRChat 文字框" : "Clear VRChat chatbox"))
        {
            service.ClearChatbox();
        }

        if (service.LastSentUtc is { } lastSent)
        {
            ImGui.TextDisabled(isChinese
                ? $"上次发送：{lastSent.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                : $"Last send: {lastSent.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        }

        if (!string.IsNullOrWhiteSpace(service.LastError))
        {
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.45f, 1f), service.LastError);
        }

        ImGui.Separator();
        ImGui.TextUnformatted(isChinese ? "OSC 目标" : "OSC target");

        var targetIp = configuration.TargetIp;
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputText(isChinese ? "目标 IP" : "Target IP", ref targetIp, 64))
        {
            configuration.TargetIp = targetIp;
            configuration.Save();
        }

        var targetPort = configuration.TargetPort;
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputInt(isChinese ? "目标端口" : "Target port", ref targetPort))
        {
            configuration.TargetPort = Math.Clamp(targetPort, 1, 65535);
            configuration.Save();
        }

        var refreshInterval = configuration.RefreshIntervalSeconds;
        ImGui.SetNextItemWidth(180);
        if (ImGui.SliderInt(isChinese ? "刷新间隔（秒）" : "Refresh interval (seconds)", ref refreshInterval, 1, 120))
        {
            configuration.RefreshIntervalSeconds = Math.Clamp(refreshInterval, 1, 300);
            configuration.Save();
            service.RequestImmediateSend();
        }

        ImGui.TextDisabled(isChinese
            ? "最低 1 秒。过快刷新可能被 VRChat 限流。"
            : "Minimum 1 second. Very fast updates may be rate-limited by VRChat.");

        var sendImmediately = configuration.SendImmediately;
        if (ImGui.Checkbox(
                isChinese ? "直接发送，不打开 VRChat 键盘" : "Send immediately instead of opening the VRChat keyboard",
                ref sendImmediately))
        {
            configuration.SendImmediately = sendImmediately;
            configuration.Save();
        }

        var playNotificationSound = configuration.PlayNotificationSound;
        if (ImGui.Checkbox(
                isChinese ? "播放 VRChat 文字框提示音" : "Play VRChat chatbox notification sound",
                ref playNotificationSound))
        {
            configuration.PlayNotificationSound = playNotificationSound;
            configuration.Save();
        }
    }

    private static void AppendTemplateToken(Configuration configuration, VrcChatboxService service, string token)
    {
        var template = configuration.ActiveStatusTemplate;
        var separator = string.IsNullOrEmpty(template) || template.EndsWith(' ') || template.EndsWith('\n') || template.EndsWith('|')
            ? string.Empty
            : " ";
        configuration.ActiveStatusTemplate = $"{template}{separator}{token}";
        configuration.Save();
        service.RequestImmediateSend();
    }

    private static void DrawOscLanguageRadio(
        Configuration configuration,
        VrcChatboxService service,
        OscDisplayLanguage language,
        string label)
    {
        if (ImGui.RadioButton($"{label}##OscDisplayLanguage-{language}", configuration.OscDisplayLanguage == language))
        {
            configuration.OscDisplayLanguage = language;
            configuration.Save();
            service.RequestImmediateSend();
        }
    }

    private static void AppendTemplateText(Configuration configuration, VrcChatboxService service, string text)
    {
        configuration.ActiveStatusTemplate += text;
        configuration.Save();
        service.RequestImmediateSend();
    }
}
