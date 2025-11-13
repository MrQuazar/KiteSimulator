using UnityEngine;

/// <summary>
/// KiteMotion: separate script that handles orbiting/revolving behaviour while the rope is held.
/// It talks to WindMechanics to tell it when the kite is held (so WindMechanics stops increasing wind/lift).
/// Attach this to the kite (same GameObject that has the Rigidbody + WindMechanics).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(WindMechanics))]
public class KiteMotion : MonoBehaviour
{
    [Header("References")]
    public WindMechanics windMechanics; // will auto-assign in Start if null
    private Rigidbody kiteRb;
    public Transform ropeOrigin; // optional override; otherwise taken from windMechanics or Phirki

    [Header("Orbit Settings")]
    [Tooltip("Multiplier to compute orbit radius from current kite height: radius = height * multiplier + baseOrbitRadius")]
    public float orbitRadiusMultiplier = 0.25f;
    [Tooltip("Minimum/base orbit radius added to height-based radius")]
    public float baseOrbitRadius = 2f;
    [Tooltip("Angular speed (radians/sec) of orbit movement around rope origin")]
    public float orbitAngularSpeed = 1.5f;
    [Tooltip("Smoothing factor (0..1) used when moving the rigidbody toward the target orbit position; higher is snappier")]
    [Range(0.01f, 1f)] public float orbitSmoothness = 0.15f;

    [Header("Local Rotation")]
    [Tooltip("Degrees per second to rotate only around the kite's local X axis")]
    public float localXRotationSpeed = 60f;

    [Header("Return-to-rest Rotation")]
    [Tooltip("Degrees per second used to rotate the kite back to its rest rotation after release")]
    public float rotationReturnSpeed = 180f; // deg/sec
    [Tooltip("How close (in degrees) before we consider rotation finished")]
    public float returnThresholdDegrees = 1f;
    
    [Header("Release / Damping")]
    [Tooltip("How long (seconds) we apply extra damping & position anchoring after release")]
    public float releaseDampingDuration = 0.35f;
    [Tooltip("Temporary linear drag while damping (higher = stronger positional damping)")]
    public float releaseLinearDrag = 8f;
    [Tooltip("Temporary angular drag while damping")]
    public float releaseAngularDrag = 8f;
    [Tooltip("How strongly to snap to the release position each FixedUpdate while damping (0..1)")]
    [Range(0f, 1f)] public float releasePositionLerp = 0.6f;

    // Internal state
    private bool isHeld = false;
    private float orbitAngle = 0f; // radians
    private int orbitDirSign = 1;
    private int rotDirSign = 1;

    // return/target rotation
    private Quaternion restRotation;
    private bool isReturning = false;
    
    // internal
    private float releaseTimer = 0f;
    private float originalLinearDrag;
    private float originalAngularDrag;
    private Vector3 releasePosition;

    void Start()
    {
        kiteRb = GetComponent<Rigidbody>();
        if (kiteRb == null) Debug.LogError("KiteMotion requires a Rigidbody.");

        if (windMechanics == null)
            windMechanics = GetComponent<WindMechanics>();

        if (windMechanics == null)
            Debug.LogError("KiteMotion requires WindMechanics on same GameObject (or assign it in inspector).");

        // prefer explicit ropeOrigin, otherwise use windMechanics.ropeOrigin, else find Phirki
        if (ropeOrigin == null)
        {
            if (windMechanics != null && windMechanics.ropeOrigin != null)
                ropeOrigin = windMechanics.ropeOrigin;
            else
            {
                GameObject ph = GameObject.Find("Phirki");
                if (ph != null) ropeOrigin = ph.transform;
            }
        }

        if (ropeOrigin == null)
            Debug.LogWarning("KiteMotion: ropeOrigin not assigned and 'Phirki' not found. Using world origin as center.");

        // Set the rest rotation you want the kite to return to.
        // This matches your spawn rotation Quaternion.Euler(-45f, 90f, 0f)
        restRotation = Quaternion.Euler(-45f, 90f, 0f);
        originalLinearDrag = kiteRb != null ? kiteRb.linearDamping : 0f;
        originalAngularDrag = kiteRb != null ? kiteRb.angularDamping : 0f;
    }

