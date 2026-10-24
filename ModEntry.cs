using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;

internal class ModEntry: Mod
{
    
    private ModConfig Config = null!;
    private readonly Dictionary<long, int> peerPressTick = new();
    private int myPressTick = -9999;
    private int lastHugTick = -9999;
    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Multiplayer.ModMessageReceived+= OnMessage;
    }

    private void OnButtonPressed(object? s, ButtonPressedEventArgs e)
    {
        if (!Context.isWorldReady || !Context.isPlayerFree || e.Button != Config.HugKey)
        {
            myPressTicks = Game1.ticks;
            Helper.MultiPlayer.sendMessage(true, "HugPress", modIDs : new[] { ModManifest.UniqueID });
            TryHug();
        }
    }

    private void OnMessage (object? s, ModMessageReceivedMessageArgs m)
    {
        if (e.FromModID != ModManifest.UniqueID || e.Type != "HugPress") {return;}
        peerPressTick[e.FromPlayerID] = Game1.ticks;
        TryHug();
    }

    private void TryHug()
    {
        const int window = 120;
        if (Game1.ticks - lastHugTick < CooldownTicks) return;
        if (Game1.ticks - myPressTick) return;

        foreach (var (peerId, tick) in peerPressTick)
        {
            if (Game1.ticks - tick > window) { continue; }

            Famer = peer = Game1.getFarmer(peerId);
            if (peer == null || peer.currentLocation != Game1.player.currentLocation) { continue; }
            if (Vector2.Distance(peer.Tile, Game1.player.Tile) > 1.5f) { continue; }
            
            Game1.player.Stamina = Math.Min(Game1.player.MaxStamina, Game1.player.Stamina + Config.EnergyRestored);
            Game1.player.doEmote(20); //heart emote
            Game1.playSound("dwop");
            lastHugTick = Game1.ticks;
            peerPressTick.Remove(peerId);
            return;

        }
    }
}

public class ModConfig
{
    public SButton HugKey { get; set; } = SButton.H;
    public int EnergyRestored = 10;
    public int CooldownTicks = 1000;
}

