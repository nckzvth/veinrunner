// namespace: Game.Player
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Drives Animator parameters and flips sprite toward cursor.
    /// Keeps logic out of the controller for clean separation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimator2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerController2D controller;
        [SerializeField] private Miner miner;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer sprite;

        [Header("Facing")]
        [SerializeField] private bool faceByCursor = true;
        [SerializeField] private bool flipInvert = false; // if your art faces left by default

        [Header("Mining")]
        [SerializeField, Min(0f)] private float mineFlashSeconds = 0.12f;

        float mineTimer;

        static readonly int HashSpeed    = Animator.StringToHash("Speed");
        static readonly int HashGrounded = Animator.StringToHash("Grounded");
        static readonly int HashVelY     = Animator.StringToHash("VelY");
        static readonly int HashMine     = Animator.StringToHash("Mine");

        void Reset()
        {
            controller = GetComponentInParent<PlayerController2D>();
            miner      = GetComponentInParent<Miner>();
            animator   = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            sprite     = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        }

        void OnEnable()
        {
            if (!controller) controller = GetComponentInParent<PlayerController2D>();
            if (!miner) miner = GetComponentInParent<Miner>();
            if (miner != null) miner.Swung += OnSwung;
        }

        void OnDisable()
        {
            if (miner != null) miner.Swung -= OnSwung;
        }

        void OnSwung(Miner _)
        {
            mineTimer = mineFlashSeconds;
            if (animator) animator.SetTrigger(HashMine);
        }

        void Update()
        {
            if (!controller || !animator) return;

            // Parameters
            float speedX = Mathf.Abs(controller.HorizontalSpeed);
            animator.SetFloat(HashSpeed, speedX);
            animator.SetBool(HashGrounded, controller.IsGrounded);
            animator.SetFloat(HashVelY, controller.VerticalSpeed);

            // Facing
            if (sprite)
            {
                bool flip = sprite.flipX;
                if (faceByCursor && Camera.main && Mouse.current != null)
                {
                    Vector2 m = Mouse.current.position.ReadValue();
                    Vector2 mw = Camera.main.ScreenToWorldPoint(m);
                    float dx = mw.x - controller.transform.position.x;
                    if (Mathf.Abs(dx) > 0.001f) flip = (dx < 0f);
                }
                else
                {
                    float velx = controller.HorizontalSpeed;
                    if (Mathf.Abs(velx) > 0.02f) flip = (velx < 0f);
                }
                sprite.flipX = flipInvert ? !flip : flip;
            }

            if (mineTimer > 0f) mineTimer -= Time.unscaledDeltaTime;
        }
    }
}