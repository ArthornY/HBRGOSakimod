$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '..\Core\Helpers\HotaruMyHappinessCardValueHelper.cs'
$source = (Get-Content -LiteralPath $sourcePath -Raw).Replace(
    'namespace HBRGOSakiMod.Core.Helpers;', 'namespace HBRGOSakiMod.Core.Helpers {') + "`n}"
$harness = @'

// The production helper is compiled unchanged against small in-memory API
// stand-ins. Tests exercise its actual scopes, weak table and getter arithmetic.
namespace HarmonyLib
{
    public static class AccessTools
    {
        public delegate ref TField FieldRef<T, TField>(T instance);
        public static FieldRef<T, TField> FieldRefAccess<T, TField>(string fieldName)
        {
            var field = typeof(T).GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            var method = new System.Reflection.Emit.DynamicMethod("FieldRef",
                typeof(TField).MakeByRefType(), new[] { typeof(T) }, typeof(AccessTools), true);
            var il = method.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            il.Emit(System.Reflection.Emit.OpCodes.Ldflda, field);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return method.CreateDelegate<FieldRef<T, TField>>();
        }
    }
}
namespace MegaCrit.Sts2.Core.Entities.Cards
{
    public enum PileType { Hand, Deck, Discard }
}
namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public sealed class Creature
    {
        public object CombatState = new object();
        public HBRGOSakiMod.Core.Models.Powers.HotaruMyHappinessPower Power;
        public T GetPower<T>() where T : class => Power as T;
    }
}
namespace MegaCrit.Sts2.Core.Models
{
    public class AbstractModel { }
    public sealed class Player
    {
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Creature = new();
        public Deck Deck = new();
    }
    public sealed class Deck
    {
        public TrackingCards Cards = new();
    }
    public sealed class TrackingCards : System.Collections.ObjectModel.Collection<CardModel>
    {
        public int Searches;
        public new bool Contains(CardModel card) { Searches++; return base.Contains(card); }
    }
    public sealed class Pile
    {
        public MegaCrit.Sts2.Core.Entities.Cards.PileType Type;
    }
    public sealed class CardModel : AbstractModel
    {
        public bool IsMutable = true;
        public Player Owner;
        public object CombatState;
        public Pile Pile = new();
    }
}
namespace MegaCrit.Sts2.Core.Localization.DynamicVars
{
    public class DynamicVar
    {
        protected MegaCrit.Sts2.Core.Models.AbstractModel _owner;
        private decimal _baseValue;
        public DynamicVar(MegaCrit.Sts2.Core.Models.AbstractModel owner, decimal value = 10m)
        { _owner = owner; _baseValue = value; }
    }
    public class CalculatedVar : DynamicVar
    { public CalculatedVar(MegaCrit.Sts2.Core.Models.AbstractModel owner) : base(owner) { } }
    public class DamageVar : DynamicVar
    { public DamageVar(MegaCrit.Sts2.Core.Models.AbstractModel owner) : base(owner) { } }
    public class CalculatedDamageVar : DynamicVar
    { public CalculatedDamageVar(MegaCrit.Sts2.Core.Models.AbstractModel owner) : base(owner) { } }
    public class OstyDamageVar : DynamicVar
    { public OstyDamageVar(MegaCrit.Sts2.Core.Models.AbstractModel owner) : base(owner) { } }
}
namespace STS2RitsuLib.Cards.DynamicVars
{
    public interface IComputedDynamicVar { }
}
namespace HBRGOSakiMod.Core.Models.Powers
{
    public sealed class HotaruMyHappinessPower
    {
        public decimal Bonus;
        public decimal GetCardBaseValueBonus(MegaCrit.Sts2.Core.Models.CardModel card) => Bonus;
    }
}
namespace HBRGOSakiMod.Core.Helpers
{
    internal static class DamageLimitHelper { internal const decimal MaximumSingleHitDamage = 999999999m; }
    internal static class DamageAmplificationLimits
    { internal static decimal AddSaturated(decimal left, decimal right) => left + right; }

