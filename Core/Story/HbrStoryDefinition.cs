#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace HBRGOSakiMod.Core.Story;

/// <summary>Versioned, declarative story data shared by the game and external editor.</summary>
public sealed class HbrStoryDefinition
{
    public const int SupportedSchemaVersion = 1;
    public const int MaximumChoicesPerPage = 6;
    public const int MaximumCombinedChoicesPerPage = 9;
    public const int ExtendedGameplayVersion = 1;
    public const int MemoryGardenEncounterVersion = 2;
    public const int MaximumCharactersPerScene = 8;
    public const string EventIdPrefix = "HBRGO_SAKI_MOD_EVENT_";

    private static readonly Regex Identifier = new("^[A-Z][A-Z0-9_]{0,79}$", RegexOptions.CultureInvariant);
    private static readonly Regex CharacterName = new("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SenseNames = new(StringComparer.Ordinal)
    {
        "LeadershipCharm", "Talent", "Gentleness", "MentalStrength", "Simplicity", "Madness"
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string EventPool { get; init; } = "shared";
    public required HbrStoryConditions Conditions { get; init; }
    public required HbrStoryPresentation Presentation { get; init; }
    public required string StartPage { get; init; }
    public required IReadOnlyList<HbrStoryPage> Pages { get; init; }

    [JsonIgnore]
    public bool UsesExtendedGameplay => Pages.Any(page => page.MetCharacters.Count > 0 || page.ChoiceMode != "list" || page.ChoiceLeft.Count > 0 || page.ChoiceRight.Count > 0
        || page.Choices.Any(choice => choice.Combat.Length > 0 || choice.Rewards.CardRarity != "default"
            || choice.Rewards.Potions > 0 || choice.Rewards.CardRemovals > 0 || choice.Rewards.CardUpgrades > 0
            || choice.Rewards.HealFull || choice.Rewards.MaxDp > 0));

    /// <summary>Omit extension defaults so unchanged V1 stories still load in the previous runtime.</summary>
    public static IJsonTypeInfoResolver CreateSerializationResolver()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            foreach (JsonPropertyInfo property in info.Properties)
            {
                if (info.Type == typeof(HbrStoryPage) && property.Name == "choiceMode")
                    property.ShouldSerialize = (_, value) => !Equals(value, "list");
                else if (info.Type == typeof(HbrStoryPage) && property.Name is "choiceLeft" or "choiceRight" or "metCharacters")
                    property.ShouldSerialize = (_, value) => value is not IReadOnlyList<string> list || list.Count > 0;
                else if (info.Type == typeof(HbrStoryChoice) && property.Name == "combat")
                    property.ShouldSerialize = (_, value) => !Equals(value, "");
                else if (info.Type == typeof(HbrStoryRewards) && property.Name == "cardRarity")
                    property.ShouldSerialize = (_, value) => !Equals(value, "default");
            }
        });
        return resolver;
    }

    /// <summary>Parse and validate before the game can enter any page or apply rewards.</summary>
    public static HbrStoryDefinition Parse(string json, string source = "story")
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            RejectDuplicateProperties(document.RootElement, "$", source);
            HbrStoryDefinition definition = JsonSerializer.Deserialize<HbrStoryDefinition>(json, JsonOptions)
                ?? throw Invalid(source, "根节点必须是事件对象。");
            definition.Validate(source);
            return definition;
        }
        catch (JsonException exception)
        {
            throw Invalid(source, $"JSON 格式错误：{exception.Message}", exception);
        }
    }

    public HbrStoryPage GetPage(string pageId) => Pages.FirstOrDefault(page => page.Id == pageId)
        ?? throw Invalid(Id, $"找不到剧情页 {pageId}。");

    public void Validate(string source = "story")
    {
        Require(SchemaVersion == SupportedSchemaVersion, source, "schemaVersion 必须为 1。");
        CheckIdentifier(Id, source, "id");
        Require(!string.IsNullOrWhiteSpace(Title), source, "title 不能为空。");
        Require(EventPool is "shared" or "memoryFragments", source,
            "eventPool 必须为 shared 或 memoryFragments。");
        Require(Conditions != null, source, "conditions 不能为空。");
        Require(Presentation != null, source, "presentation 不能为空。");
        Require(Pages != null && Pages.Count > 0, source, "至少需要一个剧情页。");
        CheckIdentifier(StartPage, source, "startPage");

        Require(Conditions.PartyCharacters != null && Conditions.Acts != null && Conditions.AfterEventIds != null,
            source, "条件列表必须为数组，不能为 null。");
        Require(Conditions.PartyCharacters.All(value => value != null && CharacterName.IsMatch(value)),
            source, "partyCharacters 必须填写角色类名。");
        Require(Conditions.Acts.All(value => value is >= 1 and <= 99), source, "acts 使用从 1 开始的幕编号（1–99）。");
        foreach (string eventId in Conditions.AfterEventIds)
        {
            CheckIdentifier(eventId, source, "afterEventIds");
            Require(NormalizeEventId(eventId) != NormalizeEventId(Id), source, "前置事件不能是自身。");
        }
        CheckUnique(Conditions.PartyCharacters, source, "partyCharacters");
        CheckUnique(Conditions.AfterEventIds.Select(NormalizeEventId), source, "afterEventIds");
        Require(Conditions.Acts.Distinct().Count() == Conditions.Acts.Count, source, "acts 不可重复。");
        CheckImagePath(Presentation.Background, source, "presentation.background", optional: false);
        CheckImagePath(Presentation.ChoicePortrait, source, "presentation.choicePortrait", optional: true);
        CheckImagePath(Presentation.ThoughtPortrait, source, "presentation.thoughtPortrait", optional: true);
        Require(Presentation.DialogueStyle is "classic" or "original", source,
            "presentation.dialogueStyle 必须为 classic 或 original。");
        CheckPortraitLayout(Presentation.PortraitLayout, source, "presentation.portraitLayout", optional: false);

        HashSet<string> pageIds = new(StringComparer.Ordinal);
        foreach (HbrStoryPage page in Pages)
        {
            Require(page != null, source, "剧情页不能为 null。");
            CheckIdentifier(page.Id, source, "page.id");
            Require(pageIds.Add(page.Id), source, $"重复的剧情页：{page.Id}。");
            Require(page.ChoicePrompt != null && page.Lines != null && page.Choices != null,
                source, $"{page.Id} 的提示、台词和选项不能为 null。");
            Require(page.ChoiceMode is "list" or "combine", source, $"{page.Id}.choiceMode 必须为 list 或 combine。");
            Require(page.ChoiceLeft != null && page.ChoiceRight != null, source, $"{page.Id} 的组合选项文本必须为数组。");
            if (page.ChoiceMode == "combine")
                Require(page.ChoiceLeft.Count == 3 && page.ChoiceRight.Count == 3 && page.Choices.Count == MaximumCombinedChoicesPerPage
                    && page.ChoiceLeft.Concat(page.ChoiceRight).All(text => !string.IsNullOrWhiteSpace(text)),
                    source, $"{page.Id} 的组合选择必须为左侧 3 项、右侧 3 项非空文本及按左 × 3 + 右排列的 9 个结果。");
            else Require(page.Choices.Count <= MaximumChoicesPerPage, source, $"{page.Id} 最多支持 {MaximumChoicesPerPage} 个列表选项。");
            CheckImagePath(page.Background, source, $"{page.Id}.background", optional: true);
            CheckCharacters(page.Characters, source, $"{page.Id}.characters", optional: false);
            Require(page.MetCharacters != null, source, $"{page.Id}.metCharacters 必须为数组，不能为 null。");
            Require(page.MetCharacters.All(HbrStoryCharacterCatalog.IsKnown),
                source, $"{page.Id}.metCharacters 包含未知角色，请使用编辑器角色名册中的角色编号。");
            CheckUnique(page.MetCharacters, source, $"{page.Id}.metCharacters");
            int lineIndex = 0;
            foreach (HbrStoryLine line in page.Lines)
            {
                Require(line != null && line.Kind is "narration" or "thought" or "dialogue", source,
                    $"{page.Id} 的 kind 必须为 narration、thought 或 dialogue。");
                Require(line.Speaker != null && !string.IsNullOrWhiteSpace(line.Text), source,
                    $"{page.Id} 的 speaker 不能为 null，台词 text 不能为空。");
                CheckImagePath(line.Portrait, source, $"{page.Id}.line.portrait", optional: true);
                CheckImagePath(line.Background, source, $"{page.Id}.lines[{lineIndex}].background", optional: true);
                CheckPortraitLayout(line.PortraitLayout, source, $"{page.Id}.lines[{lineIndex}].portraitLayout", optional: true);
                CheckCharacters(line.Characters, source, $"{page.Id}.lines[{lineIndex}].characters", optional: true);
                lineIndex++;
            }

            HashSet<string> choiceIds = new(StringComparer.Ordinal);
            foreach (HbrStoryChoice choice in page.Choices)
            {
                Require(choice != null, source, $"{page.Id} 的选项不能为 null。");
                CheckIdentifier(choice.Id, source, $"{page.Id}.choice.id");
                Require(choiceIds.Add(choice.Id), source, $"{page.Id} 有重复选项：{choice.Id}。");
                Require(!string.IsNullOrWhiteSpace(choice.Text) && choice.Description != null,
                    source, $"{page.Id}/{choice.Id} 需要选项文字，description 不能为 null。");
                CheckIdentifier(choice.NextPage, source, $"{page.Id}/{choice.Id}.nextPage");
                Require(choice.Combat is "" or "nararaShibaBoss", source, $"{page.Id}/{choice.Id}.combat 仅支持空字符串或 nararaShibaBoss。");
                HbrStoryRewards rewards = choice.Rewards;
                Require(rewards != null && rewards.Sense != null, source, $"{page.Id}/{choice.Id} 的 rewards 不能为 null。");
                Require(rewards.Sense.Length == 0 || SenseNames.Contains(rewards.Sense), source,
                    $"{page.Id}/{choice.Id} 使用了未知六感：{rewards.Sense}。");
                Require(rewards.SenseAmount is >= 0 and <= 99, source, "senseAmount 必须在 0–99 之间。");
                Require(rewards.Sense.Length > 0 || rewards.SenseAmount == 0, source, "未选择六感时，senseAmount 必须为 0。");
                Require(rewards.CardChoices is >= 0 and <= 10, source, "cardChoices 必须在 0–10 之间，0 表示无卡牌奖励。");
                Require(rewards.CardRarity is "default" or "rare", source, "cardRarity 必须为 default 或 rare。");
                Require(rewards.Potions is >= 0 and <= 10, source, "potions 必须在 0–10 之间。");
                Require(rewards.CardRemovals is >= 0 and <= 10, source, "cardRemovals 必须在 0–10 之间。");
                Require(rewards.CardUpgrades is >= 0 and <= 10, source, "cardUpgrades 必须在 0–10 之间。");
                Require(rewards.MaxDp is >= 0 and <= 999, source, "maxDp 必须在 0–999 之间。");
            }
        }

        Require(pageIds.Contains(StartPage), source, $"起始页 {StartPage} 不存在。");
        foreach (HbrStoryPage page in Pages)
        {
            foreach (HbrStoryChoice choice in page.Choices)
            {
                Require(pageIds.Contains(choice.NextPage), source,
                    $"{page.Id}/{choice.Id} 指向不存在的剧情页 {choice.NextPage}。");
            }
        }
        ValidateGraph(source);
    }

    /// <summary>Both a short custom ID and the final model entry are accepted for prerequisites.</summary>
    public static string NormalizeEventId(string id) => id.StartsWith(EventIdPrefix, StringComparison.Ordinal)
        ? id[EventIdPrefix.Length..] : id;

    private void ValidateGraph(string source)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        Stack<string> pending = new();
        pending.Push(StartPage);
        while (pending.TryPop(out string pageId))
        {
            if (!visited.Add(pageId)) continue;
            foreach (HbrStoryChoice choice in GetPage(pageId).Choices) pending.Push(choice.NextPage);
        }
        Require(visited.Count == Pages.Count, source,
            $"存在无法从起始页到达的剧情页：{string.Join("、", Pages.Where(page => !visited.Contains(page.Id)).Select(page => page.Id))}。");

        HashSet<string> canFinish = Pages.Where(page => page.Choices.Count == 0).Select(page => page.Id).ToHashSet(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (HbrStoryPage page in Pages)
            {
                if (page.Choices.Any(choice => canFinish.Contains(choice.NextPage))) changed |= canFinish.Add(page.Id);
            }
        } while (changed);
        Require(canFinish.Count == Pages.Count, source, "每个剧情页都必须有一条路径可以抵达无选项的结尾页。");
    }

    private static void RejectDuplicateProperties(JsonElement element, string path, string source)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), source, $"{path} 出现重复属性 {property.Name}。");
                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}", source);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement child in element.EnumerateArray()) RejectDuplicateProperties(child, $"{path}[{index++}]", source);
        }
    }

    private static void CheckIdentifier(string value, string source, string field) =>
        Require(value != null && Identifier.IsMatch(value), source,
            $"{field} 必须以大写字母开头，仅包含大写字母、数字、下划线（最多 80 字符）。");

    private static void CheckUnique(IEnumerable<string> values, string source, string field)
    {
        string[] entries = values.ToArray();
        Require(entries.Distinct(StringComparer.Ordinal).Count() == entries.Length, source, $"{field} 不可重复。");
    }

    private static void CheckPortraitLayout(HbrPortraitLayout layout, string source, string field, bool optional)
    {
        if (optional && layout == null) return;
        Require(layout != null, source, $"{field} 不能为 null。");
        CheckNumber(layout.X, -1600, 3200, source, field + ".x");
        CheckNumber(layout.Y, -900, 1800, source, field + ".y");
        CheckNumber(layout.Size, 40, 700, source, field + ".size");
        CheckNumber(layout.ImageScale, 0.1f, 8, source, field + ".imageScale");
        CheckNumber(layout.ImageOffsetX, -1600, 1600, source, field + ".imageOffsetX");
        CheckNumber(layout.ImageOffsetY, -1600, 1600, source, field + ".imageOffsetY");
    }

    private static void CheckCharacters(IReadOnlyList<HbrStoryCharacter> characters, string source, string field, bool optional)
    {
        if (optional && characters == null) return;
        Require(characters != null, source, $"{field} 必须为人物数组，不能为 null。");
        Require(characters.Count <= MaximumCharactersPerScene, source, $"{field} 最多支持 {MaximumCharactersPerScene} 位人物。");
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < characters.Count; index++)
        {
            HbrStoryCharacter character = characters[index];
            string characterField = $"{field}[{index}]";
            Require(character != null, source, $"{characterField} 不能为 null。");
            CheckIdentifier(character.Id, source, characterField + ".id");
            Require(ids.Add(character.Id), source, $"{field} 有重复人物编号：{character.Id}。");
            CheckImagePath(character.Image, source, characterField + ".image", optional: false);
            CheckNumber(character.X, -1600, 3200, source, characterField + ".x");
            CheckNumber(character.Y, -900, 1800, source, characterField + ".y");
            CheckNumber(character.Height, 40, 2400, source, characterField + ".height");
            CheckNumber(character.Opacity, 0, 1, source, characterField + ".opacity");
        }
    }

    private static void CheckNumber(float value, float minimum, float maximum, string source, string field) =>
        Require(float.IsFinite(value) && value >= minimum && value <= maximum, source,
            $"{field} 必须为 {minimum}–{maximum} 之间的有限数值。");

    private static void CheckImagePath(string path, string source, string field, bool optional)
    {
        if (optional && string.IsNullOrEmpty(path)) return;
        Require(path != null && path.StartsWith("res://images/", StringComparison.Ordinal)
            && !path.Contains('\\') && !path[6..].Contains(':') && !path.Any(char.IsControl)
            && path[6..].Split('/').All(part => part.Length > 0 && part != "." && part != ".."),
            source, $"{field} 必须是 images 目录内的 res:// 图片路径，不能包含相对路径跳转。");
        string extension = System.IO.Path.GetExtension(path);
        Require(new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" }.Contains(extension, StringComparer.OrdinalIgnoreCase),
            source, $"{field} 仅支持 png、jpg、jpeg、webp、svg 图片。");
    }

    private static void Require(bool condition, string source, string message)
    {
        if (!condition) throw Invalid(source, message);
    }

    private static InvalidOperationException Invalid(string source, string message, Exception inner = null) =>
        new($"HBR 剧情事件 [{source}]：{message}", inner);
}

