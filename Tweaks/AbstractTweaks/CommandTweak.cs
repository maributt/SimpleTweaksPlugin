﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin.Tweaks.AbstractTweaks; 

public abstract class CommandTweak : Tweak {
    protected abstract string Command { get; }
    protected virtual string[] Alias => Array.Empty<string>();
    protected virtual string HelpMessage => $"[{Plugin.Name} {Name}";
    protected virtual bool ShowInHelp => true;
    
    protected abstract void OnCommand(string args);

    private void OnCommandInternal(string _, string args) => OnCommand(args);

    private List<string> registeredCommands = new();

    protected override void Enable() {
        base.Enable();
        var c = Command.StartsWith("/") ? Command : $"/{Command}";
        if (Service.Commands.Commands.ContainsKey(c)) {
            Plugin.Error(this, new Exception($"Command '{c}' is already registered."));
        } else {
            Service.Commands.AddHandler(c, new CommandInfo(OnCommandInternal) {
                HelpMessage = HelpMessage,
                ShowInHelp = ShowInHelp
            });
        
            registeredCommands.Add(c);
        }
        
        foreach (var a in Alias) {
            var alias = a.StartsWith("/") ? a : $"/{a}";
            if (!Service.Commands.Commands.ContainsKey(alias)) {
                Service.Commands.AddHandler(alias, new CommandInfo(OnCommandInternal) {
                    HelpMessage = HelpMessage,
                    ShowInHelp = false
                });
                registeredCommands.Add(alias);
            }
        }
    }

    protected override void Disable() {
        foreach(var c in registeredCommands) {
            Service.Commands.RemoveHandler(c);
        }
        registeredCommands.Clear();
        base.Disable();
    }

    protected List<string> GetArgumentList(string args) => Regex.Matches(args, @"[\""].+?[\""]|[^ ]+")
            .Select(m => {
                if (m.Value.StartsWith('"') && m.Value.EndsWith('"')) { return m.Value.Substring(1, m.Value.Length - 2); }
                return m.Value;
            })
            .ToList();
}
