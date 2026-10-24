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
            Helper.MultiPlayer
        }
    }
}

public class ModConfig
{
    public SButton HugKey { get; set; } = SButton.H;
    public int EnergyRestored = 10;
    public int CooldownTicks = 1000;
}

