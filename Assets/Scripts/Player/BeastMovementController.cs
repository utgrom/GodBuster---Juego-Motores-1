using Unity.Cinemachine;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BeastMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody body;
    [SerializeField] private Transform orientationRoot;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform cameraReference;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private CinemachineCamera defaultCamera;
    [SerializeField] private CinemachineCamera chargeCamera;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 12f;
    [SerializeField] private float sprintRampUpTime = 1f;
    [SerializeField] private float groundAcceleration = 30f;
    [SerializeField] private float groundDeceleration = 35f;
    [SerializeField] private float airAcceleration = 12f;
    [SerializeField] private float rotationSmoothing = 12f;
    [SerializeField] private float idleSpeedThreshold = 0.2f;

    [Header("Jumping")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBuffer = 0.15f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float gravityScale = 1.5f;

    [Header("Friction")]
    [SerializeField] private float groundedDrag = 6f;
    [SerializeField] private float airborneDrag = 0.2f;

    [Header("Charge Settings")]
    [SerializeField] private float idleTimeBeforeCharge = 1f;
    [SerializeField] private float minChargeToLockCamera = 200f;
    [SerializeField] private float chargeGoal = 2000f;

    [SerializeField] private float chargeMax = 4000f;
    [SerializeField] private float dashCameraFov = 130f;
    [SerializeField] private float dashCameraFovReturnTime = 2f;
    [SerializeField] private float chargePerTap = 50f;
    [SerializeField] private float baseChargeDecayPerSecond = 10f;
    [SerializeField] private float maxChargeDecayPerSecond = 20f;

    [Header("Dash Settings")]
    [SerializeField] private float dashInitialImpulse = 20f;
    [SerializeField] private float dashImpulsePerCharge = 0.02f;
    [SerializeField] private float dashSustainAcceleration = 45f;
    [SerializeField] private float dashSustainScale = 0.02f;
    [SerializeField] private float dashChargeDrainPerSecond = 200f;
    [SerializeField] private float dashMinDuration = 0.35f;
    [SerializeField] private float dashMaxDuration = 1.2f;
    [SerializeField] private float dashCooldown = 0.6f;
    [SerializeField] private float dashJumpConvertWindow = 0.25f;
    [SerializeField] private float dashLeapVerticalBoost = 6f;

    [Header("Stability")]
    [SerializeField] private bool useMoveRotationForBody = true;
    [SerializeField] private bool dampAngularVelocity = true;
    [SerializeField] private float angularDampingFactor = 1f;

    public float CurrentCharge => chargeMeter;
    public float NormalizedCharge => chargeMax > 0f ? chargeMeter / chargeMax : 0f;
    public bool IsCharging => isCharging;
    public bool IsDashing => isDashing;

    // Exposed read-only properties for UI/debug
    public float ChargeGoal => chargeGoal;
    public float ChargeMax => chargeMax;
    public float MinChargeThreshold => minChargeToLockCamera;
    public float IdleTimeBeforeCharge => idleTimeBeforeCharge;
    public bool IdleReady => idleTimer >= idleTimeBeforeCharge;
    public float CurrentChargeDecayPerSecond => Mathf.Lerp(baseChargeDecayPerSecond, maxChargeDecayPerSecond, NormalizedCharge);
    public bool IsChargeCameraActive => isChargeCameraActive;
    public float ChargePerTap => chargePerTap;

    private Vector2 moveInput;
    private bool sprintHeld;
    private bool sprintPressedThisFrame;
    private bool jumpPressedThisFrame;
    private bool forwardPressedThisFrame;
    private bool forwardWasHeldLastFrame;

    private float sprintBlend;
    [SerializeField] private float chargeMeter;
    private float idleTimer;
    private float jumpBufferTimer;
    private float dashCooldownTimer;

    private bool isCharging;
    private Vector3 chargeFacing;

    private bool isDashing;
    private float dashElapsedTime;
    private float dashDuration;
    private float dashChargeRemaining;
    private float dashChargeInitial;
    private Vector3 dashDirection;
    private bool dashLeapTriggered;
    private bool dashJumpQueued;

    private bool isGrounded;
    private float lastGroundedTime;

    private bool isChargeCameraActive;
    private float chargeCameraBaseFov = 60f;
    private float chargeCameraFovTimer;

    private CinemachineBasicMultiChannelPerlin chargeCameraNoise;
    private float chargeNoiseBaseAmplitude;
    [SerializeField] private AnimationCurve chargeNoiseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    private int defaultCameraPriority;
    private int chargeCameraPriority;

    private Behaviour[] defaultCameraInputBehaviours;
    private bool[] defaultCameraInputEnabledStates;

    private Quaternion pendingRotation;
    private bool hasPendingRotation;

    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int AnimIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int AnimJumped = Animator.StringToHash("Jumped");
    private static readonly int AnimOnAir = Animator.StringToHash("OnAir");

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (orientationRoot == null)
        {
            orientationRoot = transform;
        }

        if (groundCheck == null)
        {
            groundCheck = transform;
        }

        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        chargeGoal = Mathf.Max(1f, chargeGoal);
        chargeMax = Mathf.Max(chargeGoal, chargeMax);


        CacheCameraDefaults();
        SetChargeCameraActive(false);
        pendingRotation = body.rotation;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.Enable();
        }
        if (sprintAction != null && sprintAction.action != null)
        {
            sprintAction.action.Enable();
        }
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.Disable();
        }
        if (sprintAction != null && sprintAction.action != null)
        {
            sprintAction.action.Disable();
        }
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.Disable();
        }
        hasPendingRotation = false;
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        ReadInput();
        UpdateIdleTimer(delta);
        UpdateChargeState(delta);
        UpdateJumpBuffer(delta);
        UpdateDashLogic(delta);
        UpdateCameraState();
        UpdateOrientation(delta);
        UpdateChargeCameraFov(delta);
        UpdateAnimatorParameters();
    }

    private void FixedUpdate()
    {
        float delta = Time.fixedDeltaTime;

        if (useMoveRotationForBody && hasPendingRotation)
        {
            body.MoveRotation(pendingRotation);
            hasPendingRotation = false;
        }

        UpdateGroundedState();
        body.linearDamping = isGrounded ? groundedDrag : airborneDrag;

        if (isDashing)
        {
            HandleDashPhysics(delta);
            ApplyAdditionalGravity();
            if (dampAngularVelocity)
            {
                Vector3 w = body.angularVelocity;
                w *= Mathf.Max(0f, 1f - angularDampingFactor * delta);
                body.angularVelocity = w;
            }
            return;
        }

        Vector3 desiredDirection = isCharging ? Vector3.zero : GetDesiredPlanarDirection();
        HandleMovement(desiredDirection, delta);
        HandleJump();
        ApplyAdditionalGravity();

        if (dampAngularVelocity)
        {
            Vector3 w = body.angularVelocity;
            w *= Mathf.Max(0f, 1f - angularDampingFactor * delta);
            body.angularVelocity = w;
        }
    }

    private void ReadInput()
    {
        var move = moveAction != null ? moveAction.action : null;
        var sprint = sprintAction != null ? sprintAction.action : null;
        var jump = jumpAction != null ? jumpAction.action : null;

        moveInput = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        sprintHeld = sprint != null && sprint.IsPressed();
        sprintPressedThisFrame = sprint != null && sprint.WasPressedThisFrame();
        jumpPressedThisFrame = jump != null && jump.WasPressedThisFrame();

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        bool forwardHeld = moveInput.y > 0.25f;
        forwardPressedThisFrame = forwardHeld && !forwardWasHeldLastFrame;
        forwardWasHeldLastFrame = forwardHeld;
    }

    private void UpdateIdleTimer(float delta)
    {
        Vector3 horizontalVelocity = GetHorizontalVelocity(body.linearVelocity);
        bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;
        bool effectivelyIdle = horizontalVelocity.magnitude <= idleSpeedThreshold && !hasMoveInput;

        if (effectivelyIdle && !isDashing)
        {
            idleTimer += delta;
        }
        else
        {
            idleTimer = 0f;
        }

        if (!isCharging)
        {
            float targetBlend = sprintHeld ? 1f : 0f;
            float ramp = Mathf.Approximately(sprintRampUpTime, 0f) ? 1f : delta / sprintRampUpTime;
            sprintBlend = Mathf.MoveTowards(sprintBlend, targetBlend, ramp);
        }
    }

    private void StartChargingWithTap()
    {
        isCharging = true;
        chargeFacing = GetCurrentForward();
        sprintBlend = 0f;
        dashElapsedTime = 0f;
        ApplyChargeTap();
    }

    private void ApplyChargeTap()
    {
        float previous = Mathf.Max(0f, chargeMeter);
        chargeMeter = Mathf.Min(chargeMax, previous + chargePerTap);
        Debug.Log($"[Charge Tap] {previous:F0} -> {chargeMeter:F0}", this);
        if (chargeMeter >= minChargeToLockCamera)
        {
            SetChargeCameraActive(true);
        }

        UpdateChargeNoise();
    }

    private void UpdateChargeState(float delta)
    {
        if (isDashing)
        {
            chargeMeter = dashChargeRemaining;
            return;
        }

        bool noMoveInput = moveInput.sqrMagnitude <= 0.0001f;
        float horizontalSpeed = GetHorizontalVelocity(body.linearVelocity).magnitude;
        bool readyToStart = IdleReady && noMoveInput && horizontalSpeed <= idleSpeedThreshold && dashCooldownTimer <= 0f;
        bool forwardTap = forwardPressedThisFrame;

        if (!isCharging)
        {
            if (!readyToStart)
            {
                if (chargeMeter > 0f)
                {
                    Debug.Log("[Charge Reset] Conditions not met to start charge.", this);
                    chargeMeter = 0f;
                }
                return;
            }

            if (sprintPressedThisFrame)
            {
                StartChargingWithTap();
            }
            return;
        }

        bool stillStationary = noMoveInput && horizontalSpeed <= idleSpeedThreshold;

        if (forwardTap)
        {
            if (chargeMeter >= chargeGoal)
            {
                Debug.Log($"[Charge Dash] Forward pressed with {chargeMeter:F0}", this);
                StartDash();
                return;
            }

            Debug.Log($"[Charge Cancel] Forward pressed but only {chargeMeter:F0}/{chargeGoal:F0}", this);
            CancelCharge();
            return;
        }

        if (!stillStationary)
        {
            Debug.Log("[Charge Cancel] Movement detected while charging.", this);
            CancelCharge();
            return;
        }

        if (sprintPressedThisFrame)
        {
            ApplyChargeTap();
        }

        float normalized = Mathf.Clamp01(chargeMeter / Mathf.Max(1f, chargeMax));
        float decayRate = Mathf.Lerp(baseChargeDecayPerSecond, maxChargeDecayPerSecond, normalized);
        chargeMeter = Mathf.Max(0f, chargeMeter - decayRate * delta);

        if (chargeMeter < minChargeToLockCamera)
        {
            SetChargeCameraActive(false);
        }

        UpdateChargeNoise();

        if (chargeMeter <= Mathf.Epsilon)
        {
            CancelCharge();
        }
    }

    private void UpdateJumpBuffer(float delta)
    {
        if (jumpPressedThisFrame)
        {
            jumpBufferTimer = jumpBuffer;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - delta);
        }
    }

    private void UpdateDashLogic(float delta)
    {
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - delta);
        }

        if (isDashing && jumpPressedThisFrame && !dashLeapTriggered && dashElapsedTime <= dashJumpConvertWindow)
        {
            dashJumpQueued = true;
        }
    }

    private void UpdateCameraState()
    {
        bool useChargeCam = (isCharging && chargeMeter >= minChargeToLockCamera) || isDashing;
        SetChargeCameraActive(useChargeCam);

        if (useChargeCam)
        {
            AlignChargeCamera();
        }
    }

    private void UpdateOrientation(float delta)
    {
        Transform target = orientationRoot != null ? orientationRoot : transform;
        Vector3 forward;

        if (isDashing)
        {
            forward = dashDirection;
        }
        else if (isCharging)
        {
            forward = chargeFacing;
        }
        else
        {
            forward = GetDesiredPlanarDirection();
            if (forward.sqrMagnitude <= 0.0001f)
            {
                Vector3 horizontalVelocity = GetHorizontalVelocity(body.linearVelocity);
                forward = horizontalVelocity.sqrMagnitude > 0.01f ? horizontalVelocity.normalized : target.forward;
            }
        }

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        if (useMoveRotationForBody && target == transform)
        {
            pendingRotation = Quaternion.Slerp(body.rotation, desiredRotation, delta * rotationSmoothing);
            hasPendingRotation = true;
        }
        else
        {
            target.rotation = Quaternion.Slerp(target.rotation, desiredRotation, delta * rotationSmoothing);
        }

        if (visualRoot != null)
        {
            Quaternion referenceRotation = (useMoveRotationForBody && target == transform) ? pendingRotation : target.rotation;
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, referenceRotation, delta * rotationSmoothing * 1.5f);
        }
    }

    private void HandleMovement(Vector3 desiredDirection, float delta)
    {
        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = GetHorizontalVelocity(velocity);
        Vector3 vertical = Vector3.up * velocity.y;

        float targetSpeed = Mathf.Lerp(walkSpeed, sprintSpeed, sprintBlend);
        Vector3 targetHorizontal = desiredDirection * targetSpeed;
        float accel = (targetHorizontal.sqrMagnitude >= horizontal.sqrMagnitude)
            ? (isGrounded ? groundAcceleration : airAcceleration)
            : (isGrounded ? groundDeceleration : airAcceleration);

        Vector3 newHorizontal = Vector3.MoveTowards(horizontal, targetHorizontal, accel * delta);
        body.linearVelocity = newHorizontal + vertical;
    }

    private void HandleJump()
    {
        if (jumpBufferTimer <= 0f)
        {
            return;
        }

        bool canJump = isGrounded || (Time.time - lastGroundedTime) <= coyoteTime;
        if (!canJump)
        {
            return;
        }

        jumpBufferTimer = 0f;

        Vector3 velocity = body.linearVelocity;
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
        }

        body.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
        body.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        playerAnimator?.SetTrigger(AnimJumped);

        if (isCharging)
        {
            CancelCharge();
        }
    }

    private void HandleDashPhysics(float delta)
    {
        dashElapsedTime += delta;

        if (dashChargeRemaining > 0f)
        {
            float sustainForce = dashSustainAcceleration + dashSustainScale * dashChargeInitial;
            body.AddForce(dashDirection * sustainForce, ForceMode.Acceleration);
            dashChargeRemaining = Mathf.Max(0f, dashChargeRemaining - dashChargeDrainPerSecond * delta);
            chargeMeter = dashChargeRemaining;
            UpdateChargeNoise();
        }

        if (dashJumpQueued && !dashLeapTriggered)
        {
            ConvertDashToLeap();
        }

        if (dashElapsedTime >= dashDuration || dashChargeRemaining <= 0f)
        {
            FinishDash();
        }
    }

    private void ApplyAdditionalGravity()
    {
        if (gravityScale <= 1f)
        {
            return;
        }

        Vector3 extraGravity = Physics.gravity * (gravityScale - 1f);
        body.AddForce(extraGravity, ForceMode.Acceleration);
    }

    private void UpdateGroundedState()
    {
        Vector3 origin = (groundCheck != null ? groundCheck.position : transform.position) + Vector3.up * 0.05f;
        float radius = Mathf.Max(0.05f, groundCheckRadius);
        float distance = Mathf.Max(groundCheckDistance, 0.05f);

        bool hitGround = Physics.SphereCast(origin, radius, Vector3.down, out _, distance, groundLayers, QueryTriggerInteraction.Ignore);
        if (hitGround)
        {
            lastGroundedTime = Time.time;
        }

        isGrounded = hitGround;
    }


    private void CancelCharge()
    {
        if (!isCharging)
        {
            return;
        }

        isCharging = false;
        chargeMeter = 0f;
        SetChargeCameraActive(false);
    }

    private void StartDash()
    {
        isCharging = false;
        isDashing = true;
        dashLeapTriggered = false;
        dashJumpQueued = false;
        dashElapsedTime = 0f;
        dashChargeInitial = Mathf.Clamp(chargeMeter, 0f, chargeMax);
        dashChargeRemaining = dashChargeInitial;
        dashDuration = Mathf.Lerp(dashMinDuration, dashMaxDuration, Mathf.Clamp01(dashChargeInitial / Mathf.Max(1f, chargeMax)));
        dashDirection = chargeFacing.sqrMagnitude > 0.001f ? chargeFacing.normalized : GetCurrentForward();
        dashCooldownTimer = dashCooldown;
        chargeMeter = dashChargeRemaining;

        SetChargeCameraActive(true);
        AlignChargeCamera();
        UpdateChargeNoise();
        SetChargeCameraFov(dashCameraFov);
        chargeCameraFovTimer = Mathf.Max(0f, dashCameraFovReturnTime);
        if (chargeCameraFovTimer <= 0f)
        {
            ResetChargeCameraFov();
        }
        ApplyDashImpulse();
    }

    private void ApplyDashImpulse()
    {
        Vector3 forward = dashDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = GetCurrentForward();
        }
        dashDirection = forward.normalized;

        float impulse = dashInitialImpulse + dashImpulsePerCharge * dashChargeInitial;
        body.AddForce(dashDirection * impulse, ForceMode.VelocityChange);
    }

    private void ConvertDashToLeap()
    {
        dashLeapTriggered = true;
        dashJumpQueued = false;

        Vector3 forward = dashDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = GetCurrentForward();
        }
        forward = forward.normalized;

        float angleRad = Mathf.Deg2Rad * 60f;
        Vector3 leapDirection = (forward * Mathf.Cos(angleRad) + Vector3.up * Mathf.Sin(angleRad)).normalized;
        float currentSpeed = GetHorizontalVelocity(body.linearVelocity).magnitude;
        float targetSpeed = Mathf.Max(currentSpeed, dashInitialImpulse);

        Vector3 newVelocity = leapDirection * targetSpeed;
        newVelocity.y = Mathf.Max(newVelocity.y, dashLeapVerticalBoost);
        body.linearVelocity = newVelocity;

        dashChargeRemaining = 0f;
        chargeMeter = 0f;
        UpdateChargeNoise();
    }

    private void FinishDash()
    {
        if (!isDashing)
        {
            return;
        }

        isDashing = false;
        dashChargeRemaining = 0f;
        chargeMeter = 0f;
        dashJumpQueued = false;
        dashLeapTriggered = false;
        SetChargeCameraActive(false);
    }

    private void SetChargeCameraActive(bool enable)
    {
        if (enable == isChargeCameraActive)
        {
            return;
        }

        isChargeCameraActive = enable;

        if (enable)
        {
            if (defaultCamera != null)
            {
                SetDefaultCameraInputEnabled(false);
                defaultCamera.Priority = defaultCameraPriority;
            }

            if (chargeCamera != null)
            {
                chargeCameraNoise = chargeCamera.GetComponentInChildren<CinemachineBasicMultiChannelPerlin>();
                if (chargeCameraNoise != null)
                {
                    chargeNoiseBaseAmplitude = chargeCameraNoise.AmplitudeGain;
                }

                chargeCameraBaseFov = chargeCamera.Lens.FieldOfView;

                int baselinePriority = defaultCamera != null ? defaultCamera.Priority : chargeCameraPriority;
                chargeCamera.Priority = Mathf.Max(baselinePriority, chargeCameraPriority) + 1;
            }
        }
        else
        {
            if (chargeCamera != null)
            {
                chargeCamera.Priority = chargeCameraPriority;
            }

            if (defaultCamera != null)
            {
                defaultCamera.Priority = defaultCameraPriority;
                SetDefaultCameraInputEnabled(true);
            }
        }

        UpdateChargeNoise();
        ResetChargeCameraFov();
    }
    private void AlignChargeCamera()
    {
        Vector3 forward = isDashing ? dashDirection : chargeFacing;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = GetCurrentForward();
        }

        chargeCamera.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private static readonly string[] CinemachineInputBehaviourNames =
    {
        "CinemachineInputAxisController",
        "CinemachineInputProvider",
        "CinemachineInputAxisDriver"
    };

    private static bool IsCinemachineInputBehaviour(Behaviour behaviour)
    {
        if (behaviour == null)
        {
            return false;
        }

        string typeName = behaviour.GetType().Name;
        for (int i = 0; i < CinemachineInputBehaviourNames.Length; i++)
        {
            if (typeName == CinemachineInputBehaviourNames[i])
            {
                return true;
            }
        }

        return false;
    }

    private void CacheCameraDefaults()
    {
        if (defaultCamera != null)
        {
            defaultCameraPriority = defaultCamera.Priority;
            var behaviours = defaultCamera.GetComponents<Behaviour>();
            var inputCandidates = behaviours.Where(IsCinemachineInputBehaviour).ToList();
            if (inputCandidates.Count > 0)
            {
                defaultCameraInputBehaviours = inputCandidates.ToArray();
                defaultCameraInputEnabledStates = inputCandidates.Select(b => b.enabled).ToArray();
            }
        }

        if (chargeCamera != null)
        {
            chargeCameraPriority = chargeCamera.Priority;
            chargeCameraNoise = chargeCamera.GetComponentInChildren<CinemachineBasicMultiChannelPerlin>();
            if (chargeCameraNoise != null)
            {
                chargeNoiseBaseAmplitude = chargeCameraNoise.AmplitudeGain;
            }

            var lens = chargeCamera.Lens;
            chargeCameraBaseFov = lens.FieldOfView;
        }

        UpdateChargeNoise();
        ResetChargeCameraFov();
    }
    private void SetDefaultCameraInputEnabled(bool enable)
    {
        if (defaultCameraInputBehaviours == null || defaultCameraInputBehaviours.Length == 0)
        {
            return;
        }

        for (int i = 0; i < defaultCameraInputBehaviours.Length; i++)
        {
            Behaviour behaviour = defaultCameraInputBehaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            if (enable)
            {
                if (defaultCameraInputEnabledStates != null && i < defaultCameraInputEnabledStates.Length)
                {
                    behaviour.enabled = defaultCameraInputEnabledStates[i];
                }
                else
                {
                    behaviour.enabled = true;
                }
            }
            else
            {
                if (defaultCameraInputEnabledStates == null || defaultCameraInputEnabledStates.Length != defaultCameraInputBehaviours.Length)
                {
                    defaultCameraInputEnabledStates = new bool[defaultCameraInputBehaviours.Length];
                }
                defaultCameraInputEnabledStates[i] = behaviour.enabled;
                behaviour.enabled = false;
            }
        }
    }

    private void UpdateChargeNoise()
    {
        if (chargeCameraNoise == null)
        {
            return;
        }

        if (!isChargeCameraActive)
        {
            chargeCameraNoise.AmplitudeGain = chargeNoiseBaseAmplitude;
            return;
        }

        float range = Mathf.Max(0.01f, Mathf.Max(chargeGoal, minChargeToLockCamera) - minChargeToLockCamera);
        float t = Mathf.Clamp01((chargeMeter - minChargeToLockCamera) / range);
        float curveValue = chargeNoiseCurve != null ? chargeNoiseCurve.Evaluate(t) : 0f;
        chargeCameraNoise.AmplitudeGain = Mathf.Max(0f, chargeNoiseBaseAmplitude + curveValue);
    }

    private void ResetChargeCameraFov()
    {
        chargeCameraFovTimer = 0f;
        SetChargeCameraFov(chargeCameraBaseFov);
    }

    private void SetChargeCameraFov(float fov)
    {
        if (chargeCamera == null)
        {
            return;
        }

        var lens = chargeCamera.Lens;
        lens.FieldOfView = fov;
        chargeCamera.Lens = lens;
    }

    private void UpdateChargeCameraFov(float delta)
    {
        if (chargeCameraBaseFov <= 0f)
        {
            chargeCameraBaseFov = chargeCamera.Lens.FieldOfView;
        }

        if (chargeCameraFovTimer > 0f)
        {
            chargeCameraFovTimer = Mathf.Max(0f, chargeCameraFovTimer - delta);
            float duration = Mathf.Max(0.0001f, dashCameraFovReturnTime);
            float t = 1f - (chargeCameraFovTimer / duration);
            float fov = Mathf.Lerp(dashCameraFov, chargeCameraBaseFov, Mathf.Clamp01(t));
            SetChargeCameraFov(fov);
        }
        else
        {
            SetChargeCameraFov(chargeCameraBaseFov);
        }
    }

    private void UpdateAnimatorParameters()
    {
        if (playerAnimator == null)
        {
            return;
        }

        bool isMoving = moveInput.sqrMagnitude > 0.0001f;
        bool isChargingState = (isCharging && chargeMeter >= minChargeToLockCamera) || isDashing;
        bool isRunning = isMoving && sprintHeld && isGrounded && !isChargingState && !isDashing;
        bool onAir = !isGrounded;

        playerAnimator.SetBool(AnimIsMoving, isMoving);
        playerAnimator.SetBool(AnimIsRunning, isRunning);
        playerAnimator.SetBool(AnimIsCharging, isChargingState);
        playerAnimator.SetBool(AnimOnAir, onAir);
    }

    private Vector3 GetDesiredPlanarDirection()
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Transform reference = cameraReference;
        if (reference == null && Camera.main != null)
        {
            reference = Camera.main.transform;
        }
        if (reference == null)
        {
            reference = orientationRoot != null ? orientationRoot : transform;
        }

        Vector3 forward = reference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = (orientationRoot != null ? orientationRoot.forward : transform.forward);
        }

        Vector3 right = reference.right;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        return direction;
    }

    private Vector3 GetCurrentForward()
    {
        Transform target = orientationRoot != null ? orientationRoot : transform;
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }
        return forward.normalized;
    }

    private static Vector3 GetHorizontalVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        return velocity;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float radius = Mathf.Max(0.05f, groundCheckRadius);
        float distance = Mathf.Max(groundCheckDistance, 0.05f);
        Vector3 origin = ((groundCheck != null ? groundCheck.position : transform.position) + Vector3.up * 0.05f);
        Vector3 end = origin + Vector3.down * distance;

        bool hitGround = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hitInfo, distance, groundLayers, QueryTriggerInteraction.Ignore);

        Color previous = Gizmos.color;
        Gizmos.color = hitGround ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, radius);

        if (hitGround)
        {
            Gizmos.DrawSphere(hitInfo.point, radius * 0.25f);
        }

        Gizmos.color = previous;
    }
#endif
}