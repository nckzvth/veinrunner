using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "World/Band Config", fileName = "BandConfig")]
    public class BandConfigSO : ScriptableObject
    {
        [Header("Terrain")]
        [Tooltip("Pixels per chunk side (must match TerrainWorld.chunkPixels).")]
        public int chunkPixels = 64;

        [Header("Band Depths (in chunks)")]
        [Tooltip("Depth of Yard band measured in chunks (top band).")]
        public int yardChunks = 8;
        [Tooltip("Depth of Galleries band measured in chunks.")]
        public int galleriesChunks = 16;
        [Tooltip("Depth of Ancient band measured in chunks.")]
        public int ancientChunks = 32;

        // Computed tile-Y thresholds (0 at surface; negative downward)
        public int YardTopTileY => 0;
        public int GalleriesTopTileY => -(yardChunks * chunkPixels);
        public int AncientTopTileY  => -((yardChunks + galleriesChunks) * chunkPixels);
        // Ancient bottom is open-ended
    }
}
