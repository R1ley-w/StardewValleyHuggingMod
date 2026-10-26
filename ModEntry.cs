using System;
using Microsoft.Xna.Framework;

using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

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
        if (!Context.IsWorldReady || !Context.IsPlayerFree || e.Button != Config.HugKey)
            return;

        myPressTick = Game1.ticks;
        Monitor.Log($"[Hug] You pressed {e.Button} at tick {Game1.ticks}. Sending HugPress to peers.", LogLevel.Debug);
        Helper.Multiplayer.SendMessage(true, "HugPress", modIDs : new[] { ModManifest.UniqueID });
        TryHug();
    }

    private void OnMessage (object? s, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != ModManifest.UniqueID || e.Type != "HugPress") {return;}
        peerPressTick[e.FromPlayerID] = Game1.ticks;
        Monitor.Log($"[Hug] Received HugPress from player {e.FromPlayerID} at tick {Game1.ticks}.", LogLevel.Debug);
        TryHug();
    }

    private void TryHug()
    {
        const int window = 120;
        if (Game1.ticks - this.lastHugTick < this.Config.CooldownTicks) { Monitor.Log("[Hug] Still on cooldown.", LogLevel.Debug); return; }
        if (Game1.ticks - this.myPressTick > window) { Monitor.Log("[Hug] Waiting for you to press the hug key too.", LogLevel.Debug); return; }
        if (peerPressTick.Count == 0) Monitor.Log("[Hug] No other player has pressed the hug key yet.", LogLevel.Debug);

        foreach (var (peerId, tick) in peerPressTick)
        {
            if (Game1.ticks - tick > window) { Monitor.Log($"[Hug] Player {peerId}'s press was too long ago ({Game1.ticks - tick} ticks).", LogLevel.Debug); continue; }

            Farmer? peer = Game1.GetPlayer(peerId, false);
            if (peer == null || peer.currentLocation != Game1.player.currentLocation) { Monitor.Log($"[Hug] Player {peerId} not found or not in your location.", LogLevel.Debug); continue; }
            Monitor.Log($"[Hug] Distance to {peer.Name}: {Vector2.Distance(peer.Tile, Game1.player.Tile):0.00} tiles.", LogLevel.Debug);
            if (Vector2.Distance(peer.Tile, Game1.player.Tile) > 1.5f)
            {
                Game1.addHUDMessage(new HUDMessage("Too far away to hug!", HUDMessage.error_type));
                peerPressTick.Remove(peerId);
                return;
            }
            
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

