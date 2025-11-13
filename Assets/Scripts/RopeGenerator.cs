using System.Collections.Generic;
using UnityEngine;

public class RopeGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform fixedPoint;
    public GameObject ropeSegmentPrefab;
    public GameObject kitePrefab;

    [Header("Rope Settings")]
    public float segmentLength = 0.3f;
    public float segmentMass = 0.05f;
    public int initialSegmentCount = 3;

    [Header("Joint Settings")]
    public float angularYLimit = 45f;
    public float angularZLimit = 45f;
    public float breakForce = Mathf.Infinity;
    public float breakTorque = Mathf.Infinity;
    public SoftJointLimit linearLimit = new SoftJointLimit { limit = 0.05f };
    public SoftJointLimitSpring linearLimitSpring = new SoftJointLimitSpring { spring = 50f, damper = 5f };

    // internal state
    public List<Transform> ropeSegments = new List<Transform>();
    private Rigidbody lastSegmentRb;
    private Rigidbody prevRb; // the rigidbody the next segment will connect to (starts as fixedPoint's rb)

    // kite references so we can reattach it when new segments are added/removed
    private GameObject kiteInstance;
    private ConfigurableJoint kiteJoint;
    private Rigidbody kiteRb;

    // Start only builds if fixedPoint is already assigned (useful when instantiated by a spawner)
    void Start()
    {
        if (fixedPoint == null)
        {
            // don't auto-build — assume an external spawner will call BuildRope()
            return;
        }

        BuildRope();
    }

    // Public: build rope (safe to call multiple times; will early-out if already built)
    public void BuildRope()
    {
        if (ropeSegments.Count > 0 || kiteInstance != null)
        {
            // already built — you may want to clear first if you want rebuild behavior
            return;
        }

        if (fixedPoint == null || ropeSegmentPrefab == null || kitePrefab == null)
        {
            Debug.LogError("RopeGenerator: assign fixedPoint, ropeSegmentPrefab and kitePrefab in inspector.");
            return;
        }

        prevRb = fixedPoint.GetComponent<Rigidbody>();
        if (prevRb == null)
        {
            // add kinematic rb if fixedPoint doesn't have one (keeps joint behavior consistent)
            prevRb = fixedPoint.gameObject.AddComponent<Rigidbody>();
            prevRb.isKinematic = true;
        }

        // Build initial rope
        Vector3 spawnPos = fixedPoint.position;
        for (int i = 0; i < initialSegmentCount; i++)
        {
            spawnPos += Vector3.up * segmentLength;
            CreateSegment(spawnPos);
        }

        // Initialize rope visuals if present
        var rv = GetComponent<RopeVisual>();
        if (rv != null) rv.initRopeSegments();

        // Spawn kite and attach to last segment
        SpawnAndAttachKite();
    }

    // Public API: add a single segment at the end (between last segment and kite).
    public Transform AddSegment()
    {
        Vector3 spawnPos;
        if (lastSegmentRb != null)
            spawnPos = lastSegmentRb.transform.position + Vector3.up * segmentLength;
        else if (fixedPoint != null)
            spawnPos = fixedPoint.position + Vector3.up * segmentLength;
        else
            spawnPos = transform.position;

        Transform newSeg = CreateSegment(spawnPos);

        // If kite exists, reattach kite's joint to the new last segment
        if (kiteJoint != null && newSeg != null)
        {
            Rigidbody newRb = newSeg.GetComponent<Rigidbody>();
            kiteJoint.connectedBody = newRb;
            kiteJoint.connectedAnchor = Vector3.up * (segmentLength * 0.5f);
        }

        var rv = GetComponent<RopeVisual>();
        if (rv != null) rv.initRopeSegments();

        return newSeg;
    }

    // Public API: add multiple segments
    public void AddSegments(int count)
    {
        for (int i = 0; i < count; i++)
            AddSegment();
    }

    // Public API: remove last segment. Returns true if a segment was removed.
    public bool RemoveLastSegment()
    {
        if (ropeSegments.Count == 0)
        {
            Debug.LogWarning("RopeGenerator: No segments to remove.");
            return false;
        }

        // Remove last
        Transform last = ropeSegments[ropeSegments.Count - 1];
        Rigidbody lastRb = last.GetComponent<Rigidbody>();

        // Destroy the GameObject
        Destroy(last.gameObject);
        ropeSegments.RemoveAt(ropeSegments.Count - 1);

        // Update prevRb and lastSegmentRb to the new last segment (or fixedPoint)
        if (ropeSegments.Count > 0)
        {
            Transform newLast = ropeSegments[ropeSegments.Count - 1];
            lastSegmentRb = newLast.GetComponent<Rigidbody>();
            prevRb = lastSegmentRb;
        }
        else
        {
            lastSegmentRb = null;
            prevRb = fixedPoint.GetComponent<Rigidbody>() ?? prevRb; // keep fixedPoint rb if available
        }

        // Reattach kite joint if kite exists
        if (kiteJoint != null)
        {
            if (lastSegmentRb != null)
            {
                kiteJoint.connectedBody = lastSegmentRb;
                kiteJoint.connectedAnchor = Vector3.up * (segmentLength * 0.5f);
            }
            else
            {
                // no rope segments left: connect kite to fixedPoint (or null)
                Rigidbody fixedRb = fixedPoint.GetComponent<Rigidbody>();
                kiteJoint.connectedBody = fixedRb != null ? fixedRb : null;
                kiteJoint.connectedAnchor = Vector3.zero;
            }
        }

        var rv = GetComponent<RopeVisual>();
        if (rv != null) rv.initRopeSegments();

        return true;
    }

    // Public API: remove multiple segments (safely)
    public void RemoveSegments(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!RemoveLastSegment()) break;
        }
    }

    // Creates one segment GameObject, sets Rigidbody and joint to prevRb, and updates prevRb/lastSegmentRb
    private Transform CreateSegment(Vector3 position)
    {
        GameObject segment = Instantiate(ropeSegmentPrefab, position, Quaternion.identity, transform);
        segment.name = "Segment " + ropeSegments.Count;
        Transform segT = segment.transform;
        ropeSegments.Add(segT);

        // Rigidbody
        Rigidbody rb = segment.GetComponent<Rigidbody>();
        if (rb == null) rb = segment.AddComponent<Rigidbody>();
        rb.mass = segmentMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // ConfigurableJoint connecting this segment to prevRb
        ConfigurableJoint joint = segment.GetComponent<ConfigurableJoint>();
        if (joint == null) joint = segment.AddComponent<ConfigurableJoint>();

        ConfigureConfigurableJoint(joint, prevRb);

        // Move forward: this segment becomes the prevRb for the next created segment
        prevRb = rb;
        lastSegmentRb = rb;

        return segT;
    }

    // Configures a configurable joint on 'segment' to connect to 'connectedBody'
    private void ConfigureConfigurableJoint(ConfigurableJoint joint, Rigidbody connectedBody)
    {
        joint.connectedBody = connectedBody;

        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.up * (segmentLength * 0.5f); // approximate top
        joint.connectedAnchor = Vector3.zero;

        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;
        joint.linearLimit = linearLimit;
        joint.linearLimitSpring = linearLimitSpring;

        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Limited;
        joint.angularZMotion = ConfigurableJointMotion.Limited;

        joint.angularYLimit = new SoftJointLimit { limit = angularYLimit };
        joint.angularZLimit = new SoftJointLimit { limit = angularZLimit };

        joint.breakForce = breakForce;
        joint.breakTorque = breakTorque;
    }

    // Spawns the kite and attaches it to the current last segment
    public void SpawnAndAttachKite()
    {
        if (kiteInstance != null)
        {
            Debug.LogWarning("RopeGenerator: kite already spawned.");
            return;
        }

        Vector3 kitePos = (lastSegmentRb != null)
            ? lastSegmentRb.transform.position + Vector3.up * (segmentLength * 0.8f)
            : fixedPoint.position + Vector3.up * (segmentLength * 0.8f);

        kiteInstance = Instantiate(kitePrefab, kitePos, Quaternion.Euler(-45f, 90f, 0f));
        kiteInstance.name = "Kite";

        kiteRb = kiteInstance.GetComponent<Rigidbody>();
        if (kiteRb == null)
        {
            Debug.LogError("Rigidbody not found on kite prefab. Adding one as fallback.");
            kiteRb = kiteInstance.AddComponent<Rigidbody>();
        }

        kiteRb.mass = 1f;
        kiteRb.interpolation = RigidbodyInterpolation.Interpolate;
        kiteRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // compute attach point in kite local space (adjust as needed)
        Vector3 attachLocalOnKite = new Vector3(0.5f * kiteInstance.transform.localScale.x, 0f, 0f);

        // Add/configure ConfigurableJoint on kite to connect to the last rope segment
        kiteJoint = kiteInstance.GetComponent<ConfigurableJoint>();
        if (kiteJoint == null) kiteJoint = kiteInstance.AddComponent<ConfigurableJoint>();

        // If we have a last segment, connect to it, otherwise connect to fixed point rb if present
        if (lastSegmentRb != null)
        {
            kiteJoint.connectedBody = lastSegmentRb;
            kiteJoint.connectedAnchor = Vector3.up * (segmentLength * 0.5f);
        }
        else
        {
            Rigidbody fixedRb = fixedPoint.GetComponent<Rigidbody>();
            kiteJoint.connectedBody = fixedRb != null ? fixedRb : null;
            kiteJoint.connectedAnchor = Vector3.zero;
        }

        kiteJoint.autoConfigureConnectedAnchor = false;
        kiteJoint.anchor = attachLocalOnKite;

        kiteJoint.xMotion = ConfigurableJointMotion.Limited;
        kiteJoint.yMotion = ConfigurableJointMotion.Limited;
        kiteJoint.zMotion = ConfigurableJointMotion.Limited;
        kiteJoint.linearLimit = new SoftJointLimit { limit = segmentLength * 1.5f };

        kiteJoint.angularXMotion = ConfigurableJointMotion.Free;
        kiteJoint.angularYMotion = ConfigurableJointMotion.Limited;
        kiteJoint.angularZMotion = ConfigurableJointMotion.Limited;
        kiteJoint.angularYLimit = new SoftJointLimit { limit = 60f };
        kiteJoint.angularZLimit = new SoftJointLimit { limit = 60f };
    }
    
    public Transform GetKiteTransform()
    {
        return kiteInstance != null ? kiteInstance.transform : null;
    }
    
    public GameObject GetKiteGameObject()
    {
        return kiteInstance != null ? kiteInstance : null;
    }

    // Optional: returns current segment count
    public int GetSegmentCount()
    {
        return ropeSegments.Count;
    }
}
