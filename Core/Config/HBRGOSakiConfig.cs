using System;
using System.Globalization;
using BaseLib.Config;
using HBRGOSakiMod.Core.RunUpload;

namespace HBRGOSakiMod.Core.Config;

public sealed class HBRGOSakiConfig : SimpleModConfig
{
    public const int DefaultEnemyHighDimensionalShellPercent = 20;
    public const int MaxEnemyHighDimensionalShellPercent = 1000;
    public const int DefaultEnemyDpGrowthPerActPercent = 0;
    public const int DefaultEnemyMaxHpPercent = 100;
    public const int DefaultEnemyMaxHpGrowthPerActPercent = 0;
    public const int DefaultEnemyDamagePercent = 100;
    public const int DefaultEnemyDamageGrowthPerActPercent = 0;
    public const int MaxEnemyScalingPercent = 1000;

    private static HBRGOSakiConfig _instance;
    private static bool _modUI = true;
    private static int _enemyHighDimensionalShellPercent =
        DefaultEnemyHighDimensionalShellPercent;
    private static int _enemyDpGrowthPerActPercent =
        DefaultEnemyDpGrowthPerActPercent;
    private static int _enemyMaxHpPercent = DefaultEnemyMaxHpPercent;
    private static int _enemyMaxHpGrowthPerActPercent =
        DefaultEnemyMaxHpGrowthPerActPercent;
    private static int _enemyDamagePercent = DefaultEnemyDamagePercent;
    private static int _enemyDamageGrowthPerActPercent =
        DefaultEnemyDamageGrowthPerActPercent;
    private static int _hpDpThicknessPercent = 150;
    private static int _thirtyOneXHiddenEffectPermille = 1;

    public HBRGOSakiConfig()
    {
        _instance = this;
    }

    [ConfigHideInUI]
    [ConfigIgnoreRestoreDefaults]
    public static bool SeenDamageChallengeAnnouncement { get; set; } = false;

    [ConfigHideInUI]
    public static int ThirtyOneXHiddenEffectPermille
    {
        get => _thirtyOneXHiddenEffectPermille;
        set => _thirtyOneXHiddenEffectPermille = Math.Clamp(value, 0, 1000);
    }

    internal static bool CanSaveAnnouncementAcknowledgment => _instance != null;

    internal static void AcknowledgeDamageChallengeAnnouncement()
    {
        if (_instance == null || SeenDamageChallengeAnnouncement) return;
        SeenDamageChallengeAnnouncement = true;
        _instance.Save();
    }

    [ConfigHideInUI]
    public static int HpDpThicknessPercent
    {
        get => _hpDpThicknessPercent;
        set => _hpDpThicknessPercent = Math.Clamp(value, 50, 400);
    }

    internal static void SaveGaugeSizing() => _instance?.SaveDebounced(250);

    // Both BaseLib and Ritsu's mirrored settings bind this same persisted
    // property. Their normal save callbacks own disk writes, including reloads.
    [ConfigHoverTip]
    public static bool ModUI
    {
        get => _modUI;
        set
        {
            if (_modUI == value) return;
            _modUI = value;
            NotifyConfigValueChanged();
        }
    }

    [ConfigHoverTip]
    public static bool UseBigDogChewCardFace { get; set; } = false;

    [ConfigHoverTip]
    public static bool ThirtyOneACutInPresentation { get; set; } = true;

    [ConfigHoverTip]
    public static bool ThirtyGProposalPresentation { get; set; } = true;

    [ConfigHoverTip]
    public static HbrMemoryGardenStoryPlayback MemoryGardenUpgradeStoryPlayback { get; set; } =
        HbrMemoryGardenStoryPlayback.All;

    // This is a temporary new-run policy, not a live-run mode switch. Keep a
    // writable config property so old/off files and mirrored settings can load
    // normally, but they cannot opt a newly started run out of HBR numerics.
    // Existing runs continue to read the mode saved in their soldier manual.
    [ConfigHoverTip]
    public static bool HbrNumerics
    {
        get => true;
        set { }
    }