    public static class HappinessCardValueRegression
    {
        public static string Run()
        {
            var player = new MegaCrit.Sts2.Core.Models.Player();
            var card = new MegaCrit.Sts2.Core.Models.CardModel
            { Owner = player, CombatState = player.Creature.CombatState };
            var variable = new MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar(card);
            Check(HotaruMyHappinessCardValueHelper.GetBonus(variable) == 0m &&
                player.Deck.Cards.Searches == 0, "Absent power must bypass the permanent deck scan.");
            HotaruMyHappinessCardValueHelper.RecordEnchantedWrite(variable);
            HotaruMyHappinessCardValueHelper.RecordPreviewWrite(variable);
            using (HotaruMyHappinessCardValueHelper.CalculateDisplayValues())
            {
                HotaruMyHappinessCardValueHelper.RecordEnchantedWrite(variable);
                HotaruMyHappinessCardValueHelper.RecordPreviewWrite(variable);
            }
            Check(!HasReceipt(variable), "Zero-bonus writes must not allocate a display receipt.");

            player.Creature.Power = new Models.Powers.HotaruMyHappinessPower { Bonus = 5m };
            Check(HotaruMyHappinessCardValueHelper.BaseValue(variable, 10m) == 15m,
                "A combat card must receive the bonus.");
            player.Deck.Cards.Add(card);
            Check(HotaruMyHappinessCardValueHelper.GetBonus(variable) == 0m,
                "The permanent deck card must remain unboosted even if its pile is not Deck.");
            player.Deck.Cards.Clear();
            card.Pile.Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck;
            Check(HotaruMyHappinessCardValueHelper.GetBonus(variable) == 0m,
                "Deck pile cards must remain unboosted.");
            card.Pile.Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand;
            card.CombatState = new object();
            Check(HotaruMyHappinessCardValueHelper.GetBonus(variable) == 0m,
                "A card from another combat must remain unboosted.");
            card.CombatState = player.Creature.CombatState;

            using (HotaruMyHappinessCardValueHelper.CalculateDisplayValues())
            {
                HotaruMyHappinessCardValueHelper.RecordEnchantedWrite(variable);
                HotaruMyHappinessCardValueHelper.RecordPreviewWrite(variable);
            }
            Check(HasReceipt(variable) && HotaruMyHappinessCardValueHelper.EnchantedValue(variable, 15m) == 15m &&
                HotaruMyHappinessCardValueHelper.PreviewValue(variable, 15m) == 15m,
                "Boosted writes must retain receipts and avoid a second bonus.");
            player.Creature.Power = null;
            Check(HotaruMyHappinessCardValueHelper.EnchantedValue(variable, 15m) == 10m &&
                HotaruMyHappinessCardValueHelper.PreviewValue(variable, 15m) == 10m,
                "Removing the power must still subtract the old included bonus.");
            using (HotaruMyHappinessCardValueHelper.CalculateDisplayValues())
            {
                HotaruMyHappinessCardValueHelper.RecordEnchantedWrite(variable);
                HotaruMyHappinessCardValueHelper.RecordPreviewWrite(variable);
            }
            Check(HotaruMyHappinessCardValueHelper.EnchantedValue(variable, 10m) == 10m &&
                HotaruMyHappinessCardValueHelper.PreviewValue(variable, 10m) == 10m,
                "Unboosted writes after power removal must clear existing receipts.");
            player.Creature.Power = new Models.Powers.HotaruMyHappinessPower { Bonus = 5m };
            Check(HotaruMyHappinessCardValueHelper.PreviewValue(variable, 10m) == 15m,
                "Reapplying the power after clearing receipts must restore the bonus.");
            using (HotaruMyHappinessCardValueHelper.ReadRawValues())
                Check(HotaruMyHappinessCardValueHelper.GetBonus(variable) == 0m,
                    "Raw scopes must bypass the bonus.");
            var capped = new MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar(card, 999999997m);
            Check(HotaruMyHappinessCardValueHelper.GetBonus(capped) == 2m,
                "Receipts must record only the retained increase at the native ceiling.");
            var zero = new MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar(card);
            player.Creature.Power.Bonus = 0m;
            int searchesBefore = player.Deck.Cards.Searches;
            using (HotaruMyHappinessCardValueHelper.CalculateDisplayValues())
            {
                HotaruMyHappinessCardValueHelper.RecordEnchantedWrite(zero);
                HotaruMyHappinessCardValueHelper.RecordPreviewWrite(zero);
            }
            Check(!HasReceipt(zero) && player.Deck.Cards.Searches == searchesBefore,
                "An active power with no card bonus must bypass receipt creation and deck scans.");
            return "PASS: absent/zero bonus fast paths, combat/permanent/deck cards, power removal and reapply, receipt clearing, raw scope and cap.";
        }

        private static bool HasReceipt(MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar variable)
        {
            object table = typeof(HotaruMyHappinessCardValueHelper).GetField("DisplayStates",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            return (bool)table.GetType().GetMethod("TryGetValue").Invoke(table, new object[] { variable, null });
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); }
    }
}
'@

Add-Type -TypeDefinition ($source + $harness)
[HBRGOSakiMod.Core.Helpers.HappinessCardValueRegression]::Run()
