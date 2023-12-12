using System;
using System.ComponentModel;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Tweaks.UiAdjustment;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using static SimpleTweaksPlugin.Tweaks.UiAdjustment.TargetHP;

namespace SimpleTweaksPlugin
{
    public partial class UiAdjustmentsConfig
    {
        public PreciseHpPercent.Configs PreciseHpPercent = null;
    }
}

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment
{
    public unsafe class PreciseHpPercent : UiAdjustments.SubTweak
    {
        [PluginService] private static IGameInteropProvider GameInteropProvider { get; set; } = null!;

        public class Configs : TweakConfig
        {
            [TweakConfigOption("Decimal precision", "precision")]
            public int DecimalPrecision = 1;
            public RoundingMethod roundingMethod = RoundingMethod.ToZero;
        }

        protected float exampleValue = 49.598f;

        public enum RoundingMethod : int
        {
            [Description("Closest")]
            AwayFromZero = 1,
            [Description("Floor")]
            ToZero = 2,
            [Description("Ceiling")]
            ToPositiveInfinity = 4
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputInt("Decimal Places##decimalPrecision", ref Config.DecimalPrecision))
            {
                if (Config.DecimalPrecision <= 0)
                {
                    Config.DecimalPrecision = 1;
                    return;
                }
                if (Config.DecimalPrecision > 15)
                {
                    Config.DecimalPrecision = 15;
                    return;
                }

                hasChanged = true;
            }
            ImGui.SetNextItemWidth(250);
            if (ImGui.BeginCombo("Display Format###targetHpFormat", $"{Config.roundingMethod.GetDescription()} ({exampleValue} → {Math.Round(exampleValue, 2, (MidpointRounding)Config.roundingMethod)})"))
            {
                foreach (var v in (RoundingMethod[])Enum.GetValues(typeof(RoundingMethod)))
                {
                    if (!ImGui.Selectable($"{v.GetDescription()} ({exampleValue} → {Math.Round(exampleValue, 2, (MidpointRounding)v)})##hpRoundingMethodSelect{v.GetDescription()}", Config.roundingMethod == v)) continue;
                    Config.roundingMethod = v;
                    hasChanged = true;
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {

                ImGui.BeginTooltip();
                ImGui.TextDisabled("If you are unsure, keep this setting on 'Floor'.\nThis is what the game uses by default.");
                ImGui.EndTooltip();
            }

            if (hasChanged)
            {
                TargetInfoUpdate((AtkUnitBase*)Service.GameGui.GetAddonByName("_TargetInfo", 1), null, null);
            }
        };

        private const string TargetInfoOnRequestedUpdate = "40 55 57 41 56 48 83 EC 40 48 8B 6A 48 48 8B F9 4D 8B 70 40 48 85 ED 0F 84 ?? ?? ?? ?? 4D 85 F6 0F 84 ?? ?? ?? ?? 48 8B 45 20 48 89 74 24 ?? 4C 89 7C 24 ?? 44 0F B6 B9 ?? ?? ?? ?? 83 38 00 8B 70 08 0F 95 C0";
        private const string TargetInfoMainTargetOnRequestedUpdate = "40 55 57 41 56 48 83 EC 40 48 8B 6A 48 48 8B F9 4D 8B 70 40 48 85 ED 0F 84 ?? ?? ?? ?? 4D 85 F6 0F 84 ?? ?? ?? ?? 48 8B 45 20 48 89 74 24 ?? 4C 89 7C 24 ?? 44 0F B6 B9 ?? ?? ?? ?? 83 38 00 8B 70 08 0F 94 C0 ";
        private const string FocusTargetInfoOnRequestedUpdate = "40 53 41 54 41 56 41 57 48 83 EC 78";

        public Configs Config { get; private set; }

        public override string Name => "Precise HP%";
        public override string Description => "Displays a more precise HP percentage.";
        protected override string Author => "maributt";

        private string OriginalTargetInfoPercentString;
        private float OriginalTargetNamePosX;

        const float CommaOffset = 3f;
        const float SingleNumberOffset = 9f;

        // extracted from Common from an earlier commit
        public delegate void* AddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** nums, StringArrayData** strings);
        public delegate void NoReturnAddonOnUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData);
        public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(IntPtr address, NoReturnAddonOnUpdate after)
        {
            Hook<AddonOnUpdate> hook = null;
            hook = GameInteropProvider.HookFromAddress<AddonOnUpdate>(address, (atkUnitBase, nums, strings) => {
                var retVal = hook.Original(atkUnitBase, nums, strings);
                try
                {
                    after(atkUnitBase, nums, strings);
                }
                catch (Exception ex)
                {
                    SimpleLog.Error(ex);
                    hook.Disable();
                }
                return retVal;
            });
            var wh = new HookWrapper<AddonOnUpdate>(hook);
            return wh;
        }
        public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(void* address, NoReturnAddonOnUpdate after) => HookAfterAddonUpdate(new IntPtr(address), after);
        public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(string signature, NoReturnAddonOnUpdate after, int addressOffset = 0) => HookAfterAddonUpdate(Service.SigScanner.ScanText(signature) + addressOffset, after);
        public static HookWrapper<AddonOnUpdate> HookAfterAddonUpdate(AtkUnitBase* atkUnitBase, NoReturnAddonOnUpdate after) => HookAfterAddonUpdate(atkUnitBase->AtkEventListener.vfunc[46], after);
        public HookWrapper<AddonOnUpdate> TargetInfoUpdateHook;
        public HookWrapper<AddonOnUpdate> TargetInfoMainTargetUpdateHook;
        public HookWrapper<AddonOnUpdate> FocusTargetInfoUpdateHook;

        protected override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            TargetInfoUpdateHook = HookAfterAddonUpdate(TargetInfoOnRequestedUpdate, TargetInfoUpdate);
            TargetInfoMainTargetUpdateHook = HookAfterAddonUpdate(TargetInfoMainTargetOnRequestedUpdate, TargetInfoMainTargetUpdate);
            FocusTargetInfoUpdateHook = HookAfterAddonUpdate(FocusTargetInfoOnRequestedUpdate, FocusTargetInfoUpdate);
            //FocusTargetInfoUpdateHook = Common.HookAfterAddonUpdate(FocusTargetInfoOnRequestedUpdate, FocusTargetUpdate);
            TargetInfoUpdateHook.Enable();
            TargetInfoMainTargetUpdateHook.Enable();
            FocusTargetInfoUpdateHook.Enable();
            TargetInfoUpdate((AtkUnitBase*)Service.GameGui.GetAddonByName("_TargetInfo", 1), null, null);
            base.Enable();
        }

        protected override void Disable()
        {
            SaveConfig(Config);
            TargetInfoUpdateHook.Disable();
            TargetInfoMainTargetUpdateHook.Disable();
            FocusTargetInfoUpdateHook.Disable();
            PluginConfig.UiAdjustments.PreciseHpPercent = null;
            ResetTargetInfo();
            base.Disable();
        }

        private string FormatHp(float hpPercentage)
        {
            return $"{hpPercentage.ToString((hpPercentage >= 1 ? "0" : "") + "0." + new string('0', Config.DecimalPrecision), CultureInfo.InvariantCulture)}" + "%";
        }

        private void ResetTargetInfo()
        {
            // if the addon isn't found the game will just reset it by itself when targeting something again
            var atkUnitBase = (AtkUnitBase*)Service.GameGui.GetAddonByName("_TargetInfo", 1);
            if (atkUnitBase == null) return;

            // can probably assume that something is targeted if targetinfo is found... but, just to be sure
            var target = Service.Targets.Target;
            if (target == null || target.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
                return;

            var pHpNode = atkUnitBase->UldManager.NodeList[38]->GetAsAtkTextNode();
            var tNameNode = atkUnitBase->UldManager.NodeList[39];

            pHpNode->SetText(OriginalTargetInfoPercentString);
            tNameNode->SetPositionFloat(OriginalTargetNamePosX, tNameNode->Y);
        }
        
        private void FocusTargetInfoUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
        {
            if (atkUnitBase == null || Service.Targets.FocusTarget == null) return;
            var targetInfoNode = atkUnitBase->UldManager.NodeList[10]->GetAsAtkTextNode();
            var bnpc = (BattleNpc)Service.Targets.FocusTarget;
            if (bnpc.CurrentHp == bnpc.MaxHp)
                return;

            var HpPercent = FormatHp(
                (float)Math.Round(
                    bnpc.CurrentHp / (float)bnpc.MaxHp * 100,
                    Config.DecimalPrecision,
                    (MidpointRounding)Config.roundingMethod
                ));
            var targetName = targetInfoNode->NodeText.GetString().Split('%')[1];
            
            targetInfoNode->SetText(HpPercent + targetName);
        }

        private void TargetInfoMainTargetUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
        {
            if (atkUnitBase == null) return;
            TargetInfoModifyHp(atkUnitBase, 8, 7);
        }

        private void TargetInfoUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
        {
            if (atkUnitBase == null) return;
            TargetInfoModifyHp(atkUnitBase, 39, 38);
        }

        private void TargetInfoModifyHp(AtkUnitBase* atkUnitBase, int NameNodeIndex, int HpPercentNodeIndex)
        {
            var HpPercentNode = atkUnitBase->UldManager.NodeList[HpPercentNodeIndex]->GetAsAtkTextNode();
            var NameNode = atkUnitBase->UldManager.NodeList[NameNodeIndex];

            var target = Service.Targets.Target;
            if (target == null || target.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
                return;
            
            OriginalTargetNamePosX = NameNode->X;
            OriginalTargetInfoPercentString = HpPercentNode->NodeText.GetString();

            NameNode->SetPositionFloat(
                OriginalTargetNamePosX + CommaOffset + SingleNumberOffset * Config.DecimalPrecision,
                NameNode->Y
                );

            var bnpc = (BattleNpc)target;
            if (bnpc.CurrentHp == bnpc.MaxHp)
                return;

            var HpPercent = FormatHp(
                (float)Math.Round(
                    bnpc.CurrentHp / (float)bnpc.MaxHp * 100,
                    Config.DecimalPrecision,
                    (MidpointRounding)Config.roundingMethod
                ));

            HpPercentNode->SetText(HpPercent);
        }
    }
}
