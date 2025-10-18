// namespace: Game.Player
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float baseMoveSpeed = 2.2f;   // u/s @ 32 ppu → ~70 px/s
        [SerializeField, Range(1f, 2f)] private float sprintMultiplier = 1.25f;

        [Header("Acceleration")]
        [SerializeField] private float groundAccel = 25f;
        [SerializeField] private float airAccel = 18f;

        [Header("Jump (scale-proof)")]
        [Tooltip("Desired peak jump height in world units. v0 computed from gravity.")]
        [SerializeField] private float desiredJumpHeightUnits = 1.1f;

        [Header("Responsiveness")]
        [SerializeField] private float coyoteTime = 0.10f;
        [SerializeField] private float jumpBuffer = 0.10f;

        [Header("Grounding")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.12f;
        [SerializeField] private LayerMask groundMask;

        [Header("Progression Hooks")]
        [SerializeField, Min(0)] private int extraJumps = 0;
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private float jumpForceMultiplier = 1f;

        [Header("Links / Input")]
        [SerializeField] private Bag bag;
        [SerializeField] private PlayerInput input;

        const float OVERCAP_SPEED_MULT = 0.6f; // −40% when over-cap

        Rigidbody2D rb;
        Vector2 moveInput;
        bool wantSprint;
        bool isGrounded;
        int jumpsUsed;
        float lastGroundedTime;
        float lastJumpPressedTime;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (!bag) bag = GetComponent<Bag>() ?? GetComponentInChildren<Bag>();
            if (!groundCheck) Debug.LogWarning("PlayerController2D: GroundCheck not assigned.");
            if (!input) input = GetComponent<PlayerInput>();
            rb.freezeRotation = true;
        }

        void OnEnable()
        {
            if (!input) input = GetComponent<PlayerInput>();
            if (input && input.actions != null)
            {
                var map = input.actions.FindActionMap("Player", false);
                if (map != null)
                {
                    var move = map.FindAction("Move");
                    var jump = map.FindAction("Jump");
                    var sprint = map.FindAction("Sprint");
                    if (move != null) { move.performed += OnMove; move.canceled += OnMove; }
                    if (jump != null) { jump.performed += OnJump; }
                    if (sprint != null) { sprint.performed += OnSprint; sprint.canceled += OnSprint; }
                }
            }
        }

        void OnDisable()
        {
            if (input && input.actions != null)
            {
                var map = input.actions.FindActionMap("Player", false);
                if (map != null)
                {
                    var move = map.FindAction("Move");
                    var jump = map.FindAction("Jump");
                    var sprint = map.FindAction("Sprint");
                    if (move != null) { move.performed -= OnMove; move.canceled -= OnMove; }
                    if (jump != null) { jump.performed -= OnJump; }
                    if (sprint != null) { sprint.performed -= OnSprint; sprint.canceled -= OnSprint; }
                }
            }
        }

        void Update()
        {
            if (groundCheck)
            {
                isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
                if (isGrounded)
                {
                    lastGroundedTime = coyoteTime;
                    jumpsUsed = 0;
                }
            }
            if (lastGroundedTime > 0) lastGroundedTime -= Time.unscaledDeltaTime;
            if (lastJumpPressedTime > 0) lastJumpPressedTime -= Time.unscaledDeltaTime;

            TryConsumeJumpBuffered();
        }

        void FixedUpdate()
        {
            float targetSpeed = GetEffectiveMoveSpeed();
            float xTarget = moveInput.x * targetSpeed;

            Vector2 lv = rb.linearVelocity;

            float accel = isGrounded ? groundAccel : airAccel;
            float vx = Mathf.MoveTowards(lv.x, xTarget, accel * Time.fixedDeltaTime);

            // Anti-slide bias when grounded
            float vy = lv.y;
            if (isGrounded && vy < 0f) vy = -0.5f;

            rb.linearVelocity = new Vector2(vx, vy);
        }

        float GetEffectiveMoveSpeed()
        {
            float sprintMult = wantSprint ? sprintMultiplier : 1f;
            float overcapMult = (bag && bag.IsOverCap) ? OVERCAP_SPEED_MULT : 1f;
            return baseMoveSpeed * moveSpeedMultiplier * sprintMult * overcapMult;
        }

        void Jump()
        {
            bool canGroundJump = isGrounded || lastGroundedTime > 0f;
            bool canAirJump = jumpsUsed < extraJumps;
            if (!canGroundJump && !canAirJump) return;

            // Compute initial velocity from desired height: v0 = sqrt(2 * g * h)
            float g = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
            float v0 = Mathf.Sqrt(Mathf.Max(0.01f, 2f * g * desiredJumpHeightUnits));
            v0 *= jumpForceMultiplier;

            Vector2 v = rb.linearVelocity;
            if (v.y < 0f) v.y = 0f; // cut downward for snappy takeoff
            v.y = v0;
            rb.linearVelocity = v;

            if (!isGrounded && lastGroundedTime <= 0f) jumpsUsed++;
            lastJumpPressedTime = 0f;
        }

        void TryConsumeJumpBuffered()
        {
            if (lastJumpPressedTime > 0f)
            {
                bool canGroundJump = isGrounded || lastGroundedTime > 0f;
                bool canAirJump = jumpsUsed < extraJumps;
                if (canGroundJump || canAirJump) Jump();
            }
        }

        // Input
        public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
        public void OnJump(InputAction.CallbackContext ctx) { if (ctx.performed) lastJumpPressedTime = jumpBuffer; }
        public void OnSprint(InputAction.CallbackContext ctx) { if (ctx.performed) wantSprint = true; if (ctx.canceled) wantSprint = false; }
        public void OnInteract(InputAction.CallbackContext ctx) { /* stub */ }

        // Progression APIs
        public void SetExtraJumps(int v) => extraJumps = Mathf.Max(0, v);
        public void SetMoveSpeedMultiplier(float mult) => moveSpeedMultiplier = Mathf.Max(0.1f, mult);
        public void SetJumpForceMultiplier(float mult) => jumpForceMultiplier = Mathf.Max(0.1f, mult);

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (groundCheck)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }
        }
#endif
    }
}