using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color ropeColor = new Color(0.9f, 0.9f, 1f, 1f); // light bluish-white, like nylon
    [SerializeField] private float ropeThickness = 0.02f; // small width for thin rope
    [SerializeField] private Material ropeMaterial; // optional, assign a transparent or unlit material
    [SerializeField, Range(0, 1)] private float textureTiling = 0.1f; // UV tiling if using a texture
    [SerializeField] private bool useSmoothLine = true; // optional smoothing for subtle visual polish

    private LineRenderer line;
    private List<Transform> ropeSegments;

    public void initRopeSegments()
    {
        ropeSegments = GetComponent<RopeGenerator>().ropeSegments;
        line = GetComponent<LineRenderer>();

        line.positionCount = ropeSegments.Count;
        ApplyVisualSettings();
    }

    private void ApplyVisualSettings()
    {
        if (line == null) return;

        // Set color
        line.startColor = ropeColor;
        line.endColor = ropeColor;

        // Set thickness
        line.startWidth = ropeThickness;
        line.endWidth = ropeThickness;

        // Assign material (optional)
        if (ropeMaterial != null)
            line.material = ropeMaterial;
        else
            line.material = new Material(Shader.Find("Sprites/Default")); // simple unlit fallback

        // Optional texture tiling if you use a rope texture
        line.textureMode = LineTextureMode.Stretch;
        line.material.mainTextureScale = new Vector2(textureTiling, 1f);

        // Optionally smooth corners slightly for a thread-like look
        line.numCornerVertices = useSmoothLine ? 4 : 0;
        line.numCapVertices = useSmoothLine ? 2 : 0;
    }

    void Update()
    {
        if (ropeSegments == null || line == null)
            return;

        for (int i = 0; i < ropeSegments.Count; i++)
        {
            line.SetPosition(i, ropeSegments[i].position);
        }
    }
}
