using System;
using System.Collections.Generic;
using System.Text;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// Memory garden card names come back bit by bit (ui-concepts/memory-garden-names-20261106). Obtaining the card is
// one part and every recollection (upgrade) another, out of MaxUpgradeLevel + 1 parts. Hidden characters show the
// classic 乱码: the character's UTF-8 bytes read as GBK, two bytes making one character, so a name keeps its length.
// Punctuation and spaces always show. Plain data, so validation/hbr-card-style checks it without Godot.
internal static class HbrMemoryTitleReveal
{
    // The seal shader's static steps every 0.6s; in the garden about one hidden character in five jumps with it.
    internal const double JumpSeconds = .6;
    internal const int JumpPercent = 22;
    // Newly revealed characters flicker for .52s each, .14s apart, after .25s, then settle on the real character.
    internal const double DecodeDelay = .25;
    internal const double DecodeStagger = .14;
    internal const double DecodeLength = .52;
    internal const double DecodeFlicker = .045;

    internal const string Kept = "、，。！？；：…—·「」『』（）()!?,.;:'\"";
    internal const string LatinNoise = "ÃÂâ€¢£¤¥¦§©ª«¬®°±²³µ¶·¹º»¼½¾¿ÐØÞßæðøþ";

    // GBK readings of the UTF-8 byte pairs E3-E9 and EF followed by 80-BF, 64 per lead byte. Every one of them is in
    // the game's Chinese card font (Noto Sans Mono CJK SC), checked when the preview was built.
    internal const string Moji =
        "銆銇銈銉銊銋銌銍銏銐銑銒銓銔銕銖銗銘銙銚銛銜銝銞銟銠銡銢銣銤銥銦銧恪恽悖悚悭悝悃悒悌悛惬悻悱惝惘惆惚悴愠愦愕愣惴愀愎愫慊慵憬憔憧" + // E3 80-BF
        "鋩鋪鋫鋬鋭鋮鋯鋰鋱鋲鋳鋴鋵鋶鋷鋸鋹鋺鋻鋼鋽鋾鋿錀錁錂錃錄錅錆錇錈錉洹洧洌浃浈洇洄洙洎洫浍洮洵洚浏浒浔洳涑浯涞涠浞涓涔浜浠浼浣渚淇" + // E4 80-BF
        "鍊鍋鍌鍍鍎鍏鍐鍑鍒鍓鍔鍕鍖鍗鍘鍙鍚鍛鍜鍝鍞鍟鍠鍡鍢鍣鍤鍥鍦鍧鍨鍩鍫濉澧澹澶濂濡濮濞濠濯瀚瀣瀛瀹瀵灏灞宀宄宕宓宥宸甯骞搴寤寮褰寰蹇" + // E5 80-BF
        "鎬鎭鎮鎯鎰鎱鎲鎳鎴鎵鎶鎷鎸鎹鎺鎻鎼鎽鎾鎿鏀鏁鏂鏃鏄鏅鏆鏇鏈鏉鏋鏌鏍妗姊妫妞妤姒妲妯姗妾娅娆姝娈姣姘姹娌娉娲娴娑娣娓婀婧婊婕娼婢婵" + // E6 80-BF
        "鐎鐏鐐鐑鐒鐓鐔鐕鐖鐗鐘鐙鐚鐛鐜鐝鐞鐟鐠鐡鐢鐣鐤鐥鐦鐧鐨鐩鐪鐫鐬鐭鐮纭纰纾绀绁绂绉绋绌绐绔绗绛绠绡绨绫绮绯绱绲缍绶绺绻绾缁缂缃缇缈" + // E7 80-BF
        "鑰鑱鑲鑳鑴鑵鑶鑷鑸鑹鑺鑻鑼鑽鑾鑿钀钁钂钃钄钑钖钘铇铏铓铔铚铦铻锜锠琛琚瑁瑜瑗瑕瑙瑷瑭瑾璜璎璀璁璇璋璞璨璩璐璧瓒璺韪韫韬杌杓杞杈杩" + // E8 80-BF
        "閫閬閭閮閯閰閱閲閳閴閵閶閷閸閹閺閻閼閽閾閿闀闁闂闃闄闅闆闇闈闉闊闋椤棰椋椁楗棣椐楱椹楠楂楝榄楫榀榘楸椴槌榇榈槎榉楦楣楹榛榧榻榫榭" + // E9 80-BF
        "飥飦飩飪飫飬飭飮飯飰飱飲飳飴飵飶飷飸飹飺飻飼飽飾飿餀餁餂餃餄餅餆餇铩铫铮铯铳铴铵铷铹铼铽铿锃锂锆锇锉锊锍锎锏锒锓锔锕锖锘锛锝锞锟"; // EF 80-BF

    internal static int TotalParts(int maxUpgradeLevel) => Math.Max(0, maxUpgradeLevel) + 1;

    // Obtaining is the first part; every upgrade level adds one, up to the card's cap.
    internal static int OwnedParts(int upgradeLevel, int maxUpgradeLevel) =>
        Math.Clamp(upgradeLevel, 0, Math.Max(0, maxUpgradeLevel)) + 1;

    // Characters shown out of `countable`: countable x parts / total, rounded down; at least one once the card is
    // obtained, all of them at the cap.
    internal static int Shown(int countable, int parts, int totalParts)
    {
        if (countable <= 0 || parts <= 0) return 0;
        if (parts >= totalParts) return countable;
        return Math.Clamp(countable * parts / totalParts, 1, countable);
    }

