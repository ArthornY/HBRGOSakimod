param(
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$modRoot = Split-Path -Parent $PSScriptRoot
$scratchDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('hbr-story-background-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratchDirectory | Out-Null
$definitionPath = [System.Security.SecurityElement]::Escape((Join-Path $modRoot 'Core\Story\HbrStoryDefinition.cs'))
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="$definitionPath" />
  </ItemGroup>
</Project>
"@
$program = @'
using HBRGOSakiMod.Core.Story;
using System.Text.Json.Nodes;

int checks = 0;
void Equal(string actual, string expected, string name)
{
    if (actual != expected) throw new Exception($"{name}: expected {expected}, got {actual}");
    checks++;
}
void Reject(Action action, string name)
{
    try { action(); }
    catch (InvalidOperationException) { checks++; return; }
    throw new Exception($"{name}: invalid story accepted");
}
void BadIndex(Action action, string name)
{
    try { action(); }
    catch (ArgumentOutOfRangeException) { checks++; return; }
    throw new Exception($"{name}: invalid line index accepted");
}

const string global = "res://images/global.png";
const string pageBackground = "res://images/page.png";
const string firstChange = "res://images/first.png";
const string lastChange = "res://images/last.png";
string json = """
{
  "schemaVersion": 1,
  "id": "BACKGROUND_TEST",
  "title": "背景测试",
  "conditions": { "partyCharacters": [], "acts": [], "oncePerRun": false, "afterEventIds": [] },
  "presentation": { "background": "res://images/global.png" },
  "startPage": "START",
  "pages": [{
    "id": "START",
    "lines": [
      { "kind": "dialogue", "speaker": "A", "text": "one" },
      { "kind": "dialogue", "speaker": "B", "text": "two" },
      { "kind": "dialogue", "speaker": "A", "text": "three" },
      { "kind": "dialogue", "speaker": "B", "text": "four" }
    ],
    "choices": []
  }]
}
""";
HbrStoryDefinition legacy = HbrStoryDefinition.Parse(json);
Equal(legacy.Presentation.DialogueStyle, "classic", "legacy dialogue style");
JsonObject styleInput = JsonNode.Parse(json)!.AsObject();
foreach (string style in new[] { "original", "classic" })
{
    styleInput["presentation"]!["dialogueStyle"] = style;
    Equal(HbrStoryDefinition.Parse(styleInput.ToJsonString()).Presentation.DialogueStyle, style,
        $"accepted dialogue style {style}");
}
foreach (string style in new[] { "", "Original", "unknown", " original " })
{
    styleInput["presentation"]!["dialogueStyle"] = style;
    Reject(() => HbrStoryDefinition.Parse(styleInput.ToJsonString()), $"invalid dialogue style {style}");
}
styleInput["presentation"]!["dialogueStyle"] = null;
Reject(() => HbrStoryDefinition.Parse(styleInput.ToJsonString()), "null dialogue style");
HbrStoryPage legacyPage = legacy.GetPage("START");
for (int index = -1; index < legacyPage.Lines.Count; index++)
    Equal(legacyPage.ResolveBackground(legacy.Presentation, index), global, $"legacy line {index}");

JsonObject edited = JsonNode.Parse(json)!.AsObject();
JsonObject pageNode = edited["pages"]![0]!.AsObject();
pageNode["background"] = pageBackground;
pageNode["lines"]![1]!["background"] = firstChange;
pageNode["lines"]![2]!["background"] = "";
pageNode["lines"]![3]!["background"] = lastChange;
HbrStoryDefinition definition = HbrStoryDefinition.Parse(edited.ToJsonString());
HbrStoryPage page = definition.GetPage("START");
Equal(page.ResolveBackground(definition.Presentation, -1), pageBackground, "page default");
Equal(page.ResolveBackground(definition.Presentation, 0), pageBackground, "first line inherits page");
Equal(page.ResolveBackground(definition.Presentation, 1), firstChange, "first change");
Equal(page.ResolveBackground(definition.Presentation, 2), firstChange, "empty line inherits previous");
Equal(page.ResolveBackground(definition.Presentation, 3), lastChange, "choice background uses last line");
Equal(page.ResolveBackground(definition.Presentation, 0), pageBackground, "back navigation restores page");
Equal(legacyPage.ResolveBackground(definition.Presentation, 0), global, "new page resets previous scene");
BadIndex(() => page.ResolveBackground(definition.Presentation, -2), "negative index");
BadIndex(() => page.ResolveBackground(definition.Presentation, 4), "past last line");

pageNode["background"] = null;
pageNode["lines"]![2]!["background"] = null;
definition = HbrStoryDefinition.Parse(edited.ToJsonString());
page = definition.GetPage("START");
Equal(page.ResolveBackground(definition.Presentation, 0), global, "null page inherits presentation");
Equal(page.ResolveBackground(definition.Presentation, 2), firstChange, "null line inherits previous");
pageNode["background"] = "res://images/../bad.png";
Reject(() => HbrStoryDefinition.Parse(edited.ToJsonString()), "page path traversal");
pageNode["background"] = "";
pageNode["lines"]![1]!["background"] = "C:/bad.png";
Reject(() => HbrStoryDefinition.Parse(edited.ToJsonString()), "external line path");
pageNode["lines"]![1]!["background"] = " ";
Reject(() => HbrStoryDefinition.Parse(edited.ToJsonString()), "whitespace line path");

pageNode["background"] = pageBackground;
pageNode["lines"] = new JsonArray();
definition = HbrStoryDefinition.Parse(edited.ToJsonString());
page = definition.GetPage("START");
Equal(page.ResolveBackground(definition.Presentation, page.Lines.Count - 1), pageBackground,
    "empty page choice inherits page background");

HbrStoryDefinition existing = HbrStoryDefinition.Parse(File.ReadAllText(args[0]), args[0]);
Equal(existing.Presentation.DialogueStyle, "classic", "packaged story keeps classic layout");
foreach (HbrStoryPage existingPage in existing.Pages)
    for (int index = -1; index < existingPage.Lines.Count; index++)
        Equal(existingPage.ResolveBackground(existing.Presentation, index), existing.Presentation.Background,
            $"packaged story compatibility {existingPage.Id}/{index}");
Console.WriteLine($"PASS: {checks} story background checks.");
'@
try {
    Set-Content -LiteralPath (Join-Path $scratchDirectory 'StoryBackgroundTests.csproj') -Value $project -Encoding utf8
    Set-Content -LiteralPath (Join-Path $scratchDirectory 'Program.cs') -Value $program -Encoding utf8
    & $Dotnet run --project (Join-Path $scratchDirectory 'StoryBackgroundTests.csproj') -- (Join-Path $modRoot 'story\events\library_hbrgo_saki.json')
    if ($LASTEXITCODE -ne 0) { throw "Story background checks failed with exit code $LASTEXITCODE." }
}
finally {
    $resolvedScratch = [System.IO.Path]::GetFullPath($scratchDirectory)
    $resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolvedScratch.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean test directory outside the temporary root: $resolvedScratch"
    }
    Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
}
