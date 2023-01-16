using System;
using System.ComponentModel;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Logging;
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
                    PluginLog.LogDebug(v.GetDescription());
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

        public Configs Config { get; private set; }

        public override string Name => "Precise HP%";
        public override string Description => "Displays a more precise HP percentage.";
        protected override string Author => "maributt";

        private string OriginalTargetInfoPercentString;
        private float OriginalTargetNamePosX;

        const float CommaOffset = 3f;
        const float SingleNumberOffset = 9f;

        public HookWrapper<Common.AddonOnUpdate> TargetInfoUpdateHook;

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            TargetInfoUpdateHook = Common.HookAfterAddonUpdate(TargetInfoOnRequestedUpdate, TargetInfoUpdate);
            TargetInfoUpdateHook.Enable();
            TargetInfoUpdate((AtkUnitBase*)Service.GameGui.GetAddonByName("_TargetInfo", 1), null, null);
            base.Enable();
        }

        public override void Disable()
        {
            SaveConfig(Config);
            TargetInfoUpdateHook.Disable();
            PluginConfig.UiAdjustments.PreciseHpPercent = null;
            ResetTargetInfo();
            base.Disable();
        }

        private string FormatHp(float hpPercentage)
        {
            return $"{hpPercentage.ToString((hpPercentage >= 1 ? "0" : "") + "0." + new string('0', Config.DecimalPrecision), CultureInfo.InvariantCulture)}%";
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

        private void TargetInfoUpdate(AtkUnitBase* atkUnitBase, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
        {
            if (atkUnitBase == null) return;

            var HpPercentNode = atkUnitBase->UldManager.NodeList[38]->GetAsAtkTextNode();
            var NameNode = atkUnitBase->UldManager.NodeList[39];

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