    void Update()
    {
        // handle input for holding the rope
        if (Input.GetKeyDown(KeyCode.Space))
        {
            BeginHold();
        }
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            EndHold();
        }
    }

    void FixedUpdate()
    {
        if (isHeld)
        {
            DoOrbitMotion();
            // while held we are not returning
            isReturning = false;
        }
        else
        {
            // If a damping window is active after release, hold position near releasePosition
            if (releaseTimer > 0f)
            {
                // Move the rigidbody toward the releasePosition each FixedUpdate to prevent lateral boomerang movement.
                // Using Lerp gives a smooth but strong snap; you can set releasePositionLerp closer to 1 to be firmer.
                Vector3 lockedPos = Vector3.Lerp(kiteRb.position, releasePosition, releasePositionLerp);
                kiteRb.MovePosition(lockedPos);

                // Keep returning rotation active while damped
                if (isReturning)
                    DoReturnRotation();

                // decrement timer
                releaseTimer -= Time.fixedDeltaTime;

                // when timer ends, restore original drags (and let physics continue)
                if (releaseTimer <= 0f)
                {
                    kiteRb.linearDamping = originalLinearDrag;
                    kiteRb.angularDamping = originalAngularDrag;

                    // small safety: zero minor velocities left
                    kiteRb.linearVelocity = Vector3.zero;
                    kiteRb.angularVelocity = Vector3.zero;
                }

                return;
            }

            // Normal return behavior if not currently damped (and not being pulled)
            if (isReturning)
            {
                DoReturnRotation();
            }
        }
    }


    void BeginHold()
    {
        if (isHeld) return;
        isHeld = true;

        // notify wind mechanics so it stops incrementing lift/wind
        if (windMechanics != null) windMechanics.SetHeld(true);

        // randomize orbit/rotation directions
        orbitDirSign = (Random.value > 0.5f) ? 1 : -1;
        rotDirSign = (Random.value > 0.5f) ? 1 : -1;

        // initialize orbitAngle from current position so motion is continuous
        Vector3 originPos = ropeOrigin != null ? ropeOrigin.position : Vector3.zero;
        Vector3 toKite = transform.position - originPos;
        orbitAngle = Mathf.Atan2(toKite.z, toKite.x);

        // remove vertical velocity so it doesn't keep rising
        Vector3 v = kiteRb.linearVelocity;
        v.y = 0f;
        kiteRb.linearVelocity = v;

        // stop any return that may be in progress
        isReturning = false;
    }

    void EndHold()
    {
        if (!isHeld) return;
        isHeld = false;

        if (windMechanics != null) windMechanics.SetHeld(false);

        // start returning to rest rotation
        isReturning = true;

        // capture the current position so we can try to keep the kite around this place
        releasePosition = kiteRb.position;

        // kill residual velocity/rotation to prevent boomerang effect
        kiteRb.linearVelocity = Vector3.zero;
        kiteRb.angularVelocity = Vector3.zero;

        // apply temporary damping
        releaseTimer = releaseDampingDuration;
        kiteRb.linearDamping = releaseLinearDrag;
        kiteRb.angularDamping = releaseAngularDrag;
    }


    void DoOrbitMotion()
    {
        Vector3 originPos = ropeOrigin != null ? ropeOrigin.position : Vector3.zero;

        // keep current kite altitude (so orbit is horizontal at that height)
        float currentHeight = transform.position.y;

        // compute radius
        float radius = currentHeight * orbitRadiusMultiplier + baseOrbitRadius;
        radius = Mathf.Max(0.01f, radius);

        // advance angle (radians)
        orbitAngle += orbitDirSign * orbitAngularSpeed * Time.fixedDeltaTime;

        // target position on XZ circle (centered at originPos at height currentHeight)
        float x = Mathf.Cos(orbitAngle) * radius;
        float z = Mathf.Sin(orbitAngle) * radius;
        Vector3 targetPos = originPos + new Vector3(x, currentHeight, z);

        // smooth movement toward target
        Vector3 smoothed = Vector3.Lerp(kiteRb.position, targetPos, orbitSmoothness);
        kiteRb.MovePosition(smoothed);

        // rotation: only alter local X axis each physics frame
        float deltaRotDeg = rotDirSign * localXRotationSpeed * Time.fixedDeltaTime;
        Quaternion localDelta = Quaternion.Euler(deltaRotDeg, 0f, 0f);
        Quaternion newRot = kiteRb.rotation * localDelta;
        kiteRb.MoveRotation(newRot);
    }

    void DoReturnRotation()
    {
        if (Input.GetKey(KeyCode.DownArrow)) return;
        // Rotate the rigidbody from its current rotation towards restRotation at rotationReturnSpeed degrees / second
        Quaternion current = kiteRb.rotation;
        // compute max degrees we can rotate this FixedUpdate
        float maxDegrees = rotationReturnSpeed * Time.fixedDeltaTime;
        Quaternion next = Quaternion.RotateTowards(current, restRotation, maxDegrees);
        kiteRb.MoveRotation(next);

        // check angle remaining
        float angleRemaining = Quaternion.Angle(next, restRotation);
        if (angleRemaining <= returnThresholdDegrees)
        {
            // snap to exact rest rotation to avoid tiny residual
            kiteRb.MoveRotation(restRotation);
            isReturning = false;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw the current orbit radius at the kite's height (editor-only)
        if (!Application.isPlaying && ropeOrigin == null)
        {
            Transform ro = ropeOrigin;
            if (ro == null)
            {
                GameObject ph = GameObject.Find("Phirki");
                if (ph != null) ro = ph.transform;
            }

            if (ro != null && transform != null)
            {
                float h = transform.position.y;
                float r = h * orbitRadiusMultiplier + baseOrbitRadius;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(ro.position + Vector3.up * h, r);
            }
        }
    }
#endif
}
