using System;
using BestHTTP;
using Il2CppInterop.Runtime;

namespace Recadoodle.Patches;

// The game's anti-hile check scans loaded modules on room changes and finds BepInEx's doorstop
// proxy (winhttp.dll). It reports it to /api/PlayerReporting/v1/hile (Type=5) and then logs the
// player out and soft-reboots to a loading screen that never finishes (no exclusivelogin).
//
// The detector isn't in CheatManager (which Plugin deactivates), and its obfuscated class isn't
// known yet, so this works at the request level:
//   1. Logs the IL2CPP call stack of the report, so the detector can be found and patched directly.
//   2. Swallows the request's completion callback, so whatever waits on the report never hears
//      back and never runs the kick path.
internal static class AntiHilePatch
{
    private const string HileEndpoint = "/PlayerReporting/v1/hile";

    // Called from the SendRequest prefix. Returns true when the request was a hile report.
    internal static bool Handle(HTTPRequest request)
    {
        if (!request.Uri.AbsolutePath.Contains(HileEndpoint, StringComparison.OrdinalIgnoreCase))
            return false;

        Plugin.Log.LogWarning("[ANTI-HILE] suppressed a cheat report; the game will not be told it was sent.");

        try
        {
            var trace = new Il2CppSystem.Diagnostics.StackTrace(true).ToString();
            Plugin.Log.LogWarning($"[ANTI-HILE] reported from:\n{trace}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[ANTI-HILE] could not capture the call stack: {e.Message}");
        }

        request.Callback = DelegateSupport.ConvertDelegate<OnRequestFinishedDelegate>(
            (Action<HTTPRequest, HTTPResponse>)((_, _) => { }));
        return true;
    }
}
