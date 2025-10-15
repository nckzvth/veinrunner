// Namespace: Game.World
using UnityEngine;
using Game.Data; // <-- so BandResolver sees BandConfigSO in Game.Data

namespace Game.World
{
    public static class BandResolver
    {
        public enum Band : int { Yard = 0, Galleries = 1, Ancient = 2 }

        static BandConfigSO _cfg;

        public static void SetConfig(BandConfigSO cfg)
        {
            _cfg = cfg;
            if (_cfg == null) Debug.LogWarning("[BandResolver] BandConfigSO not set; defaults will be used.");
        }

        // ---- PUBLIC threshold accessors (tile Y). Defaults keep you unblocked if config missing.
        public static int YardTopTileY      => _cfg ? _cfg.YardTopTileY      : 0;
        public static int GalleriesTopTileY => _cfg ? _cfg.GalleriesTopTileY : -128;
        public static int AncientTopTileY   => _cfg ? _cfg.AncientTopTileY   : -384;

        public static Band BandFromY(int tileY)
        {
            if (tileY >= GalleriesTopTileY) return Band.Yard;
            if (tileY >= AncientTopTileY)   return Band.Galleries;
            return Band.Ancient;
        }

        public static int EffectiveTier(Band band, int classifierTier)
            => Mathf.Min((int)band, classifierTier);

        public static int GetBandArmor(Band band) => (int)band;

        public static int WorldYToTileY(float worldY, float pixelsPerUnit, int pixelsPerTile = 1)
        {
            float tilesPerUnit = pixelsPerUnit / pixelsPerTile;
            return Mathf.RoundToInt(worldY * tilesPerUnit);
        }

        public static (int yardTop, int galleriesTop, int ancientTop) GetThresholds()
            => (YardTopTileY, GalleriesTopTileY, AncientTopTileY);
    }
}
