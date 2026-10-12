using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Story;

internal static class HbrStoryChoicePreview
{
    private static readonly IReadOnlyDictionary<string, string> CharacterNames =
        HbrStoryCharacterCatalog.Entries.ToDictionary(character => character.Id, character => character.Name);

    internal static IReadOnlyList<string> GetEncounterNames(
        HbrStoryDefinition definition,
        HbrStoryPage currentPage,
        HbrStoryChoice choice)
    {
        List<string> names = new();
        HashSet<string> seenCharacters = new(StringComparer.Ordinal);
        HashSet<string> visitedPages = new(StringComparer.Ordinal) { currentPage.Id };
        AddCharacters(currentPage);
        string pageId = choice.NextPage;
        while (visitedPages.Add(pageId))
        {
            HbrStoryPage page = definition.GetPage(pageId);
            AddCharacters(page);

            // Follow this choice's story, but do not promise encounters from
            // branches the player will still need to choose between later.
            if (page.Choices.Count != 1)
            {
                break;
            }
            pageId = page.Choices[0].NextPage;
        }
        return names;

        void AddCharacters(HbrStoryPage page)
        {
            foreach (string characterId in page.MetCharacters)
            {
                if (seenCharacters.Add(characterId) && CharacterNames.TryGetValue(characterId, out string name))
                {
                    names.Add(name);
                }
            }
        }
    }
}
