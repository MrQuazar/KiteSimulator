using UnityEngine;
public class PullMechanics : MonoBehaviour
{
    [Header("Face control")]
    public Vector3 faceRotationOffset = new Vector3(-45f, 0f, 0f);

    [Header("Thrust settings")]
    [Tooltip("Target speed while holding the thrust key (m/s)")]
    public float thrustSpeed = 25f;
    [Tooltip("How quickly velocity moves toward target (meters/second change per second). Higher = snappier.")]
    public float accel = 80f;

    [Header("Input")]
    public KeyCode thrustKey = KeyCode.DownArrow;

    [Header("Release / damping settings")]
    [Tooltip("How long (seconds) to smoothly fade thrust influence after key release.")]
    public float releaseFadeTime = 0.25f;
    [Tooltip("Temporary extra linear drag applied on release to damp oscillation.")]
    public float releaseExtraDrag = 4f;
    [Tooltip("How long (seconds) extra drag stays active after release.")]
    public float releaseDragDuration = 0.3f;
    [Tooltip("If true, will remove velocity component that points back into the face direction on release.")]
    public bool stripBackwardVelocityOnRelease = true;
    [Tooltip("How strongly to strip that backward component (0..1). 1 = remove entirely, 0.5 = half removed.")]
    [Range(0f,1f)] public float backwardStripStrength = 0.9f;

    // internals
    private Rigidbody rb;
    private bool wasThrusting = false;
    private float releaseTimer = 0f;
    private float dragTimer = 0f;
    private float savedDrag = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            Debug.LogError("PullHoldMechanics requires a Rigidbody on the same GameObject.");
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        bool isThrusting = Input.GetKey(thrustKey);

        if (isThrusting)
        {
            // reset release timers
            releaseTimer = 0f;
            wasThrusting = true;

            Vector3 dir = GetFaceDirection();
            Vector3 targetVel = dir.normalized * thrustSpeed;
            float maxDelta = accel * Time.fixedDeltaTime;
            rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, targetVel, maxDelta);
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            // If we just released this frame, start fade/damping behaviour
            if (wasThrusting)
            {
                wasThrusting = false;
                releaseTimer = 0f;
                // save and increase drag
                savedDrag = rb.linearDamping;
                rb.linearDamping = savedDrag + releaseExtraDrag;
                dragTimer = 0f;

                // Optionally remove backward velocity component immediately (helps strong rebounds)
                if (stripBackwardVelocityOnRelease)
                {
                    Vector3 faceDir = GetFaceDirection().normalized;
                    // component of velocity that points *towards* -faceDir (i.e. opposite of face)
                    float backComp = Vector3.Dot(rb.linearVelocity, -faceDir);
                    if (backComp > 0f)
                    {
                        // remove or reduce that component
                        Vector3 backVector = -faceDir * backComp;
                        Vector3 removal = backVector * backwardStripStrength;
                        rb.linearVelocity -= removal;
                    }
                }
            }

            // While fading -> optionally smooth additional velocity adjustments (we'll just run a tiny fade)
            if (releaseTimer < releaseFadeTime)
            {
                releaseTimer += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(releaseTimer / releaseFadeTime);
                
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, t);

                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z), t * 0.2f);
            }

            // Restore drag after short duration
            if (dragTimer < releaseDragDuration)
            {
                dragTimer += Time.fixedDeltaTime;
                if (dragTimer >= releaseDragDuration)
                {
                    rb.linearDamping = savedDrag;
                }
            }
        }
    }

    private Vector3 GetFaceDirection()
    {
        Quaternion offsetRot = Quaternion.Euler(faceRotationOffset);
        return (transform.rotation * offsetRot) * Vector3.forward;
    }
}
