using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this script to an empty GameObject. Assign your rail segments
/// (as Transforms) in the inspector. On Start, it arranges them side-by-side
/// (with an optional offset), then continuously scrolls them left. When the
/// leftmost rail fully moves off-screen, it becomes the new rightmost rail.
/// </summary>
public class InfiniteRailScroller : MonoBehaviour
{
	[Tooltip("Rail segments (unordered)")]
	public List<Transform> rails;

	[Tooltip("Scroll speed in units per second")]
	public float scrollSpeed = 5f;

	[Header("Start Offset (world units)")]
	[Tooltip("Initial offset applied to all rails before tiling")]
	public Vector2 startOffset = Vector2.zero;

	private float railWidth;
	private Camera mainCam;
	private float screenLeftX;

	void Start()
	{
		if (rails == null || rails.Count < 2)
		{
			Debug.LogError("Assign at least 2 rails to InfiniteRailScroller.");
			enabled = false;
			return;
		}

		mainCam = Camera.main;
		railWidth = rails[0].GetComponent<SpriteRenderer>().bounds.size.x;

		// Initial arrangement side-by-side, then apply startOffset
		for (int i = 0; i < rails.Count; i++)
		{
			Vector3 basePos = new Vector3(
				transform.position.x + i * railWidth,
				rails[i].position.y,
				rails[i].position.z
			);
			rails[i].position = basePos + new Vector3(startOffset.x, startOffset.y, 0f);
		}
	}

	void Update()
	{
		// Scroll all rails left
		foreach (Transform rail in rails)
			rail.Translate(Vector3.left * scrollSpeed * Time.deltaTime, Space.World);

		// Calculate world-space left edge of camera
		screenLeftX = mainCam.ViewportToWorldPoint(
			new Vector3(0, 0.5f, mainCam.nearClipPlane)
		).x;

		// Identify leftmost rail
		Transform leftmost = rails[0];
		foreach (Transform r in rails)
			if (r.position.x < leftmost.position.x)
				leftmost = r;

		// If leftmost rail's right edge is off-screen, wrap it to the right
		float rightEdgeX = leftmost.position.x + railWidth * 0.5f;
		if (rightEdgeX < screenLeftX)
		{
			// Find current rightmost rail
			Transform rightmost = rails[0];
			foreach (Transform r in rails)
				if (r.position.x > rightmost.position.x)
					rightmost = r;

			// Move leftmost to right of rightmost
			leftmost.position = new Vector3(
				rightmost.position.x + railWidth,
				leftmost.position.y,
				leftmost.position.z
			);
		}
	}
}
