// File: Assets/Scripts/World/BandResolver.cs
namespace Game.World
{
    /// <summary>Resolves band by tile Y and effective tier by band + classifier tier.</summary>
    public static class BandResolver
    {
        public enum Band { Yard, Galleries, Ancient }

        public static Band BandFromY(int tileY)
        {
            if (tileY <= -384) return Band.Ancient;
            if (tileY <= -128) return Band.Galleries;
            return Band.Yard;
        }

        public static int EffectiveTier(Band band, int classifierTier)
        {
            // TODO: tune if band modifies classifier tier.
            return classifierTier;
        }
    }
}
