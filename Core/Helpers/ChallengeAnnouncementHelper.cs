using System;
using System.Collections.Generic;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ChallengeAnnouncementHelper
{
    internal const string Title = "HBRGO高分挑战第0期";
    internal const string Intro = "以防有人没看见公告，在这里也提醒一下\n来个好玩的";

    internal static IReadOnlyList<string> Rules { get; } = Array.AsReadOnly(new[]
    {
        "1：无修改器",
        "2：无筛种器",
        "3：游戏进行时，仅能在配置菜单中调高怪物的数值，不允许调低",
        "4：仅允许添加不影响游戏的mod",
        "5：奖金为：200/100/50",
        "6：名次排序为单牌造成伤害最高优先",
        "7：截至日期为2026/10/7，23：59分",
        "8：请全程录频（如果电脑无法录屏，允许使用手机）",
        "9：投稿邮箱：arthornforreal@gmail.com，标题请写“HBRGO高分挑战第0期”（不然我可能看不到）",
        "10：如无恶性bug，必须见到建筑师",
        "11：视频也可以上传至Bilibili，标题需包含HBRGO高分挑战第0期",
        "12：不允许调低敌人造成的伤害",
        "13：开始游戏前，展示启用的mod"
    });
}