    // Magic Sword Lucky Seven's finisher show. Presentation only: the setting never changes the
    // damage, and a teammate's finisher is never shown full screen on this machine.
    [ConfigHoverTip]
    public static HbrFinisherPresentation FinisherPresentation { get; set; } =
        HbrFinisherPresentation.Fullscreen;

    [ConfigHoverTip]
    public static bool ForceModEliteBossEncounters { get; set; } = false;

    [ConfigHoverTip]
    public static bool ForceModBossEncounters { get; set; } = false;

    [ConfigSlider(
        0,
        MaxEnemyHighDimensionalShellPercent,
        1,
        Format = "{0}%")]
    [ConfigHoverTip]
    public static int EnemyHighDimensionalShellPercent
    {
        get => _enemyHighDimensionalShellPercent;
        set
        {
            int normalizedValue = Math.Clamp(
                value,
                0,
                MaxEnemyHighDimensionalShellPercent);
            if (_enemyHighDimensionalShellPercent == normalizedValue)
            {
                return;
            }

            _enemyHighDimensionalShellPercent = normalizedValue;
            NotifyConfigValueChanged();
        }
    }

    [ConfigHideInUI]
    [ConfigTextInput(@"[0-9]+", MaxLength = 4)]
    [ConfigIgnoreRestoreDefaults]
    [ConfigHoverTip]
    public static string EnemyHighDimensionalShellPercentInput
    {
        get => EnemyHighDimensionalShellPercent.ToString(
            CultureInfo.InvariantCulture);
        set
        {
            if (!int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int percentage))
            {
                return;
            }

            EnemyHighDimensionalShellPercent = percentage;
        }
    }

    [ConfigSlider(0, MaxEnemyScalingPercent, 1, Format = "{0}%")]
    [ConfigHoverTip]
    public static int EnemyDpGrowthPerActPercent
    {
        get => _enemyDpGrowthPerActPercent;
        set => SetEnemyScalingPercent(ref _enemyDpGrowthPerActPercent, value);
    }

    [ConfigHideInUI]
    [ConfigTextInput(@"[0-9]+", MaxLength = 4)]
    [ConfigIgnoreRestoreDefaults]
    [ConfigHoverTip]
    public static string EnemyDpGrowthPerActPercentInput
    {
        get => EnemyDpGrowthPerActPercent.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (TryParsePercentage(value, out int percentage))
            {
                EnemyDpGrowthPerActPercent = percentage;
            }
        }
    }

    [ConfigSlider(0, MaxEnemyScalingPercent, 1, Format = "{0}%")]
    [ConfigHoverTip]
    public static int EnemyMaxHpPercent
    {
        get => _enemyMaxHpPercent;
        set => SetEnemyScalingPercent(ref _enemyMaxHpPercent, value);
    }

    [ConfigHideInUI]
    [ConfigTextInput(@"[0-9]+", MaxLength = 4)]
    [ConfigIgnoreRestoreDefaults]
    [ConfigHoverTip]
    public static string EnemyMaxHpPercentInput
    {
        get => EnemyMaxHpPercent.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (TryParsePercentage(value, out int percentage))
            {
                EnemyMaxHpPercent = percentage;
            }
        }
    }

    [ConfigSlider(0, MaxEnemyScalingPercent, 1, Format = "{0}%")]
    [ConfigHoverTip]
    public static int EnemyMaxHpGrowthPerActPercent
    {
        get => _enemyMaxHpGrowthPerActPercent;
        set => SetEnemyScalingPercent(ref _enemyMaxHpGrowthPerActPercent, value);
    }

    [ConfigHideInUI]
    [ConfigTextInput(@"[0-9]+", MaxLength = 4)]
    [ConfigIgnoreRestoreDefaults]
    [ConfigHoverTip]
    public static string EnemyMaxHpGrowthPerActPercentInput
    {
        get => EnemyMaxHpGrowthPerActPercent.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (TryParsePercentage(value, out int percentage))
            {
                EnemyMaxHpGrowthPerActPercent = percentage;
            }
        }
    }

    [ConfigSlider(0, MaxEnemyScalingPercent, 1, Format = "{0}%")]
    [ConfigHoverTip]
    public static int EnemyDamagePercent
    {
        get => _enemyDamagePercent;
        set => SetEnemyScalingPercent(ref _enemyDamagePercent, value);
    }

    [ConfigHideInUI]
    [ConfigTextInput(@"[0-9]+", MaxLength = 4)]
    [ConfigIgnoreRestoreDefaults]
    [ConfigHoverTip]
    public static string EnemyDamagePercentInput
    {
        get => EnemyDamagePercent.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (TryParsePercentage(value, out int percentage))
            {
                EnemyDamagePercent = percentage;
            }
        }
    }

    [ConfigSlider(0, MaxEnemyScalingPercent, 1, Format = "{0}%")]
    [ConfigHoverTip]
    public static int EnemyDamageGrowthPerActPercent
    {
        get => _enemyDamageGrowthPerActPercent;
        set => SetEnemyScalingPercent(ref _enemyDamageGrowthPerActPercent, value);
    }

    [ConfigHideInUI]
    [ConfigTextInput(@"[0-9]+", MaxLength = 4)]
    [ConfigIgnoreRestoreDefaults]
    [ConfigHoverTip]
    public static string EnemyDamageGrowthPerActPercentInput
    {
        get => EnemyDamageGrowthPerActPercent.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (TryParsePercentage(value, out int percentage))
            {
                EnemyDamageGrowthPerActPercent = percentage;
            }
        }
    }

    // Run data upload (Core/RunUpload). The switch reads the consent file in
    // user://HBRGOSakiMod/run-upload, not this config. The first-launch prompt
    // sets it; turning it on here shows the same explanation first, and
    // Restore Defaults never touches it.
    [ConfigHoverTip]
    [ConfigIgnoreRestoreDefaults]
    public static bool UploadRunData
    {
        get => RunUploadService.IsEnabled;
        set => RunUploadService.RequestEnabled(value);
    }

    // Shown for copying; edits are ignored and the field snaps back.
    [ConfigTextInput("[0-9a-f-]+", MaxLength = 36)]
    [ConfigHoverTip]
    [ConfigIgnoreRestoreDefaults]
    public static string RunUploadInstallId
    {
        get => RunUploadService.InstallId;
        set
        {
            if (!string.Equals(value ?? string.Empty, RunUploadService.InstallId, StringComparison.Ordinal))
                Godot.Callable.From(RefreshRunUploadSettings).CallDeferred();
        }
    }

    [ConfigButton("RunUploadCopyButton")]
    [ConfigHoverTip]
    public static void CopyRunUploadInstallId() => RunUploadService.CopyInstallId();

    [ConfigButton("RunUploadRegenerateButton")]
    [ConfigHoverTip]
    public static void RegenerateRunUploadInstallId() => RunUploadService.ConfirmRegenerateInstallId();

    [ConfigButton("RunUploadClearButton")]
    [ConfigHoverTip]
    public static void ClearRunUploadLocalData() => RunUploadService.ConfirmClearLocalData();

    internal static void RefreshRunUploadSettings() => NotifyConfigValueChanged();

    internal static void SaveRunUploadSettings()
    {
        _instance?.Save();
        NotifyConfigValueChanged();
    }

    private static bool TryParsePercentage(string value, out int percentage)
    {
        return int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out percentage);
    }

    private static void SetEnemyScalingPercent(ref int storage, int value)
    {
        int normalizedValue = Math.Clamp(value, 0, MaxEnemyScalingPercent);
        if (storage == normalizedValue)
        {
            return;
        }

        storage = normalizedValue;
        NotifyConfigValueChanged();
    }

    private static void NotifyConfigValueChanged()
    {
        if (_instance == null)
        {
            return;
        }

        _instance.ConfigReloaded();
    }
}
