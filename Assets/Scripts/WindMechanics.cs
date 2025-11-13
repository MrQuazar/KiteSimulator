using NUnit.Framework;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WindMechanics : MonoBehaviour
{
    [Header("References")]
    private Rigidbody kiteRb;

    [Tooltip("If not assigned, will try to find a GameObject named 'Phirki' at Start.")]
    public Transform ropeOrigin;

    [Header("Wind Settings")]
    [Tooltip("Upward lift force applied to keep the kite flying.")]
    public float liftForce = 0f;
    public float heightLimit = 40f;

    [Tooltip("Forward push simulating wind direction.")]
    public float windForce = 0f;

    [Tooltip("Direction of the wind (e.g. Vector3.forward for Z-axis wind).")]
    public Vector3 windDirection = Vector3.forward;

    [Header("Control Settings")]
    [Tooltip("How much lift/wind changes when pressing up/down arrows.")]
    public float liftChangeRate = 2f;

    // Exposed state for other scripts
    [HideInInspector] public bool IsHeld { get; private set; } = false;

    void Start()
    {
        kiteRb = GetComponent<Rigidbody>();
        if (kiteRb == null)
            Debug.LogError("Rigidbody not found! Please attach this script to the ConnectionFragment with a Rigidbody.");

        if (ropeOrigin == null)
        {
            GameObject ph = GameObject.Find("Phirki");
            if (ph != null) ropeOrigin = ph.transform;
        }

        if (ropeOrigin == null)
            Debug.LogWarning("ropeOrigin not assigned and 'Phirki' not found. Orbit will use world origin (0,0,0).");
    }

    void Update()
    {
        // wind/lift only change when not held
        if (!IsHeld)
        {
            // Decrease lift when DownArrow pressed
            if (false && Input.GetKey(KeyCode.DownArrow))
            {
                liftForce -= liftChangeRate * Time.deltaTime;
                windForce -= liftChangeRate * Time.deltaTime;
                if (liftForce < 0f) liftForce = 0f;
                if (windForce < 0f) windForce = 0f;
            }
            else if (!Input.GetKey(KeyCode.DownArrow) && transform.position.y < heightLimit)
            {
                liftForce += liftChangeRate * Time.deltaTime;
                windForce += liftChangeRate * Time.deltaTime;
            }
        }
    }

    void FixedUpdate()
    {
        if (kiteRb == null) return;
            // Regular physics-driven flight
            Vector3 lift = Vector3.up * liftForce;
            Vector3 wind = windDirection.normalized * windForce;
            Vector3 totalForce = lift + wind;
            kiteRb.AddForce(totalForce, ForceMode.Force);
    }

    /// <summary>
    /// Call from another script to set the held state (e.g. when player holds the rope).
    /// This will stop WindMechanics from increasing lift/wind.
    /// </summary>
    public void SetHeld(bool held)
    {
        if (IsHeld == held) return;
        IsHeld = held;

        if (IsHeld)
        {
            //stop vertical velocity so kite doesn't keep rising
            if (kiteRb != null)
            {
                Vector3 v = kiteRb.linearVelocity;
                v.y = 0f;
                kiteRb.linearVelocity = v;
            }
        }
    }
}