public sealed class HbrStoryConditions
{
    public required IReadOnlyList<string> PartyCharacters { get; init; }
    public required IReadOnlyList<int> Acts { get; init; }
    public required bool OncePerRun { get; init; }
    public required IReadOnlyList<string> AfterEventIds { get; init; }

    public bool IsEligible(IEnumerable<string> partyCharacters, int actNumber,
        IEnumerable<string> visitedEventIds, string eventId, bool allowReplay = false)
    {
        HashSet<string> visited = visitedEventIds.Select(HbrStoryDefinition.NormalizeEventId)
            .ToHashSet(StringComparer.Ordinal);
        return (PartyCharacters.Count == 0 || partyCharacters.Any(character => PartyCharacters.Contains(character, StringComparer.Ordinal)))
            && (Acts.Count == 0 || Acts.Contains(actNumber))
            && (allowReplay || !OncePerRun || !visited.Contains(HbrStoryDefinition.NormalizeEventId(eventId)))
            && AfterEventIds.All(prerequisite => visited.Contains(HbrStoryDefinition.NormalizeEventId(prerequisite)));
    }
}

public sealed class HbrStoryPresentation
{
    public required string Background { get; init; }
    /// <summary>Older stories retain their existing layout unless the original-game style is selected.</summary>
    public string DialogueStyle { get; init; } = "classic";
    public string ChoicePortrait { get; init; } = string.Empty;
    public string ThoughtPortrait { get; init; } = string.Empty;
    public HbrPortraitLayout PortraitLayout { get; init; } = new();
}

