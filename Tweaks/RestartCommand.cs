using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimpleTweaksPlugin.Tweaks;
public unsafe class RestartCommand : CommandTweak
{
    public override string Name => "Restart Command";
    public override string Description => "Adds the command /xlrestart to restart the game.";
    protected override string Author => "maributt";
    protected override string Command => "xlrestart";
    protected override string HelpMessage => "Restarts the game.";

    protected override void OnCommand(string args)
    {
        // https://github.com/goatcorp/Dalamud/blob/cc4a0652c2bc568ff9eb9d0a5311c9252ca41e9e/Dalamud/Interface/Internal/DalamudInterface.cs#L651
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern void RaiseException(uint dwExceptionCode, uint dwExceptionFlags, uint nNumberOfArguments, IntPtr lpArguments);

        RaiseException(0x12345678, 0, 0, IntPtr.Zero);
        Process.GetCurrentProcess().Kill();
    }
}