    internal static bool IsKept(Rune rune) =>
        Rune.IsWhiteSpace(rune) || (rune.IsBmp && Kept.IndexOf((char)rune.Value) >= 0);

    // The hidden positions (rune indices) in the order they come back, shuffled with the card id as seed so every
    // run and every player sees the same order.
    internal static int[] Order(string cardId, IReadOnlyList<Rune> runes)
    {
        List<int> positions = new();
        for (int index = 0; index < runes.Count; index++)
            if (!IsKept(runes[index])) positions.Add(index);
        uint state = Fnv(cardId ?? string.Empty);
        for (int index = positions.Count - 1; index > 0; index--)
        {
            int other = (int)((ulong)Next(ref state) * (ulong)(index + 1) >> 32);
            (positions[index], positions[other]) = (positions[other], positions[index]);
        }
        return positions.ToArray();
    }

    // The GBK reading of a character's first two UTF-8 bytes, or null outside the table (Latin and rare scripts).
    internal static char? MojiOf(Rune rune)
    {
        Span<byte> bytes = stackalloc byte[4];
        int length = rune.EncodeToUtf8(bytes);
        if (length < 2 || bytes[1] < 0x80 || bytes[1] > 0xBF) return null;
        int lead = bytes[0] switch
        {
            >= 0xE3 and <= 0xE9 => bytes[0] - 0xE3,
            0xEF => 7,
            _ => -1
        };
        return lead < 0 ? null : Moji[lead * 64 + bytes[1] - 0x80];
    }

    // How a name reads with `parts` of `totalParts` earned. `jumpStep` >= 0 lets a few hidden characters jump with the
    // static. While `decodeElapsed` is under DecodeDuration, the characters between `decodeFromParts` and `parts` are
    // still decoding.
    internal static string Garble(string cardId, string title, int parts, int totalParts, long jumpStep = -1,
        int decodeFromParts = -1, double decodeElapsed = double.MaxValue)
    {
        if (string.IsNullOrEmpty(title)) return title ?? string.Empty;
        List<Rune> runes = new();
        foreach (Rune rune in title.EnumerateRunes()) runes.Add(rune);
        int[] order = Order(cardId, runes);
        int shown = Shown(order.Length, parts, totalParts);
        int decodeFrom = decodeFromParts < 0 ? shown : Math.Min(shown, Shown(order.Length, decodeFromParts, totalParts));
        Dictionary<int, int> rank = new();
        for (int index = 0; index < order.Length; index++) rank[order[index]] = index;

        StringBuilder text = new(title.Length);
        for (int index = 0; index < runes.Count; index++)
        {
            Rune rune = runes[index];
            if (!rank.TryGetValue(index, out int place))
            {
                text.Append(rune.ToString());
                continue;
            }
            if (place < decodeFrom)
            {
                text.Append(rune.ToString());
                continue;
            }
            if (place < shown)
            {
                double start = DecodeDelay + (place - decodeFrom) * DecodeStagger;
                if (decodeElapsed >= start + DecodeLength) text.Append(rune.ToString());
                else if (decodeElapsed >= start) text.Append(Flicker(rune, index, decodeElapsed));
                else text.Append(Hidden(cardId, index, rune, jumpStep));
                continue;
            }
            text.Append(Hidden(cardId, index, rune, jumpStep));
        }
        return text.ToString();
    }

    // Seconds the decode of `count` newly revealed characters takes.
    internal static double DecodeDuration(int count) =>
        count <= 0 ? 0 : DecodeDelay + (count - 1) * DecodeStagger + DecodeLength;

    // Characters a decode from `fromParts` to `toParts` brings back.
    internal static int DecodeCount(string title, string cardId, int fromParts, int toParts, int totalParts)
    {
        List<Rune> runes = new();
        foreach (Rune rune in (title ?? string.Empty).EnumerateRunes()) runes.Add(rune);
        int countable = Order(cardId, runes).Length;
        return Math.Max(0, Shown(countable, toParts, totalParts) - Shown(countable, fromParts, totalParts));
    }

    // A decoding character runs through garbage of its own script, a new one every DecodeFlicker seconds.
    private static char Flicker(Rune rune, int index, double elapsed)
    {
        string pool = rune.Value < 0x2E80 ? LatinNoise : Moji;
        return pool[(int)(((long)(elapsed / DecodeFlicker) * 7 + index * 13) % pool.Length)];
    }

    private static char Hidden(string cardId, int index, Rune rune, long jumpStep)
    {
        bool latin = rune.Value < 0x2E80;
        bool jump = jumpStep >= 0 && Hash(cardId, index, jumpStep) % 100 < JumpPercent;
        uint hash = Hash(cardId, index, jump ? jumpStep : -1);
        if (latin) return LatinNoise[(int)(hash % (uint)LatinNoise.Length)];
        if (!jump && MojiOf(rune) is { } moji) return moji;
        return Moji[(int)(hash % (uint)Moji.Length)];
    }

    private static uint Hash(string cardId, int index, long step) => Fnv($"{cardId}|{index}|{step}");

    private static uint Fnv(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }

    // mulberry32
    private static uint Next(ref uint state)
    {
        unchecked
        {
            state += 0x6D2B79F5;
            uint t = state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return t ^ (t >> 14);
        }
    }
}
