namespace XIVVRCStatus.Services;

public sealed record StatusSnapshot(
    string GameName,
    string CharacterName,
    string Job,
    string Level,
    string EffectiveLevel,
    string CurrentWorld,
    string HomeWorld,
    string ServerStatus,
    string Location,
    string Duty,
    string DutyProgress,
    string DutyElapsed,
    string Activity,
    string Combat,
    string Boss,
    string BossHp,
    string BossStatus,
    string Instance,
    string Skill,
    string GcdUptime,
    string GcdStatus);
