using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NChooseACardSelectionScreen), nameof(NChooseACardSelectionScreen._Ready))]
internal static class ChihayaChooseCardNumberKeyPatch
{
    private const string ListenerName = "HBRGOChihayaChooseCardNumberKeyListener";

    private static readonly FieldInfo CardsField = AccessTools.Field(
        typeof(NChooseACardSelectionScreen),
        "_cards");

    private static void Postfix(NChooseACardSelectionScreen __instance)
    {
        if (!IsChihayaChoiceScreen(__instance) ||
            __instance.GetNodeOrNull<ChihayaChooseCardNumberKeyListener>(ListenerName) != null)
        {
            return;
        }

        __instance.AddChild(new ChihayaChooseCardNumberKeyListener
        {
            Name = ListenerName,
            Screen = __instance
        });
    }

    internal static IReadOnlyList<CardModel> GetCards(NChooseACardSelectionScreen screen)
    {
        object cards = CardsField.GetValue(screen);
        if (cards is IReadOnlyList<CardModel> cardList)
        {
            return cardList;
        }

        if (cards is IEnumerable<CardModel> cardEnumerable)
        {
            return cardEnumerable.ToArray();
        }

        return Array.Empty<CardModel>();
    }

    private static bool IsChihayaChoiceScreen(NChooseACardSelectionScreen screen)
    {
        IReadOnlyList<CardModel> cards = GetCards(screen);
        return cards.Count == 2 &&
               cards[0] is ReptureBowChoiceHbrgoSaki &&
               cards[1] is ReptureDualSwordsChoiceHbrgoSaki;
    }
}

internal sealed partial class ChihayaChooseCardNumberKeyListener : Node
{
    private static readonly MethodInfo SelectHolderMethod = AccessTools.Method(
        typeof(NChooseACardSelectionScreen),
        "SelectHolder");

    internal NChooseACardSelectionScreen Screen { get; set; }

    public override void _Ready()
    {
        SetProcessInput(true);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey keyEvent || !IsScreenAvailable())
        {
            return;
        }

        int selectedIndex = GetSelectedIndex(keyEvent);
        if (selectedIndex < 0)
        {
            return;
        }

        GetViewport().SetInputAsHandled();

        if (!keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        BlockNextRelease(keyEvent);
        TrySelectHolder(selectedIndex);
    }

    private bool TrySelectHolder(int selectedIndex)
    {
        IReadOnlyList<CardModel> cards = ChihayaChooseCardNumberKeyPatch.GetCards(Screen);
        if (selectedIndex >= cards.Count)
        {
            return false;
        }

        NCardHolder holder = GetCardHolders(Screen)
            .FirstOrDefault(cardHolder => ReferenceEquals(cardHolder.CardModel, cards[selectedIndex]));
        if (holder == null)
        {
            return false;
        }

        SelectHolderMethod.Invoke(Screen, new object[] { holder });
        return true;
    }

    private bool IsScreenAvailable()
    {
        return Screen != null && GodotObject.IsInstanceValid(Screen);
    }

    private void BlockNextRelease(InputEventKey keyEvent)
    {
        Window root = GetTree()?.Root;
        if (root == null)
        {
            return;
        }

        const string blockerName = "HBRGOChihayaChooseCardNumberKeyReleaseBlocker";
        root.GetNodeOrNull<ChihayaChooseCardNumberKeyReleaseBlocker>(blockerName)?.QueueFree();
        root.AddChild(new ChihayaChooseCardNumberKeyReleaseBlocker
        {
            Name = blockerName,
            Keycode = keyEvent.Keycode,
            PhysicalKeycode = keyEvent.PhysicalKeycode
        });
    }

    private static int GetSelectedIndex(InputEventKey keyEvent)
    {
        if (IsKey(keyEvent, Key.Key1, Key.Kp1))
        {
            return 0;
        }

        if (IsKey(keyEvent, Key.Key2, Key.Kp2))
        {
            return 1;
        }

        return -1;
    }

    private static bool IsKey(InputEventKey keyEvent, Key key, Key keypadKey)
    {
        return keyEvent.Keycode == key ||
               keyEvent.PhysicalKeycode == key ||
               keyEvent.Keycode == keypadKey ||
               keyEvent.PhysicalKeycode == keypadKey;
    }

    private static IEnumerable<NCardHolder> GetCardHolders(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is NCardHolder holder)
            {
                yield return holder;
            }

            foreach (NCardHolder descendant in GetCardHolders(child))
            {
                yield return descendant;
            }
        }
    }
}

internal sealed partial class ChihayaChooseCardNumberKeyReleaseBlocker : Node
{
    private const double LifetimeSeconds = 1.0;

    private double _lifetime;

    internal Key Keycode { get; set; }

    internal Key PhysicalKeycode { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: false } keyEvent || !Matches(keyEvent))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        QueueFree();
    }

    public override void _Process(double delta)
    {
        _lifetime += delta;
        if (_lifetime >= LifetimeSeconds)
        {
            QueueFree();
        }
    }

    private bool Matches(InputEventKey keyEvent)
    {
        return (Keycode != Key.None &&
                (keyEvent.Keycode == Keycode || keyEvent.PhysicalKeycode == Keycode)) ||
               (PhysicalKeycode != Key.None &&
                (keyEvent.Keycode == PhysicalKeycode || keyEvent.PhysicalKeycode == PhysicalKeycode));
    }
}
