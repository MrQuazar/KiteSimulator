using UnityEngine;

public class LookAt : MonoBehaviour
{
    private Transform target;
    [Tooltip("Whether to always look at the target.")]
    public bool lookAtTarget = true;
    [Tooltip("Whether to always follow target.")]
    public bool followTarget = true;
    public Transform phirki;
    [SerializeField] private float distance = 5f;
    void Update()
    {
        // Auto-assign target if not yet found
        if (target == null)
        {
            GameObject kite = GameObject.FindWithTag("CamReference");
            if (kite != null)
            {
                target = kite.transform;
            }
            else
            {
                return; // Skip if no target found yet
            }
        }

        if (followTarget)
        {
            Vector3 newCamPosition = new Vector3((target.position.x - phirki.position.x - distance),
                (target.position.y - phirki.position.y - distance), (target.position.z - phirki.position.z - distance));
            transform.position = newCamPosition;
        }
        // Rotate to look at the target
        if (lookAtTarget)
        {
            transform.LookAt(target);
        }
    }
}