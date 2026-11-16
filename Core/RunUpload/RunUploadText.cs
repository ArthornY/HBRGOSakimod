using System;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.RunUpload;

// Player-facing text of run uploading. The explanation is shown on first
// launch and again when the settings toggle goes from off to on; it promises
// exactly what RunRecorder records. Raise RunUploadConstants.ConsentVersion
// whenever it has to promise something new, so every player is asked again.
internal static class RunUploadText
{
    private static bool Chinese
    {
        get
        {
            try
            {
                string language = LocManager.Instance?.Language;
                return string.IsNullOrEmpty(language) || language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    internal static string ConsentTitle => Chinese ? "允许上传对局数据到 hbrgo.com 吗？" : "Upload your run data to hbrgo.com?";

    internal static string ConsentBody() => Chinese ? ChineseConsent() : EnglishConsent();

    internal static string Allow => Chinese ? "允许" : "Allow";
    internal static string Deny => Chinese ? "拒绝" : "Deny";
    internal static string Ok => Chinese ? "知道了" : "OK";
    internal static string Cancel => Chinese ? "取消" : "Cancel";

    internal static string InstallIdTitle => Chinese ? "匿名 ID" : "Anonymous ID";

    internal static string NoInstallId => Chinese
        ? "还没有匿名 ID。允许上传对局数据后才会生成。"
        : "There is no anonymous ID yet. It is created when you allow uploading run data.";

    internal static string Copied(string id) => Chinese
        ? $"已复制到剪贴板：\n\n{id}\n\n申请删除已上传的数据时，把这个 ID 发给司令部。联系方式见 {RunUploadConstants.PrivacyPageUrl}。"
        : $"Copied to the clipboard:\n\n{id}\n\nTo have your uploaded data deleted, send this ID to the HQ team. Contact details: {RunUploadConstants.PrivacyPageUrl}.";

    internal static string RegenerateTitle => Chinese ? "重新生成匿名 ID" : "New anonymous ID";
    internal static string RegenerateAction => Chinese ? "重新生成" : "Create new ID";

    internal static string RegenerateConfirm(string current) => Chinese
        ? "重新生成后，以后上传的对局会使用新的匿名 ID，和之前上传的记录就对不上了。本机还没发出的记录仍用现在的 ID。\n\n" +
          $"现在的 ID：\n{current}\n\n申请删除之前上传的数据时还要用到它。确认后，它会被复制到剪贴板。"
        : "Runs uploaded after this carry a new anonymous ID and cannot be linked to the ones uploaded before. " +
          "Records on this computer that have not been sent keep the current ID.\n\n" +
          $"Current ID:\n{current}\n\nYou need it to have the earlier uploads deleted. It is copied to the clipboard when you confirm.";

    internal static string Regenerated(string old, string next) => Chinese
        ? $"新的匿名 ID：\n{next}\n\n旧的 ID 已复制到剪贴板：\n{old}"
        : $"New anonymous ID:\n{next}\n\nThe old ID has been copied to the clipboard:\n{old}";

    internal static string ClearTitle => Chinese ? "清空本地未上传数据" : "Clear local unsent data";
    internal static string ClearAction => Chinese ? "清空" : "Clear";

    internal static string ClearConfirm(int queued, bool recording)
    {
        if (Chinese)
            return $"将删除本机上还没发出的对局记录（{queued} 局）" +
                   (recording ? "，以及这一局目前记下的内容（这一局之后也不再记录）" : "") +
                   "。已经上传到网站的数据不受影响；匿名 ID 会保留。\n\n此操作不能撤销。";
        return $"This deletes the run records on this computer that have not been sent ({queued})" +
               (recording ? ", and what has been recorded of the current run (the rest of it will not be recorded)" : "") +
               ". Data already on the site is not affected, and the anonymous ID is kept.\n\nThis cannot be undone.";
    }

    internal static string Cleared(int removed) => Chinese
        ? $"已清空。删除了 {removed} 局还没发出的记录。"
        : $"Done. {removed} unsent run record(s) deleted.";

    private static string ChineseConsent() =>
        "HBRGOSakiMod 想在每局结束时，把这一局的记录上传到 hbrgo.com，用来调整平衡。是否允许由你决定，之后随时可以在模组设置里更改。\n\n" +
        "完全匿名：\n" +
        "· 不收集你的 Steam ID、Steam 名字、电脑上的路径和其他模组的列表。\n" +
        "· IP 不会被保存，也不会显示给任何人；网站只在内存里用它限制请求频率。\n" +
        "· 网站托管在 Cloudflare 上。和访问任何网站一样，Cloudflare 在网络层面传输数据时会短暂处理连接的 IP 地址，这一层不归 hbrgo.com 控制。\n" +
        "· 用一个随机的匿名 ID 区分不同的电脑，它和 Steam 账号无关。设置里可以复制它，也可以随时重新生成；重新生成后，新旧记录就对不上了。\n\n" +
        "会收集：\n" +
        "· 整局信息：角色、进阶、种子、胜负、开始和结束时间、模组和游戏版本\n" +
        "· 每一幕的地图，以及你走的路线\n" +
        "· 每层的房间：遭遇和事件、事件里选的选项、卡牌奖励给了哪几张和拿了哪张、商店的购买、休息处的选择、得到的遗物和药水、升级和删卡\n" +
        "· 每场战斗逐回合：抽到的牌、打出的牌、掷骰结果、药水使用顺序、敌人意图、双方受到的伤害、HP 和 DP\n" +
        "· HBR 数值：战斗等级、六维、六感、装备\n" +
        "· 是否开着其他会影响玩法的模组：只记有没有，不记名字\n" +
        "联机时只记录你自己的选择和你这边看到的战斗，不带队友的个人信息；每个玩家各自决定是否上传。\n\n" +
        "不会上传的对局（本机也不保留）：开着修改器类模组（Loadout、Free Loadout、STS2 God Mod）的对局，用控制台改动过对局（例如加卡、加遗物、改金币和血量）的对局，以及在前 5 层内死亡或放弃的对局。只查看信息、调整日志这类不改动对局的控制台命令不算。\n\n" +
        $"发送到：{RunUploadConstants.EndpointHost}（数据保存在 hbrgo.com）\n" +
        "谁能看到：只有 hbrgo.com 的司令部账号。\n" +
        $"如何删除：在设置里复制匿名 ID，按 {RunUploadConstants.PrivacyPageUrl} 上的联系方式发给司令部，司令部会删除这个 ID 下的全部数据。\n\n" +
        "选择「拒绝」或直接关闭本窗口，就什么都不记录，也不发送任何数据。选择「允许」后，从下一局开始记录；打开时如果有一局正在进行（包括存档里还没打完的局），这一局不上传。之后随时可以在设置里关闭；关闭后立即停止记录和上传。";

    private static string EnglishConsent() =>
        "HBRGOSakiMod would like to upload a record of each run to hbrgo.com when the run ends, to help with game balance. It is your choice, and you can change it any time in the mod settings.\n\n" +
        "Fully anonymous:\n" +
        "· Your Steam ID, your Steam name, paths on your computer and the list of your other mods are never collected.\n" +
        "· Your IP address is never stored or shown to anyone; the site only uses it in memory to limit request rates.\n" +
        "· The site is hosted on Cloudflare. As with visiting any website, Cloudflare briefly processes the connection's IP address while carrying the data over its network; that layer is outside hbrgo.com's control.\n" +
        "· A random anonymous ID, not linked to your Steam account, tells computers apart. You can copy it in the settings or create a new one at any time; after that, new and old records cannot be linked.\n\n" +
        "What is collected:\n" +
        "· The run: character, ascension, seed, result, start and end time, mod and game version\n" +
        "· The map of every act and the path you took\n" +
        "· Every floor: encounters and events, the event options you chose, which cards were offered and which you took, shop purchases, rest site choices, relics and potions gained, upgrades and removals\n" +
        "· Every battle, turn by turn: cards drawn and played, roll results, the order of potions used, enemy intents, damage on both sides, HP and DP\n" +
        "· HBR values: battle level, the six stats, the six senses, equipment\n" +
        "· Whether another mod that changes gameplay is on: yes or no only, never its name\n" +
        "In co-op only your own choices and the battles as your screen shows them are recorded, without your teammates' personal information; every player decides for themselves.\n\n" +
        "Runs that are never uploaded (and not kept on this computer): runs with a modifier mod on (Loadout, Free Loadout, STS2 God Mod), runs changed through the console (such as adding cards or relics, or changing gold or HP), and runs that end in death or abandonment within the first 5 floors. Console commands that only show information or adjust logging do not count.\n\n" +
        $"Sent to: {RunUploadConstants.EndpointHost} (the data is kept on hbrgo.com)\n" +
        "Who can see it: only HQ accounts on hbrgo.com.\n" +
        $"Deletion: copy your anonymous ID in the settings and send it to the HQ team using the contact details on {RunUploadConstants.PrivacyPageUrl}. They delete all data under that ID.\n\n" +
        "\"Deny\", or closing this window, records nothing and sends nothing. After \"Allow\", recording starts with the next run: if a run is in progress when you turn this on (including a saved run not yet finished), that run is not uploaded. You can turn uploading off in the settings at any time; recording and uploading stop at once.";
}
