using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
namespace HBRGOSakiMod.Core.Models.Powers;
// A marker kept through death so removal/revival policy remains unambiguous.
public sealed class KozueSpiritPower : PowerModel, ICustomPower, IHbrAfterDeathListener
{
    [SavedProperty] public bool Inspiration { get; private set; }
    [SavedProperty] public bool Smile { get; private set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
    internal void Configure(InnocentRequiemHbrgoSaki card)
    { Inspiration = card.HasUpgradeEffect(InnocentRequiemHbrgoSaki.InspirationContagious); Smile = card.HasUpgradeEffect(InnocentRequiemHbrgoSaki.UnchangingSmile); }
    public async Task AfterDeath(PlayerChoiceContext context, Creature target, bool prevented, float anim)
    { if (target == Owner && !prevented) await KozueSpiritHelper.RemoveAuras(Owner); }
    public override Task AfterRemoved(Creature oldOwner) => KozueSpiritHelper.RemoveAuras(oldOwner);
}
public sealed class KozueSpiritShellPower : HighDimensionalShellPower
{
    protected override decimal MaximumAmount => decimal.Floor(LargeCreatureHealthHelper.GetMaxHp(Owner) * .2m);
    public override Task BeforeDamageReceived(PlayerChoiceContext context, Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel source) => Task.CompletedTask;
    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel source) => 1m;
}
