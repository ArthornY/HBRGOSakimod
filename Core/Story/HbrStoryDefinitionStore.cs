using System;
using System.Collections.Concurrent;
using Godot;

namespace HBRGOSakiMod.Core.Story;

/// <summary>Packaged story inputs are parsed once; external editor changes require a new build/package.</summary>
internal static class HbrStoryDefinitionStore
{
    private static readonly ConcurrentDictionary<string, HbrStoryDefinition> Definitions = new(StringComparer.Ordinal);

    internal static HbrStoryDefinition Load(string path) => Definitions.GetOrAdd(path, Read);

    private static HbrStoryDefinition Read(string path)
    {
        if (path == null || !path.StartsWith("res://story/events/", StringComparison.Ordinal)
            || !path.EndsWith(".json", StringComparison.Ordinal) || path.Contains("..") || path.Contains('\\'))
        {
            throw new InvalidOperationException($"HBR 剧情事件定义路径无效：{path}。");
        }

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new InvalidOperationException($"无法读取 HBR 剧情事件定义 {path}：{FileAccess.GetOpenError()}。请确认 JSON 已包含在模组资源中。");
        }
        return HbrStoryDefinition.Parse(file.GetAsText(), path);
    }
}
