// Namespace: Game.World
namespace Game.World
{
    public static class BandResolver
    {
        // Depth bands: Yard (>= -128), Galleries (>= -384), Ancient (< -384)
        public static int BandFromY(int tileY)
        {
            if (tileY >= -128) return 0;
            if (tileY >= -384) return 1;
            return 2;
        }

        public static int EffectiveTier(int band, int classifierTier)
        {
            // Placeholder; refine when Classifier goes in
            return band < classifierTier ? band : classifierTier;
        }
    }
}

