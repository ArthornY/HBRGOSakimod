using System.Threading.Tasks;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class BecomeGodHbrgoSaki : MomentOfReign
{
    private const int PhantomLevelBonus = 2;

    protected override int NewlyObtainedPhantomLevelBonus =>
        PhantomLevelBonus;

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        AddLevelsToAllRosterEntries(PhantomLevelBonus);
    }
}
