# XIV VRC Status

XIV VRC Status is a Dalamud plugin that sends a configurable Final Fantasy XIV
status message to the VRChat OSC chatbox.

XIV VRC Status 是一个卫月 / Dalamud 插件，用来把当前最终幻想14角色状态发送到
VRChat 的 OSC 文字框。

## Features

- Sends directly to VRChat's `/chatbox/input` OSC address.
- Supports English and Chinese display templates.
- Separates settings UI language from the language sent to VRChat OSC.
- Supports English, Simplified Chinese, Traditional Chinese, and Japanese OSC output.
- Configurable refresh interval, with a 1 second minimum.
- Shows character, job, level, data center, world, area, duty, combat state, and instance.
- Supports duty progress, boss or target HP, recently clicked skill, and live GCD utilization.
- Provides clickable placeholder buttons in the config window.
- Provides preview, manual send, and clear buttons.
- Enforces VRChat's 144-character and 9-line chatbox limits.

## Installation

### 1. Enable VRChat OSC

In VRChat, open the Action Menu and enable OSC.

VRChat listens on `127.0.0.1:9000` by default. The plugin uses that target by
default, so most users do not need to change the OSC IP or port.

### 2. Install the Plugin in Dalamud

For local development or manual testing:

1. Download `latest.zip` from
   [GitHub Releases](https://github.com/XiaoLan9999/XIVVRCStatus/releases), or
   build the plugin from source.
2. Extract the zip if you downloaded a release package.
3. In FFXIV, open Dalamud settings with `/xlsettings`.
4. Go to `Experimental` > `Dev Plugin Locations`.
5. Add the full path to `XIVVRCStatus.dll`.
6. Open `/xlplugins`, find `XIV VRC Status`, and enable it.
7. Run `/xivvrc` to open the config window.

If you build from source, the DLL is written to:

```text
XIVVRCStatus\bin\x64\Release\XIVVRCStatus.dll
```

The release package zip is written to:

```text
XIVVRCStatus\bin\x64\Release\XIVVRCStatus\latest.zip
```

### 3. Configure the Chatbox Text

Run:

```text
/xivvrc
```

Then:

1. Choose English or Chinese.
2. Choose the VRChat OSC display language: English, 简体中文, 繁體中文, or 日本語.
3. Edit the shared status template or click placeholder buttons to insert tokens.
4. Enable automatic status updates.
5. Use `Send now` to test.

The settings UI language and the VRChat OSC display language are separate. You
can keep one template and switch only the text sent to VRChat.

## Commands

```text
/xivvrc
/xivvrc send
/xivvrc clear
/xivvrc on
/xivvrc off
```

## Example Templates

English:

```text
{game} | {job} Lv{level} | {server_status} | {activity}
```

Chinese:

```text
{game}
{job} Lv{level} | {server_status}
{activity}
{duty_progress} {boss_status}
```

Combat template:

```text
{game}
{activity}
{job} Lv{level} | {server_status}
{duty_progress} {boss_status} | {gcd}
{技能}
{name}
```

## Template Placeholders

| Placeholder | Value |
| --- | --- |
| `{game}` | Game name. In Chinese mode this is `最终幻想14`. |
| `{name}` | Character name. |
| `{job}` | Current job. Output is localized by the VRChat OSC display language, for example `BLU` becomes `青魔` in Simplified Chinese and `青魔道士` in Japanese. |
| `{level}` | Current job level. |
| `{effective_level}` | Level after level sync. |
| `{server}` | Current world/server alias. |
| `{home_server}` | Home world/server alias. |
| `{server_status}` | Current data center and world, for example `陆行鸟 晨曦王座`. |
| `{world}` | Current world. |
| `{home_world}` | Home world. |
| `{location}` | Current area. |
| `{duty}` | Current duty, when available. |
| `{duty_progress}` | Duty timer/status, such as in duty, cutscene, or duty complete. |
| `{duty_elapsed}` | Time since the plugin detected duty start. |
| `{activity}` | Current duty or area. |
| `{combat}` | Combat state. |
| `{boss}` | Auto-detected boss or current target name. |
| `{boss_hp}` | Auto-detected boss or current target HP percent. |
| `{boss_status}` | Combined boss/target name and HP percent, or phase text. |
| `{instance}` | Area instance number. |
| `{skill}` / `{技能}` | Last clicked action name, cleared after 5 seconds. |
| `{gcd}` | Live GCD utilization with label. |
| `{gcd_uptime}` / `{GCD利用率}` | Live GCD utilization percent. |

## Notes About Skill and GCD Display

The skill display hooks FFXIV's action usage path, so instant skills should be
detected as well as casted skills. If no new action is used for 5 seconds, the
skill text is cleared.

Generated labels such as game name, job name, combat state, duty progress, boss
label, target label, and GCD label follow the VRChat OSC display language. Game
data such as area, duty, boss, and action names use the requested English or
Japanese data sheets when available. Simplified Chinese uses the current client
text, and Traditional Chinese applies a lightweight script conversion to current
client text.

GCD utilization is a lightweight live estimate that only runs during active
combat windows. The timer starts from the first detected GCD in combat, pauses
during cutscenes or when no usable enemy is available, and subtracts downtime
only while an enemy can reasonably be attacked. It also treats normal
action-queue presses as landing when the GCD becomes available, so queueing
slightly early should not lower the result. It is intended for VRChat status
display rather than log-grade combat analysis.

## Build

Requirements:

- FINAL FANTASY XIV, XIVLauncher, and Dalamud installed.
- .NET 10 SDK.
- `DALAMUD_HOME` set if Dalamud is not installed in its default location.

Build:

```powershell
dotnet build .\XIVVRCStatus.sln -c Release
```

## Privacy

Displaying character names, worlds, locations, duties, or combat status can
reveal information to nearby VRChat users. Only add those placeholders when you
are comfortable sharing them.

## Disclaimer

Dalamud and other third-party FFXIV tools are unofficial and are not affiliated
with Square Enix. VRChat OSC must be enabled for messages to appear.

If you plan to submit this plugin to the official Dalamud repository, review the
[Dalamud AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy/).
This plugin was created with AI assistance and should be personally reviewed,
tested, understood, and disclosed before submission.

## Source Credits

This plugin uses the public [goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin)
project as a Dalamud plugin scaffold/reference.

It is built against the [Dalamud](https://github.com/goatcorp/Dalamud) plugin
API, the [Dalamud.NET.Sdk](https://github.com/goatcorp/Dalamud.NET.Sdk), and
[FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs).
