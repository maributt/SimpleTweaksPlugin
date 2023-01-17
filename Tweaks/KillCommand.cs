using SimpleTweaksPlugin.Tweaks.AbstractTweaks;
using System.Diagnostics;


namespace SimpleTweaksPlugin.Tweaks;
public unsafe class KillCommand : CommandTweak
{
    public override string Name => "Kill Command";
    public override string Description => "Adds the command /xlkill to kill the game.";
    protected override string Author => "maributt";
    protected override string Command => "xlkill";
    protected override string HelpMessage => "Kills the game.";
    
    protected override void OnCommand(string args)
    {
        Process.GetCurrentProcess().Kill();
    }
}
