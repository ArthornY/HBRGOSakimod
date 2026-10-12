using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunes;

public enum HextechRarityTier
{
    Silver = 0,
    Gold = 1,
    Prismatic = 2
}

[Flags]
public enum PlayerRuneFlags
{
    None = 0,
    Disabled = 1,
    AttributeConversionExclusive = 2,
    FirstActExcluded = 4,
    ThirdActExcluded = 8,
    SelectionExcluded = 16
}

public enum PlayerRuneCharacterPool
{
    Ironclad,
    Silent,
    Regent,
    Defect,
    Necrobinder
}

public abstract class HextechRelicBase : RelicModel
{
    public sealed override RelicRarity Rarity => RelicRarity.Starter;

    public virtual bool IsAvailableForPlayer(Player player)
    {
        return true;
    }

    public virtual Task AfterTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side)
    {
        return Task.CompletedTask;
    }
}

public abstract class HextechForgeBase : HextechRelicBase
{
}

public static class HextechRunesApi
{
    public static void RegisterPlayerRune<TRune>(
        HextechRarityTier rarity,
        PlayerRuneFlags flags = PlayerRuneFlags.None,
        PlayerRuneCharacterPool? characterPool = null,
        int characterOrder = 0,
        string tagKey = "COMPREHENSIVE",
        string? assetModId = null)
        where TRune : HextechRelicBase
    {
        throw new NotSupportedException(
            "This compile-time reference assembly must not be loaded at runtime.");
    }

    public static void RegisterForge<TForge>(
        HextechRarityTier rarity,
        string? assetModId = null)
        where TForge : HextechForgeBase
    {
        throw new NotSupportedException(
            "This compile-time reference assembly must not be loaded at runtime.");
    }
}
