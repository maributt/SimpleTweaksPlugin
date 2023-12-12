﻿#nullable enable
using System;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.Chat;

[TweakName("Sticky Chat")]
[TweakDescription("Sets the chat channel when you use temporary chat messages.\nExample: \"/p hello!\" will set the chat channel to Party")]
[TweakAuthor("MidoriKami")]
[TweakReleaseVersion("1.8.2.0")]
public unsafe partial class StickyChat : ChatTweaks.SubTweak {
    private delegate byte ProcessChatInputDelegate(nint uiModule, byte** message, nint a3);

    [TweakHook, Signature("E8 ?? ?? ?? ?? FE 86 ?? ?? ?? ?? C7 86 ?? ?? ?? ?? ?? ?? ?? ??", DetourName = nameof(ProcessChatInputDetour))]
    private readonly HookWrapper<ProcessChatInputDelegate>? processChatInputHook = null;

    [GeneratedRegex("^\\/cwl[1-8] .+")]
    private static partial Regex CrossWorldLinkshellShort();
    
    [GeneratedRegex("^\\/cwlinkshell[1-8] .+")]
    private static partial Regex CrossWorldLinkshellLong();
    
    [GeneratedRegex("^\\/l[1-8] .+")]
    private static partial Regex LinkshellShort();
    
    [GeneratedRegex("^\\/linkshell[1-8] .+")]
    private static partial Regex LinkshellLong();
    
    private byte ProcessChatInputDetour(nint uiModule, byte** message, nint a3) {
        var result = processChatInputHook!.Original(uiModule, message, a3);
        
        try {
            var stringSize = StringLength(message);
            var inputString = Encoding.UTF8.GetString(*message, stringSize);

            switch (inputString) {
                case not null when inputString.StartsWith("/party "):
                case not null when inputString.StartsWith("/p "):
                    ChatHelper.SendMessage("/party");
                    break;
                
                case not null when inputString.StartsWith("/say "):
                case not null when inputString.StartsWith("/s "):
                    ChatHelper.SendMessage("/say");
                    break;
                
                case not null when inputString.StartsWith("/alliance "):
                case not null when inputString.StartsWith("/a "):
                    ChatHelper.SendMessage("/alliance");
                    break;
                
                case not null when inputString.StartsWith("/freecompany "):
                case not null when inputString.StartsWith("/fc "):
                    ChatHelper.SendMessage("/freecompany");
                    break;
                
                case not null when inputString.StartsWith("/novice "):
                case not null when inputString.StartsWith("/n "):
                    ChatHelper.SendMessage("/novice");
                    break;
                
                case not null when inputString.StartsWith("/yell "):
                case not null when inputString.StartsWith("/y "):
                    ChatHelper.SendMessage("/yell");
                    break;

                case not null when CrossWorldLinkshellLong().IsMatch(inputString) && inputString.Length > 12:
                    ChatHelper.SendMessage($"/cwlinkshell{inputString[12]}");
                    break;
                
                case not null when CrossWorldLinkshellShort().IsMatch(inputString) && inputString.Length > 4:
                    ChatHelper.SendMessage($"/cwl{inputString[4]}");
                    break;
                    
                case not null when LinkshellLong().IsMatch(inputString) && inputString.Length > 10:
                    ChatHelper.SendMessage($"/linkshell{inputString[10]}");
                    break;
                
                case not null when LinkshellShort().IsMatch(inputString) && inputString.Length > 2:
                    ChatHelper.SendMessage($"/l{inputString[2]}");
                    break;
            }
        }
        catch (Exception e) {
            SimpleLog.Error(e, "Something went wrong in StickyChat, let MidoriKami know!");
        }

        return result;
    }

    private static int StringLength(byte** message) {
        var byteCount = 0;
        for (var i = 0; i <= 500; i++) {
            if (*(*message + i) != 0) continue;
            
            byteCount = i;
            break;
        }

        return byteCount;
    }
}
