// namespace: Game.FX
using System.Collections.Generic;
using UnityEngine;
using Game.Player;  // Miner
using Game.World;   // TerrainChunk.RimSample

namespace Game.FX
{
    /// <summary>
    /// EXACT port of the proven approach:
    /// - One scene ParticleSystem (no instantiation/pooling).
    /// - Emits from rim using RimSample.worldPos and RimSample.outward.
    /// - Works with CompositeCollider2D + Collision2D (High, Dynamic).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CrumbleFXHook : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Miner miner;                  // auto-found if null
        [Tooltip("Scene Particle System (NOT a prefab asset).")]
        [SerializeField] public ParticleSystem crumblePS;
        [SerializeField] public LayerMask collideWith = 0;

        [Header("Burst shape")]
        [SerializeField] public float particlesPerMeter = 90f;
        [SerializeField] public int   minPerBurst       = 10;
        [SerializeField] public int   maxPerBurst       = 120;
        [Tooltip("Push spawn point off the wall to avoid instant collisions.")]
        [SerializeField] public float spawnOffset       = 0.03f;
        [Range(0.05f, 1f)] public float rimCoverage     = 0.35f; // reserved hook if you later subsample

        [Header("Initial velocity")]
        [SerializeField] public float popSpeed    = 0.75f;
        [SerializeField] public float fillPopMul  = 0.45f;
        [SerializeField] public float jitterSpeed = 0.35f;
        [SerializeField] public float settleBiasY = -0.25f;

        [Header("Particle defaults")]
        [SerializeField] public Vector2 sizeRange = new(0.07f, 0.10f);
        [SerializeField] public Vector2 lifeRange = new(0.75f, 1.10f);
        [SerializeField] public Color   digColor  = new(0.78f, 0.68f, 0.52f, 1f);
        [SerializeField] public Color   fillColor = new(0.55f, 0.62f, 0.75f, 1f);

        [Header("Lifetime styling")]
        [Range(0.00f, 0.5f)] public float fadeInFrac  = 0.25f;
        [Range(0.20f, 0.95f)] public float visibleFrac = 0.70f;

        void Reset()
        {
            if (!miner) miner = GetComponentInParent<Miner>();
        }

        void Awake()
        {
            if (!crumblePS)
            {
                Debug.LogWarning("[CrumbleFXHook] Assign a ParticleSystem in the scene.");
                enabled = false;
                return;
            }

            // Draw above terrain
            var r = crumblePS.GetComponent<ParticleSystemRenderer>();
            r.sortingOrder = Mathf.Max(300, r.sortingOrder);

            // Collision (2D) — use the settings you validated
            var col = crumblePS.collision;
            col.enabled = true;
            col.type    = ParticleSystemCollisionType.World;
            col.mode    = ParticleSystemCollisionMode.Collision2D;
            col.enableDynamicColliders = true;
            col.maxCollisionShapes     = 2048;
            col.minKillSpeed = 0f;
            col.maxKillSpeed = 10000f;
            col.radiusScale  = 0.75f;
            col.quality      = ParticleSystemCollisionQuality.High;

            if (collideWith != 0) col.collidesWith = collideWith;
            else
            {
                int ground = LayerMask.NameToLayer("Ground");
                col.collidesWith = (ground >= 0) ? (LayerMask)(1 << ground) : ~0;
            }

            var dmp = col.dampen; dmp.mode = ParticleSystemCurveMode.Constant; dmp.constant = 0.15f; col.dampen = dmp;
            var bnc = col.bounce;  bnc.mode = ParticleSystemCurveMode.Constant;  bnc.constant  = 0.01f; col.bounce  = bnc;

            // World space + mild gravity
            var main = crumblePS.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var g = main.gravityModifier; g.mode = ParticleSystemCurveMode.Constant; g.constant = Mathf.Max(0.12f, g.constant); main.gravityModifier = g;

            // Color over Lifetime: fade in -> hold -> fade out
            var colife = crumblePS.colorOverLifetime; colife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeInFrac)),
                    new GradientAlphaKey(1f, Mathf.Clamp01(visibleFrac)),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colife.color = new ParticleSystem.MinMaxGradient(grad);

            // Size over Lifetime: small -> grow -> shrink
            var sol = crumblePS.sizeOverLifetime; sol.enabled = true;
            AnimationCurve sizeCurve = new(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.15f, 1f),
                new Keyframe(1f, 0f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
            
            // Gentle noise wobble
            var noise = crumblePS.noise; noise.enabled = true;
            var strength = noise.strength; strength.mode = ParticleSystemCurveMode.Constant; strength.constant = 0.08f; noise.strength = strength;
            noise.frequency = 0.5f; noise.scrollSpeed = 0.2f;
        }

        void OnEnable()
        {
            if (!miner) miner = GetComponentInParent<Miner>();
            if (miner != null) miner.DigApplied += OnDigApplied;
        }

        void OnDisable()
        {
            if (miner != null) miner.DigApplied -= OnDigApplied;
        }

        // Miner → rim samples (from TerrainChunk.ApplyCircle)
        void OnDigApplied(IReadOnlyList<TerrainChunk.RimSample> rim)
        {
            EmitFromRim(rim, isDig: true);
        }

        public void EmitFromRim(IReadOnlyList<TerrainChunk.RimSample> rim, bool isDig)
        {
            if (!crumblePS || rim == null || rim.Count == 0) return;

            // Estimate count from rim density (same heuristic as your original)
            int target = Mathf.Clamp(
                Mathf.RoundToInt(rim.Count * Mathf.Clamp(particlesPerMeter * 0.02f, 0.1f, 3f)),
                minPerBurst, maxPerBurst
            );

            var emit = new ParticleSystem.EmitParams
            {
                startColor = isDig ? digColor : fillColor
            };

            float speed = isDig ? popSpeed : popSpeed * fillPopMul;

            for (int i = 0; i < target; i++)
            {
                var rs = rim[Random.Range(0, rim.Count)];

                // OUTWARD from the wall (provided by World)
                Vector2 n = rs.outward.sqrMagnitude > 1e-6f ? rs.outward : Vector2.down;

                // Spawn slightly off wall so we don't start inside colliders
                Vector2 p = rs.worldPos + n * Mathf.Max(0.02f, spawnOffset);

                // Tangent for arc variation
                Vector2 t = new Vector2(-n.y, n.x);

                // Small arc around outward
                float ang = Random.Range(-0.30f, 0.30f);
                Vector2 dir = (n * Mathf.Cos(ang) + t * Mathf.Sin(ang)).normalized;

                Vector2 v = dir * speed + (Random.insideUnitCircle * jitterSpeed) + new Vector2(0f, settleBiasY);

                emit.position      = new Vector3(p.x, p.y, 0f);
                emit.velocity      = new Vector3(v.x, v.y, 0f);
                emit.startSize     = Random.Range(sizeRange.x, sizeRange.y);
                emit.startLifetime = Random.Range(lifeRange.x, lifeRange.y);

                crumblePS.Emit(emit, 1);
            }
        }
    }
}