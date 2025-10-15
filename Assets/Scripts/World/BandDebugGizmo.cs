using UnityEngine;

namespace Game.World
{
    [ExecuteAlways]
    public sealed class BandDebugGizmo : MonoBehaviour
    {
        public float pixelsPerUnit = 32f;
        public Color yardLine = new Color(0.2f, 1f, 0.2f, 0.35f);
        public Color galLine  = new Color(1f, 0.9f, 0.2f, 0.35f);
        public Color ancLine  = new Color(1f, 0.3f, 0.2f, 0.35f);
        const float Extent = 500f;

        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.identity;
            DrawLineAt(BandResolver.YardTopTileY,      yardLine);
            DrawLineAt(BandResolver.GalleriesTopTileY, galLine);
            DrawLineAt(BandResolver.AncientTopTileY,   ancLine);
        }

        void DrawLineAt(int tileY, Color c)
        {
            float worldY = tileY / pixelsPerUnit;
            Gizmos.color = c;
            Gizmos.DrawLine(new Vector3(-Extent, worldY, 0), new Vector3(Extent, worldY, 0));
        }
    }
}