public sealed class HbrStoryPage
{
    public required string Id { get; init; }
    /// <summary>Optional editor label; missing/null/empty uses automatic naming. Story transitions still use Id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Title { get; init; }
    public string ChoicePrompt { get; init; } = string.Empty;
    public string ChoiceMode { get; init; } = "list";
    public IReadOnlyList<string> ChoiceLeft { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ChoiceRight { get; init; } = Array.Empty<string>();
    public string Background { get; init; } = string.Empty;
    public required IReadOnlyList<HbrStoryLine> Lines { get; init; }
    public required IReadOnlyList<HbrStoryChoice> Choices { get; init; }
    public IReadOnlyList<HbrStoryCharacter> Characters { get; init; } = Array.Empty<HbrStoryCharacter>();
    /// <summary>Explicitly encountered characters in this branch; only memory-fragment events record these per-run unlocks.</summary>
    public IReadOnlyList<string> MetCharacters { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Resolve the background at an original line index. Missing/null/empty backgrounds inherit
    /// the preceding line, then this page, then the presentation. -1 means the page default.
    /// Each page resolves independently, so back navigation does not retain a later background.
    /// </summary>
    public string ResolveBackground(HbrStoryPresentation presentation, int lineIndex)
    {
        if (lineIndex < -1 || lineIndex >= Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex), "台词索引必须为 -1（章节默认）或本章中的台词索引。");
        for (int index = lineIndex; index >= 0; index--)
        {
            if (!string.IsNullOrEmpty(Lines[index].Background)) return Lines[index].Background;
        }
        return string.IsNullOrEmpty(Background) ? presentation.Background : Background;
    }

    /// <summary>
    /// Resolve this page's character composition at an original line index. Omitted/null
    /// line lists inherit the nearest preceding explicit list; an empty list clears it.
    /// -1 means the page default. Each page resolves independently, including when reading backwards.
    /// </summary>
    public IReadOnlyList<HbrStoryCharacter> ResolveCharacters(int lineIndex)
    {
        if (lineIndex < -1 || lineIndex >= Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex), "台词索引必须为 -1（章节默认）或本章中的台词索引。");
        for (int index = lineIndex; index >= 0; index--)
        {
            if (Lines[index].Characters != null) return Lines[index].Characters;
        }
        return Characters;
    }
}

