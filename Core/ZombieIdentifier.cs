using EFT;

namespace ZSlayerZombieClient.Core;

public static class ZombieIdentifier
{
    public static bool IsInfected(BotOwner bot)
    {
        if (bot?.Profile?.Info?.Settings == null) return false;
        return IsInfectedRole(bot.Profile.Info.Settings.Role);
    }

    public static bool IsInfectedRole(WildSpawnType role)
    {
        var val = (int)role;
        return val >= ZombieConstants.InfectedAssault && val <= ZombieConstants.InfectedTagilla;
    }
}
