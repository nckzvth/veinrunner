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
        [SerializeField] private float desiredJumpHeightUnits = 0.50f;

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

        [Header("Environment Sensors")]
        [SerializeField] private WallSensors2D sensors; // used for ceiling only in dash; walls later

        [Header("Links / Input")]
        [SerializeField] private Bag bag;
        [SerializeField] private PlayerInput input;

        // ===== Dash (groundwork) =====
        [Header("Dash (groundwork)")]
        [SerializeField] private bool dashEnabled = false;             // feature flag
        [SerializeField, Min(0.1f)] private float dashSpeed = 6.0f;    // units/sec
        [SerializeField, Min(0.05f)] private float dashDuration = 0.12f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.50f;
        [SerializeField, Range(0, 2)] private int dashAirCharges = 1;  // dashes allowed while airborne
        [SerializeField, Range(0.05f, 0.5f)] private float dashInputDeadzone = 0.20f;
        [SerializeField] private bool allowGroundDash = true;
        [SerializeField] private bool dashCutUpwardVelocityAtStart = true;
        [Tooltip("Delay before wall-cancel is evaluated, to avoid canceling on ground contact/slopes.")]
        [SerializeField, Range(0f, 0.12f)] private float dashWallCancelDelay = 0.035f;
        [Tooltip("If dashing from ground, set at least this upward Vy so we cleanly leave the floor.")]
        [SerializeField, Min(0f)] private float dashGroundLiftVy = 1.0f;

        [Header("Dash Collision (cast)")]
        [Tooltip("Extra distance added to the cast to avoid missing very close walls.")]
        [SerializeField, Min(0f)] private float dashCastSkin = 0.02f;
        [Tooltip("Treat a hit as a wall if |normal.x| >= this. 0.7≈mostly horizontal surface.")]
        [SerializeField, Range(0.5f, 0.95f)] private float dashWallNormalDotMin = 0.70f;

        const float OVERCAP_SPEED_MULT = 0.6f; // −40% when over-cap

        Rigidbody2D rb;
        Vector2 moveInput;
        bool wantSprint;
        bool isGrounded;
        bool wasGrounded;
        int jumpsUsed;
        float lastGroundedTime;
        float lastJumpPressedTime;

        // Dash state
        bool isDashing;
        float dashTimer;
        float dashCooldownTimer;
        float dashWallGraceTimer;
        int dashDir;                // -1 = left, +1 = right
        int dashesRemaining;        // replenished on ground touch

        // Direction fallbacks
        int lastFacingSign = 1;     // +1 right, -1 left
        int lastMoveSign   = 1;     // last non-zero horizontal input

        // Cast cache
        readonly RaycastHit2D[] dashCastHits = new RaycastHit2D[4];
        ContactFilter2D dashFilter;

        public bool IsGrounded => isGrounded;
        public float HorizontalSpeed => rb ? rb.linearVelocity.x : 0f;
        public float VerticalSpeed => rb ? rb.linearVelocity.y : 0f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (!bag) bag = GetComponent<Bag>() ?? GetComponentInChildren<Bag>();
            if (!groundCheck) Debug.LogWarning("PlayerController2D: GroundCheck not assigned.");
            if (!input) input = GetComponent<PlayerInput>();
            if (!sensors) sensors = GetComponent<WallSensors2D>();
            rb.freezeRotation = true;

            dashesRemaining = dashAirCharges;

            // Build dash contact filter (ground layers only, no triggers)
            dashFilter.ClearLayerMask();
            dashFilter.SetLayerMask(groundMask);
            dashFilter.useTriggers = false;
        }

        void OnValidate()
        {
            // keep cast filter synced with mask
            dashFilter.ClearLayerMask();
            dashFilter.SetLayerMask(groundMask);
            dashFilter.useTriggers = false;
        }

        void OnEnable()
        {
            if (!input) input = GetComponent<PlayerInput>();
            if (input && input.actions != null)
            {
                var map = input.actions.FindActionMap("Player", false);
                if (map != null)
                {
                    var move   = map.FindAction("Move");
                    var jump   = map.FindAction("Jump");
                    var sprint = map.FindAction("Sprint");
                    var dash   = map.FindAction("Dash");

                    if (move   != null) { move.performed += OnMove; move.canceled += OnMove; }
                    if (jump   != null) { jump.performed += OnJump; }
                    if (sprint != null) { sprint.performed += OnSprint; sprint.canceled += OnSprint; }
                    if (dash   != null) { dash.performed += OnDash; }
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
                    var move   = map.FindAction("Move");
                    var jump   = map.FindAction("Jump");
                    var sprint = map.FindAction("Sprint");
                    var dash   = map.FindAction("Dash");

                    if (move   != null) { move.performed -= OnMove; move.canceled  -= OnMove; }
                    if (jump   != null) { jump.performed -= OnJump; }
                    if (sprint != null) { sprint.performed -= OnSprint; sprint.canceled -= OnSprint; }
                    if (dash   != null) { dash.performed -= OnDash; }
                }
            }
        }

        void Update()
        {
            // Grounding (simple circle)
            if (groundCheck)
            {
                isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
                if (isGrounded)
                {
                    lastGroundedTime = coyoteTime;
                    jumpsUsed = 0;
                }
            }

            // Refill dash charges on landing
            if (isGrounded && !wasGrounded)
                dashesRemaining = dashAirCharges;
            wasGrounded = isGrounded;

            if (lastGroundedTime > 0)     lastGroundedTime     -= Time.unscaledDeltaTime;
            if (lastJumpPressedTime > 0)  lastJumpPressedTime  -= Time.unscaledDeltaTime;
            if (dashCooldownTimer > 0f)   dashCooldownTimer    -= Time.unscaledDeltaTime;
            if (dashWallGraceTimer > 0f)  dashWallGraceTimer   -= Time.unscaledDeltaTime;

            if (isDashing)
            {
                dashTimer -= Time.unscaledDeltaTime;

                bool wallCancelReady = dashWallGraceTimer <= 0f;
                if (wallCancelReady && DashHitWallThisFrame())
                {
                    EndDash();
                }
                else if (dashTimer <= 0f)
                {
                    EndDash();
                }
            }

            TryConsumeJumpBuffered();
        }

        void FixedUpdate()
        {
            Vector2 lv = rb.linearVelocity;

            if (isDashing)
            {
                // Force crisp horizontal dash; keep current vertical (with ceiling clamp below)
                float vx = dashDir * dashSpeed;
                float vy = lv.y;

                // Ceiling clamp
                if (sensors && sensors.IsCeilingBlocked && vy > 0f)
                    vy = 0f;

                rb.linearVelocity = new Vector2(vx, vy);
                return; // ignore normal movement while dashing
            }

            float targetSpeed = GetEffectiveMoveSpeed();
            float xTarget = moveInput.x * targetSpeed;

            float accel = isGrounded ? groundAccel : airAccel;
            float vxMove = Mathf.MoveTowards(lv.x, xTarget, accel * Time.fixedDeltaTime);

            // Anti-slide bias when grounded
            float vyMove = lv.y;
            if (isGrounded && vyMove < 0f) vyMove = -0.5f;

            rb.linearVelocity = new Vector2(vxMove, vyMove);
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

            // v0 = sqrt(2 * g * h)
            float g = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
            float v0 = Mathf.Sqrt(Mathf.Max(0.01f, 2f * g * desiredJumpHeightUnits));
            v0 *= jumpForceMultiplier;

            Vector2 v = rb.linearVelocity;
            if (v.y < 0f) v.y = 0f; // snappy takeoff
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

        // ===== Dash helpers =====
        void TryStartDash()
        {
            if (!dashEnabled) return;
            if (isDashing) return;
            if (dashCooldownTimer > 0f) return;

            // Must have ground or remaining air charge (if ground dash is allowed)
            bool canDash = (allowGroundDash && isGrounded) || (!isGrounded && dashesRemaining > 0);
            if (!canDash) return;

            // Direction priority:
            // 1) Held move key (intended run direction)
            // 2) Current horizontal velocity sign (keeps momentum flow)
            // 3) Cursor side, if mouse available
            // 4) Last non-zero move sign, else facing
            float dir = 0f;
            if (Mathf.Abs(moveInput.x) >= dashInputDeadzone)          dir = Mathf.Sign(moveInput.x);
            else if (Mathf.Abs(rb.linearVelocity.x) > 0.01f)          dir = Mathf.Sign(rb.linearVelocity.x);
            else
            {
                var cam = Camera.main;
                if (cam && Mouse.current != null)
                {
                    Vector3 mp = Mouse.current.position.ReadValue();
                    Vector2 world = cam.ScreenToWorldPoint(mp);
                    float dx = world.x - transform.position.x;
                    if (Mathf.Abs(dx) >= 0.001f) dir = Mathf.Sign(dx);
                }
            }
            if (dir == 0f) dir = (lastMoveSign != 0 ? lastMoveSign : lastFacingSign);

            dashDir = dir > 0f ? 1 : -1;
            isDashing = true;
            dashTimer = dashDuration;
            dashWallGraceTimer = dashWallCancelDelay;

            // Start-of-dash vertical handling
            Vector2 v = rb.linearVelocity;

            // If airborne and you asked to cut upward speed, make the dash crisp
            if (!isGrounded && dashCutUpwardVelocityAtStart && v.y > 0f)
                v.y = 0f;

            // If ground dash, add a tiny lift to clear the floor & avoid immediate cancel from floor edges
            if (isGrounded && allowGroundDash && dashGroundLiftVy > 0f
                && !(sensors && sensors.IsCeilingBlocked))
            {
                v.y = Mathf.Max(v.y, dashGroundLiftVy);
            }

            rb.linearVelocity = v;

            // consume air charge only if airborne
            if (!isGrounded && dashesRemaining > 0) dashesRemaining--;
        }

        void EndDash()
        {
            isDashing = false;
            dashCooldownTimer = dashCooldown;

            // stop horizontal burst cleanly; preserve vertical
            Vector2 v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
        }

        // Use shape-cast to detect a real wall in front of the player while dashing.
        // Ignores floors/ramps because we check the hit normal.x magnitude/sign.
        bool DashHitWallThisFrame()
        {
            if (!rb) return false;

            Vector2 dir = new Vector2(dashDir, 0f);
            // Cast for the distance we’d cover in the next physics step, plus a small skin
            float dist = Mathf.Max(0.005f, dashSpeed * Time.fixedDeltaTime + dashCastSkin);

            int count = rb.Cast(dir, dashFilter, dashCastHits, dist);
            for (int i = 0; i < count; i++)
            {
                var h = dashCastHits[i];
                Vector2 n = h.normal;
                // wall if normal.x is opposite to dash direction and sufficiently horizontal
                if (dashDir > 0 && n.x <= -dashWallNormalDotMin) return true;
                if (dashDir < 0 && n.x >=  dashWallNormalDotMin) return true;
            }
            return false;
        }

        // ===== Input =====
        public void OnMove(InputAction.CallbackContext ctx)
        {
            moveInput = ctx.ReadValue<Vector2>();
            if (Mathf.Abs(moveInput.x) > 0.05f)
            {
                lastMoveSign = moveInput.x > 0f ? 1 : -1;
                lastFacingSign = lastMoveSign; // keep facing fallback sensible for ground dash
            }
        }

        public void OnJump(InputAction.CallbackContext ctx) { if (ctx.performed) lastJumpPressedTime = jumpBuffer; }
        public void OnSprint(InputAction.CallbackContext ctx) { if (ctx.performed) wantSprint = true; if (ctx.canceled) wantSprint = false; }
        public void OnInteract(InputAction.CallbackContext ctx) { /* stub */ }

        public void OnDash(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            TryStartDash(); // groundwork only; VFX/anim to come
        }

        // ===== Progression APIs =====
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