public sealed class HbrStoryLine
{
    public required string Kind { get; init; }
    public string Speaker { get; init; } = string.Empty;
    public required string Text { get; init; }
    public string Portrait { get; init; } = string.Empty;
    public string Background { get; init; } = string.Empty;
    public HbrPortraitLayout PortraitLayout { get; init; }
    public IReadOnlyList<HbrStoryCharacter> Characters { get; init; }

    /// <summary>Portrait geometry only overrides the current line, never subsequent lines.</summary>
    public HbrPortraitLayout ResolvePortraitLayout(HbrStoryPresentation presentation) =>
        PortraitLayout ?? presentation.PortraitLayout;
}

/// <summary>Geometry in a 1600 × 900 design canvas. X/Y locate the circular frame's top-left corner.</summary>
public sealed class HbrPortraitLayout
{
    public float X { get; init; } = 46;
    public float Y { get; init; } = 672;
    public float Size { get; init; } = 184;
    public float ImageScale { get; init; } = 1;
    public float ImageOffsetX { get; init; }
    public float ImageOffsetY { get; init; }
}

/// <summary>A character image anchored at its bottom-center. Later list entries appear in front.</summary>
public sealed class HbrStoryCharacter
{
    public required string Id { get; init; }
    public required string Image { get; init; }
    public float X { get; init; } = 800;
    public float Y { get; init; } = 900;
    public float Height { get; init; } = 820;
    public bool FlipX { get; init; }
    public float Opacity { get; init; } = 1;
}

