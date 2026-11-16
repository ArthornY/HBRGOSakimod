using System;

namespace HBRGOSakiMod.Core.RunUpload;

// Everything in Common/ is plain .NET: no Godot or game types, so the offline
// checks in validation/run-upload compile these files unchanged.
internal static class RunUploadConstants
{
    internal const string Schema = "hbrgo-run/1";

    // The subdomain still waits for the user's decision. Only this base
    // changes when it is settled (handoffs/run-upload-20261108/endpoint.md).
    internal const string EndpointBase = "https://runs.hbrgo.com";
    internal const string EndpointPath = "/api/v1/runs";
    internal const string PrivacyPageUrl = "https://hbrgo.com/run-data-privacy";

    internal static Uri EndpointUri => new(EndpointBase + EndpointPath);

    internal static string EndpointHost => EndpointUri.Host;

    // The version of the first-launch prompt and its consent. Raise it with a
    // schema major version (hbrgo-run/2) or whenever the collection scope
    // grows: every player is asked again, and an older consent stops counting.
    internal const int ConsentVersion = 1;
    internal const int RecorderVersion = 1;

    // Died or abandoned on floor 1..5 (the number the top bar shows, the
    // ancient being floor 1): not uploaded. Confirmed by the user, 2026-11-08.
    internal const int EarlyEndFloors = 5;

    // Modifier mods: runs played with one loaded are never uploaded. Short
    // label for run.integrity.modifiers → manifest ids (compared case-insensitively).
    internal static readonly (string Label, string[] ModIds)[] KnownModifiers =
    [
        ("loadout", ["Loadout"]),
        ("free_loadout", ["FreeLoadout"]),
        ("god_mod", ["sts2_god_mod"])
    ];

    // This mod and its dependencies never count as "other gameplay mods".
    internal static readonly string[] OwnModIds = ["HBRGOSakiMod", "BaseLib", "STS2-RitsuLib"];

    // Developer console commands of the game that never change a run, by the
    // name DevConsole looks up (compared case-insensitively). Each was read in
    // the game's source; the same in v0.107.1 and v0.111.0:
    //   achievement  unlocks, revokes or checks achievements; no run code reads them
    //   art          lists the content that has no art yet
    //   bestiary     opens the bestiary from the main menu
    //   dump         writes the model id table to the log
    //   getlogs      zips the logs and (read only) the save files into a bug report
    //   help         lists the commands; DevConsole answers it itself
    //   instant      switches the animation speed setting between Fast and Instant
    //   leaderboard  uploads test scores to a platform leaderboard
    //   log          sets a log level
    //   open         opens a folder in the file manager
    //   sentry       tests error reporting; "crash confirm" ends the game like closing it
    //   trailer      recording hotkeys: hide parts of the interface, change the time scale
    // Every other name counts as changing the run: the game's other commands,
    // this mod's own (hbrgosakimod), other mods' commands and unknown names.
    // Not listed on purpose: unlock (marks content as seen in the progress
    // save, which changes what this run records as newly discovered), cloud
    // (deletes the Steam Cloud saves, then quits) and multiplayer (its test
    // scene replaces the run and can start a debug run).
    internal static readonly string[] HarmlessConsoleCommands =
        ["achievement", "art", "bestiary", "dump", "getlogs", "help", "instant", "leaderboard", "log", "open", "sentry", "trailer"];

    internal const int MaxQueuedRuns = 50;
    internal static readonly TimeSpan MaxQueueAge = TimeSpan.FromDays(30);
    internal const long MaxCompressedBytes = 4L * 1024 * 1024;
    internal const long MaxUncompressedBytes = 16L * 1024 * 1024;

    internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan FirstRetryDelay = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(6);

    internal const int MaxCombatEvents = 20000;
    internal const int MaxTextLength = 200;

    // What the v1 recorder fills (schema-v1.md 2.1).
    internal static readonly string[] RecordedFeatures = ["map", "floors", "combat", "hbr", "names", "integrity"];
}
