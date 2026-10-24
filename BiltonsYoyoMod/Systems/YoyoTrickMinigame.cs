using Terraria;
using BiltonsYoyoMod.Items.Accessories;

namespace BiltonsYoyoMod.Systems
{
    public abstract class YoyoTrickMinigame
    {
        public bool Finished { get; protected set; }

        public bool Success { get; protected set; }

        public abstract void Start(
            Player player,
            YoyoTrickItem trick
        );

        public abstract void Update(
            Player player
        );

        // The Yoyo Trick keybind is sent here directly by
        // YoyoTrickPlayer.ProcessTriggers().
        public virtual void ReceiveKeyPress(
            Player player
        )
        {
        }
    }
}