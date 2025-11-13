using UnityEngine;

public class KiteHeightMonitor : MonoBehaviour
{
    public float heightStep = 10f;
    public int maxSegmentsToAddPerFrame = 3;
    public int minTotalSegments = 3;

    private RopeGenerator ropeGenerator;
    private int maxTotalSegments = 100;
    private Transform kite;
    private float referenceY;
    private GameObject kiteGameObject;

    void Start()
    {
        ropeGenerator = GetComponent<RopeGenerator>();
        if (ropeGenerator == null)
        {
            Debug.LogError("KiteHeightMonitor: assign RopeGenerator in inspector.");
            enabled = false;
            return;
        }
        
        kiteGameObject = ropeGenerator.GetKiteGameObject();

        kite = ropeGenerator.GetKiteTransform();
        if (kite == null)
        {
            referenceY = 0f;
        }
        else
        {
            referenceY = kite.position.y;
        }
    }

    void Update()
    {
        if (ropeGenerator == null) return;

        if (kite == null)
        {
            kite = ropeGenerator.GetKiteTransform();
            kiteGameObject = ropeGenerator.GetKiteGameObject();
            if (kite == null) return;
            referenceY = kite.position.y;
            return;
        }

        maxTotalSegments = (int)kiteGameObject.GetComponent<WindMechanics>().liftForce / 10;
        float currentY = kite.position.y;
        float delta = currentY - referenceY;

        // If kite went up enough: add segments
        if (delta >= heightStep)
        {
            int steps = Mathf.FloorToInt(delta / heightStep);
            steps = Mathf.Clamp(steps, 1, maxSegmentsToAddPerFrame);
            
            int allowedToAdd = Mathf.Max(0, maxTotalSegments - ropeGenerator.GetSegmentCount());
            int toAdd = Mathf.Min(steps, allowedToAdd);

            if (toAdd > 0)
            {
                ropeGenerator.AddSegments(toAdd);
                referenceY += toAdd * heightStep;
            }
            else
            {
                referenceY += steps * heightStep;
            }
        }
        // If kite pull triggered and drop enough: remove segments
        else if (false && Input.GetKey(KeyCode.DownArrow) && delta <= -heightStep)
        {
            int steps = Mathf.FloorToInt(-delta / heightStep);
            steps = Mathf.Clamp(steps, 1, maxSegmentsToAddPerFrame);

            int currentCount = ropeGenerator.GetSegmentCount();
            int allowedToRemove = Mathf.Max(0, currentCount - minTotalSegments);
            int toRemove = Mathf.Min(steps, allowedToRemove);
            
            if (toRemove > 0)
            {
                ropeGenerator.RemoveSegments(toRemove);
                referenceY -= toRemove * heightStep;
            }
            else
            {
                referenceY -= steps * heightStep;
            }
        }
    }
}
