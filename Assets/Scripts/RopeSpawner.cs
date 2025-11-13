using UnityEngine;

public class RopeSpawner : MonoBehaviour
{
    [Tooltip("Prefab that contains the RopeGenerator component. Make sure ropeSegmentPrefab and kitePrefab are assigned on the prefab.")]
    public GameObject ropeGeneratorPrefab;

    [Tooltip("Tag used for spawn points")]
    public string spawnTag = "SpawnPoint";

    [Tooltip("Enemy Kite Prefab")]
    public GameObject enemyKite;
    // Optional offset if you want the fixedPoint to sit exactly on the spawnpoint plus offset
    public Vector3 fixedPointOffset = Vector3.zero;

    void Start()
    {
        if (ropeGeneratorPrefab == null)
        {
            Debug.LogError("RopeSpawner: ropeGeneratorPrefab not assigned.");
            return;
        }

        if (enemyKite == null)
        {
            Debug.LogError("RopeSpawner: enemyKite not assigned.");
            return;
        }
        
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag(spawnTag);
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("RopeSpawner: no spawn points found with tag: " + spawnTag);
            return;
        }

        foreach (GameObject sp in spawnPoints)
        {
            // instantiate the prefab; parent it under this spawner to keep hierarchy tidy (optional)
            GameObject inst = Instantiate(ropeGeneratorPrefab, sp.transform.position + fixedPointOffset, Quaternion.identity, transform);
            RopeGenerator rg = inst.GetComponent<RopeGenerator>();
            if (rg == null)
            {
                Debug.LogError("RopeSpawner: ropeGeneratorPrefab missing RopeGenerator component.");
                Destroy(inst);
                continue;
            }

            // set the fixed point to the spawn point transform (so rope attaches to that object)
            rg.fixedPoint = sp.transform;

            // Build the rope now
            rg.BuildRope();
        }
    }
}