using Automaton.IPC;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using ECommons;
using ECommons.Automation;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace Automaton.Features;

public class GettingTooAttachedConfiguration
{
    [IntConfig(DefaultValue = 10000)]
    public int NumberOfLoops = 10000;
}

[Tweak]
public unsafe class GettingTooAttached : Tweak<GettingTooAttachedConfiguration>
{
    public override string Name => "Getting Too Attached";
    public override string Description => "Loop through attaching and removing materia for the Getting Too Attached achievement. Feature currently under UI renovations, please use the button.";

    private readonly float height;

    internal bool active = false;

    public override void Enable()
    {
        Svc.Toasts.ErrorToast += CheckForErrors;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MateriaAttachDialog", ConfirmMateriaDialog);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MateriaRetrieveDialog", ConfirmRetrievalDialog);
    }

    public override void Disable()
    {
        Svc.Toasts.ErrorToast -= CheckForErrors;
        Svc.AddonLifecycle.UnregisterListener(ConfirmMateriaDialog);
        Svc.AddonLifecycle.UnregisterListener(ConfirmRetrievalDialog);
    }

    public override void DrawConfig()
    {
        ImGui.DragInt("Number of Loops", ref Config.NumberOfLoops);
        if (ImGui.Button(!active ? $"Start Looping###StartLooping" : $"Looping. Click to abort.###AbortLoop"))
        {
            if (!active)
            {
                active = true;
                TaskManager.Enqueue(YesAlready.DisableIfNeeded);
                TaskManager.Enqueue(TryGettingTooAttached);
            }
            else
            {
                CancelLoop();
            }
        }
    }

    //public override void Draw()
    //{
    //    if (TryGetAddonByName<AtkUnitBase>("MateriaAttach", out var addon))
    //    {
    //        if (!(addon->UldManager.NodeListCount > 1)) return;
    //        if (!addon->UldManager.NodeList[1]->IsVisible) return;

    //        var node = addon->UldManager.NodeList[1];

    //        if (!node->IsVisible)
    //            return;

    //        var position = AtkResNodeHelper.GetNodePosition(node);

    //        ImGuiHelpers.ForceNextWindowMainViewport();
    //        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(addon->X, addon->Y - height));

    //        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(7f, 7f));
    //        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(10f, 10f));
    //        ImGui.Begin($"###LoopMelding{node->NodeID}", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNavFocus
    //        | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

    //        if (ImGui.Button(!active ? $"Getting Too Attached###StartLooping" : $"Looping. Click to abort.###AbortLoop"))
    //        {
    //            if (!active)
    //            {
    //                active = true;
    //                TaskManager.Enqueue(YesAlready.DisableIfNeeded);
    //                TaskManager.Enqueue(TryGettingTooAttached);
    //            }
    //            else
    //            {
    //                CancelLoop();
    //            }
    //        }

    //        ImGui.SameLine();
    //        ImGui.PushItemWidth(150);
    //        if (ImGui.SliderInt("Loops", ref Config.NumberOfLoops, 0, 10000))
    //            EzConfig.Save();

    //        height = ImGui.GetWindowSize().Y;

    //        ImGui.End();
    //        ImGui.PopStyleVar(2);
    //    }
    //}

    private void CancelLoop()
    {
        active = false;
        TaskManager.Abort();
        TaskManager.Enqueue(YesAlready.EnableIfNeeded);
    }

    private void CheckForErrors(ref SeString message, ref bool isHandled)
    {
        var msg = message.ExtractText();
        if (new[] { 7701, 7707 }.Any(x => msg == FindRow<LogMessage>(y => y.RowId == x)?.Text.ExtractText()))
        {
            ModuleMessage("Error while melding. Aborting Tasks.");
            CancelLoop();
        }
    }

    private static bool IsBusy() => Svc.Condition[ConditionFlag.MeldingMateria] || Svc.Condition[ConditionFlag.Occupied39] || !Svc.Condition[ConditionFlag.NormalConditions];

    private void TryGettingTooAttached()
    {
        if (Config.NumberOfLoops > 0)
        {
            TaskManager.Enqueue(SelectItem, "Selecting Item");
            TaskManager.Enqueue(SelectMateria, "Selecting Materia");
            TaskManager.Enqueue(SelectItem, "Selecting Item");
            TaskManager.Enqueue(ActivateContextMenu, "Opening Context Menu");
            TaskManager.Enqueue(RetrieveMateriaContextMenu, "Activating Retrieve Materia Context Entry");
            TaskManager.Enqueue(() => Config.NumberOfLoops -= 1);
            TaskManager.Enqueue(TryGettingTooAttached, "Repeat Loop");
        }
        else
        {
            TaskManager.Enqueue(() => active = false);
            TaskManager.Enqueue(YesAlready.EnableIfNeeded);
        }
    }

    private unsafe bool? SelectItem()
    {
        if (TryGetAddonByName<AtkUnitBase>("MateriaAttach", out var addon) && !IsBusy() && !AreDialogsOpen())
        {
            if (addon->UldManager.NodeList[16]->IsVisible())
            {
                CancelLoop();
                ModuleMessage("Unable to continue. No gear in inventory");
                return false;
            }

            Callback.Fire(addon, false, 1, 0, 1, 0); // second value is what position in the list the item is
            return addon->AtkValues[287].Int != -1; // condition for something being selected
        }
        return false;
    }

    public static bool AreDialogsOpen() => Svc.GameGui.GetAddonByName("MateriaAttachDialog") != nint.Zero && Svc.GameGui.GetAddonByName("MateriaRetrieveDialog") != nint.Zero;

    public unsafe bool? SelectMateria()
    {
        if (TryGetAddonByName<AtkUnitBase>("MateriaAttach", out var addon) && !AreDialogsOpen())
        {
            if (addon->UldManager.NodeList[6]->IsVisible())
            {
                CancelLoop();
                ModuleMessage("Unable to continue. No materia to meld.");
                return false;
            }
            else if (MemoryHelper.ReadSeStringNullTerminated(new nint(addon->AtkValues[289].String)).ToString().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[2] == "0")
            {
                CancelLoop();
                ModuleMessage("Unable to continue. First listed materia has too high ilvl requirements.");
                return false;
            }

            Callback.Fire(addon, false, 2, 0, 1, 0); // second value is which materia in the list
            return TryGetAddonByName<AtkUnitBase>("MateriaAttachDialog", out var attachDialog) && attachDialog->IsVisible && Svc.Condition[ConditionFlag.MeldingMateria];
        }
        return false;
    }

    public void ConfirmMateriaDialog(AddonEvent type, AddonArgs args)
    {
        if (!active) return;
        var addon = (AtkUnitBase*)args.Addon;
        if (addon->AtkValues[50].Type != 0)
        {
            CancelLoop();
            ModuleMessage("Unable to continue. This gear requires overmelding.");
            return;
        }

        TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.MeldingMateria]);
        TaskManager.Enqueue(() => Callback.Fire(addon, true, 0, 0, 0));
    }

    public unsafe bool ActivateContextMenu()
    {
        if (TryGetAddonByName<AtkUnitBase>("MateriaAttach", out var addon) && !Svc.Condition[ConditionFlag.MeldingMateria])
        {
            Callback.Fire(addon, false, 4, 0, 1, 0);

            return TryGetAddonByName<AtkUnitBase>("ContextMenu", out var contextMenu) && contextMenu->IsVisible;
        }
        return false;
    }

    private static bool RetrieveMateriaContextMenu()
    {
        if (!Svc.Condition[ConditionFlag.Occupied39])
            Callback.Fire((AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu"), true, 0, 1, 0u, 0, 0);
        return !Svc.Condition[ConditionFlag.Occupied39];
    }

    public void ConfirmRetrievalDialog(AddonEvent type, AddonArgs args)
    {
        if (!active) return;
        new AddonMaster.MateriaRetrieveDialog(args.Addon).Begin();
    }
}
