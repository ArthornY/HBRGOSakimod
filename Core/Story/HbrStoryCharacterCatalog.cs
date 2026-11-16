using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Story;

public sealed record HbrStoryCharacterEntry(string Id, string Name, string Squad);

/// <summary>Stable encounter IDs shared by story validation, run saves and the external editor.</summary>
public static class HbrStoryCharacterCatalog
{
    public static IReadOnlyList<HbrStoryCharacterEntry> Entries { get; } = Array.AsReadOnly(new[]
    {
        new HbrStoryCharacterEntry("ruka", "茅森月歌", "31A"),
        new HbrStoryCharacterEntry("yuki", "和泉由希", "31A"),
        new HbrStoryCharacterEntry("megumi", "逢川惠", "31A"),
        new HbrStoryCharacterEntry("tsukasa", "东城司", "31A"),
        new HbrStoryCharacterEntry("karen", "朝仓可怜", "31A"),
        new HbrStoryCharacterEntry("tama", "国见玉", "31A"),

        new HbrStoryCharacterEntry("erika", "苍井绘梨花", "31B"),
        new HbrStoryCharacterEntry("ichigo", "水濑莓", "31B"),
        new HbrStoryCharacterEntry("sumomo", "水濑李", "31B"),
        new HbrStoryCharacterEntry("seika", "樋口圣华", "31B"),
        new HbrStoryCharacterEntry("kozue", "柊木梢", "31B"),
        new HbrStoryCharacterEntry("byakko", "白虎", "31B"),

        new HbrStoryCharacterEntry("wakki", "山胁·波昂·伊瓦尔", "31C"),
        new HbrStoryCharacterEntry("seira", "樱庭星罗", "31C"),
        new HbrStoryCharacterEntry("miko", "天音巫呼", "31C"),
        new HbrStoryCharacterEntry("yayoi", "丰后弥生", "31C"),
        new HbrStoryCharacterEntry("adelheid", "神崎阿黛尔海德", "31C"),
        new HbrStoryCharacterEntry("mari", "佐月麻里", "31C"),

        new HbrStoryCharacterEntry("misato", "二阶堂三乡", "31D"),
        new HbrStoryCharacterEntry("iroha", "石井色叶", "31D"),
        new HbrStoryCharacterEntry("fubuki", "命吹雪", "31D"),
        new HbrStoryCharacterEntry("risa", "室伏理沙", "31D"),
        new HbrStoryCharacterEntry("akari", "伊达朱里", "31D"),
        new HbrStoryCharacterEntry("aina", "瑞原蓝娜", "31D"),

        new HbrStoryCharacterEntry("ichiko", "大岛一千子", "31E"),
        new HbrStoryCharacterEntry("nina", "大岛二以奈", "31E"),
        new HbrStoryCharacterEntry("minori", "大岛三野里", "31E"),
        new HbrStoryCharacterEntry("yotsuha", "大岛四叶草", "31E"),
        new HbrStoryCharacterEntry("isuzu", "大岛五十铃", "31E"),
        new HbrStoryCharacterEntry("mua", "大岛六宇亚", "31E"),

        new HbrStoryCharacterEntry("mion", "柳美音", "31F"),
        new HbrStoryCharacterEntry("kanata", "丸山奏多", "31F"),
        new HbrStoryCharacterEntry("shiki", "华村诗纪", "31F"),
        new HbrStoryCharacterEntry("chiroru", "松冈绮罗路", "31F"),
        new HbrStoryCharacterEntry("inori", "夏目祈", "31F"),
        new HbrStoryCharacterEntry("maki", "黑泽真希", "31F"),

        new HbrStoryCharacterEntry("yuina", "白河由依奈", "30G"),
        new HbrStoryCharacterEntry("monaka", "月城最中", "30G"),
        new HbrStoryCharacterEntry("miya", "桐生美也", "30G"),
        new HbrStoryCharacterEntry("chie", "菅原千惠", "30G"),
        new HbrStoryCharacterEntry("hisame", "小笠原绯雨", "30G"),
        new HbrStoryCharacterEntry("satomi", "藏里见", "30G"),

        new HbrStoryCharacterEntry("carol", "卡罗尔・利帕", "31X"),
        new HbrStoryCharacterEntry("yingxia", "李映夏", "31X"),
        new HbrStoryCharacterEntry("irene", "艾琳・雷德梅茵", "31X"),
        new HbrStoryCharacterEntry("vritika", "芙莉缇卡・巴拉克里希兰", "31X"),
        new HbrStoryCharacterEntry("maria", "玛利亚・德・安洁莉斯", "31X"),
        new HbrStoryCharacterEntry("charlotta", "夏洛塔・斯科波夫斯卡娅", "31X"),

        new HbrStoryCharacterEntry("tezuka_saki", "手冢咲", "司令部"),
        new HbrStoryCharacterEntry("nanami", "七濑七海", "司令部"),
        new HbrStoryCharacterEntry("mutsumi", "若叶睦", "司令部"),

        new HbrStoryCharacterEntry("taki", "椎名立希", "30A"),
        new HbrStoryCharacterEntry("rana", "要乐奈", "30A"),
        new HbrStoryCharacterEntry("tomori", "高松灯", "30A"),
        new HbrStoryCharacterEntry("anon", "千早爱音", "30A"),
        new HbrStoryCharacterEntry("soyo", "长崎素世", "30A")
    });

    private static readonly HashSet<string> KnownIds = new(
        Entries.Select(character => character.Id), StringComparer.Ordinal);

    public static bool IsKnown(string id) => id != null && KnownIds.Contains(id);
}
