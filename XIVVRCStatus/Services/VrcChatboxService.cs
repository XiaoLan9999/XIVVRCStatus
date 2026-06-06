using System;
using System.Net;
using System.Net.Sockets;

namespace XIVVRCStatus.Services;

public sealed class VrcChatboxService
{
    private readonly Configuration configuration;
    private readonly StatusProvider statusProvider;
    private DateTime nextSendUtc = DateTime.UtcNow.AddSeconds(1);

    public VrcChatboxService(Configuration configuration, ActionTrackerService actionTracker)
    {
        this.configuration = configuration;
        statusProvider = new StatusProvider(configuration, actionTracker);
    }

    public DateTime? LastSentUtc { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public string LastError { get; private set; } = string.Empty;

    public void Update()
    {
        if (!configuration.Enabled)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now < nextSendUtc)
        {
            return;
        }

        nextSendUtc = now.AddSeconds(Math.Clamp(configuration.RefreshIntervalSeconds, 1, 300));
        SendStatus();
    }

    public void RequestImmediateSend()
    {
        nextSendUtc = DateTime.MinValue;
    }

    public string GetPreview()
    {
        return TryBuildStatus(out var message, out var error) ? message : error;
    }

    public bool SendStatus()
    {
        if (!TryBuildStatus(out var message, out var error))
        {
            LastError = error;
            return false;
        }

        return SendText(message);
    }

    public bool ClearChatbox()
    {
        return SendText(string.Empty, true, false);
    }

    private bool TryBuildStatus(out string message, out string error)
    {
        if (!statusProvider.TryCapture(out var snapshot, out error))
        {
            message = string.Empty;
            return false;
        }

        message = TemplateRenderer.Render(configuration.ActiveStatusTemplate, snapshot);
        if (string.IsNullOrWhiteSpace(message))
        {
            error = configuration.DisplayLanguage == DisplayLanguage.Chinese
                ? "渲染后的状态文本为空。"
                : "The rendered status message is empty.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool SendText(string text)
    {
        return SendText(text, configuration.SendImmediately, configuration.PlayNotificationSound);
    }

    private bool SendText(string text, bool sendImmediately, bool playNotificationSound)
    {
        if (!IPAddress.TryParse(configuration.TargetIp, out var ipAddress))
        {
            LastError = IsChinese ? "目标 IP 不是有效的 IP 地址。" : "Target IP is not a valid IP address.";
            return false;
        }

        if (configuration.TargetPort is < 1 or > 65535)
        {
            LastError = IsChinese ? "目标端口必须在 1 到 65535 之间。" : "Target port must be between 1 and 65535.";
            return false;
        }

        try
        {
            var packet = OscMessageEncoder.BuildChatboxInput(
                ChatboxText.Sanitize(text),
                sendImmediately,
                playNotificationSound);
            var endpoint = new IPEndPoint(ipAddress, configuration.TargetPort);

            using var client = new UdpClient(endpoint.AddressFamily);
            client.Send(packet, packet.Length, endpoint);

            LastSentUtc = DateTime.UtcNow;
            LastMessage = text;
            LastError = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            LastError = IsChinese ? $"OSC 发送失败：{exception.Message}" : $"OSC send failed: {exception.Message}";
            Plugin.Log.Warning(exception, "Failed to send VRChat OSC chatbox message.");
            return false;
        }
    }

    private bool IsChinese => configuration.DisplayLanguage == DisplayLanguage.Chinese;
}
