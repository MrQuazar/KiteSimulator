using UnityEngine;

public class RopeCollisionReporter : MonoBehaviour
{
    [HideInInspector] public GameObject kiteOwner;  // assigned automatically

    private void Start()
    {
        // Try to find the kite at the top of the rope chain
        RopeGenerator rope = GetComponentInParent<RopeGenerator>();
        if (rope != null)
        {
            kiteOwner = rope.GetKiteGameObject();
        }
        else
        {
            Debug.LogWarning($"{name}: RopeCollisionReporter couldn't find RopeGenerator in parent hierarchy.");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Rope collision detected between "+ gameObject.tag + " " + collision.gameObject.tag);
        if (collision.gameObject.CompareTag("EnemySegment") && CompareTag("Segment"))
        {
            GameObject otherKite = null;

            // Try to get RopeCollisionReporter on the other segment
            RopeCollisionReporter otherReporter = collision.gameObject.GetComponent<RopeCollisionReporter>();
            if (otherReporter != null)
                otherKite = otherReporter.kiteOwner;

            string thisKiteName = kiteOwner != null ? kiteOwner.name : "(Unknown Kite)";
            string otherKiteName = otherKite != null ? otherKite.name : "(Unknown Enemy Kite)";

            Debug.Log($"Rope collision detected between {thisKiteName} and {otherKiteName}!");
        }
    }
}