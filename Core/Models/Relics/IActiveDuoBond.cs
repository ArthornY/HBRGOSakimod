namespace HBRGOSakiMod.Core.Models.Relics;

// A future two-member bond opts in here rather than extending a card-specific
// whitelist. Activation remains owned by that relic's permanent-deck rule.
public interface IActiveDuoBond
{
    bool IsBondActive { get; }
}
