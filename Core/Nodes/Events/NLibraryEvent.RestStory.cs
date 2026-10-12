using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Helpers;

namespace HBRGOSakiMod.Core.Nodes.Events;

public partial class NLibraryEvent
{
    internal bool IsInitializedForRestStory => _initialized && IsInsideTree()
        && _event != null && (_pages.Count > 0 || _showingChoices);

    private bool TryProceedFromRestStory()
    {
        if (!HbrMemoryGardenRestStoryPresentation.TryGetReturnTask(_event, out Task returnTask)) return false;
        TaskHelper.RunSafely(AwaitRestStoryReturn(returnTask));
        return true;
    }

    private async Task AwaitRestStoryReturn(Task returnTask)
    {
        try { await returnTask; }
        finally
        {
            // A successful return removes this node. If restoring the fire failed,
            // unlock its completed reader so the player can retry without rewards.
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion())
            {
                _dispatchPending = false;
                foreach (Button button in _choiceButtons) button.Disabled = false;
                ConfigureFocusNavigation();
            }
        }
    }
}