public sealed class HbrStoryChoice
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string NextPage { get; init; }
    public string Combat { get; init; } = string.Empty;
    public HbrStoryRewards Rewards { get; init; } = new();
}

public sealed class HbrStoryRewards
{
    public string Sense { get; init; } = string.Empty;
    public int SenseAmount { get; init; }
    public int CardChoices { get; init; }
    public string CardRarity { get; init; } = "default";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Potions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CardRemovals { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CardUpgrades { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HealFull { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MaxDp { get; init; }
}

/// <summary>Persisted on the encounter because event rooms save their model ID, not event instance fields.</summary>
public sealed class HbrStoryCombatState
{
    public string EventId { get; init; } = string.Empty;
    public string NextPage { get; init; } = string.Empty;
    public IReadOnlyList<string> ClaimedChoices { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MemoryEncounteredCharacters { get; init; } = Array.Empty<string>();

    public void ValidateFor(HbrStoryDefinition definition)
    {
        if (EventId != definition.Id || !definition.Pages.Any(page => page.Id == NextPage) || ClaimedChoices == null)
            throw new InvalidOperationException("剧情战斗续页与当前事件不一致。");
        var validChoices = definition.Pages.SelectMany(page => page.Choices.Select(choice => page.Id + "/" + choice.Id)).ToHashSet(StringComparer.Ordinal);
        if (ClaimedChoices.Count > validChoices.Count || ClaimedChoices.Any(key => key == null || !validChoices.Contains(key))
            || ClaimedChoices.Distinct(StringComparer.Ordinal).Count() != ClaimedChoices.Count)
            throw new InvalidOperationException("剧情战斗存档包含无效的已领奖选项。");
        var validCharacters = definition.Pages.SelectMany(page => page.MetCharacters).ToHashSet(StringComparer.Ordinal);
        if (MemoryEncounteredCharacters == null || MemoryEncounteredCharacters.Any(id => id == null || !validCharacters.Contains(id)) ||
            MemoryEncounteredCharacters.Distinct(StringComparer.Ordinal).Count() != MemoryEncounteredCharacters.Count)
            throw new InvalidOperationException("剧情战斗存档包含无效的遇见角色。");
    }

    public string Serialize() => JsonSerializer.Serialize(this);
    public static HbrStoryCombatState Deserialize(string json) =>
        JsonSerializer.Deserialize<HbrStoryCombatState>(json) ?? throw new InvalidOperationException("剧情战斗存档为空。");
}
