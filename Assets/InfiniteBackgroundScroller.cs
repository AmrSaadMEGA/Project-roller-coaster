using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class InfiniteBackgroundScroller : MonoBehaviour
{
	[Tooltip("Exactly 3 background panels (Left, Middle, Right)")]
	public List<Transform> panels;

	[Tooltip("Scrolling speed (units per second)")]
	public float scrollSpeed = 2f;

	private float panelWidth;
	private Camera mainCam;
	private float screenLeftEdge;

	[Header("Start Offset (world units)")]
	[Tooltip("Initial offset applied to all panels before tiling")]
	public Vector2 startOffset = Vector2.zero;


	void Start()
	{
		if (panels == null || panels.Count != 3)
		{
			Debug.LogError("Assign exactly 3 panels to InfiniteBackgroundScroller.");
			enabled = false;
			return;
		}

		mainCam = Camera.main;
		panelWidth = panels[0].GetComponent<SpriteRenderer>().bounds.size.x;

		for (int i = 0; i < panels.Count; i++)
		{
			Vector3 basePos = new Vector3(
				transform.position.x + i * panelWidth,
				panels[i].position.y,
				panels[i].position.z
			);

			panels[i].position = basePos + new Vector3(startOffset.x, startOffset.y, 0f);
		}
	}


	void Update()
	{
		foreach (var p in panels)
			p.Translate(Vector3.left * scrollSpeed * Time.deltaTime, Space.World);

		screenLeftEdge = mainCam.ViewportToWorldPoint(
			new Vector3(0, 0.5f, mainCam.nearClipPlane)
		).x;

		Transform leftmost = panels[0];
		foreach (var p in panels)
			if (p.position.x < leftmost.position.x)
				leftmost = p;

		float rightEdge = leftmost.position.x + panelWidth * 0.5f;
		if (rightEdge < screenLeftEdge)
		{
			Transform rightmost = panels[0];
			foreach (var p in panels)
				if (p.position.x > rightmost.position.x)
					rightmost = p;

			leftmost.position = new Vector3(
				rightmost.position.x + panelWidth,
				leftmost.position.y,
				leftmost.position.z
			);
		}
	}

#if UNITY_EDITOR
	// Draw gizmos when this object is selected
	void OnDrawGizmosSelected()
	{
		if (panels == null || panels.Count != 3)
			return;

		// Draw a small handle at the base transform position
		Gizmos.color = Color.yellow;
		Vector3 root = transform.position;
		Gizmos.DrawSphere(root, 0.1f);
		Handles.Label(root, "Scroller Root");

		// Draw offset vector from the root
		Vector3 offsetPos = root + new Vector3(startOffset.x, startOffset.y, 0);
		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(root, offsetPos);
		Handles.Label(offsetPos, $"Offset ({startOffset.x}, {startOffset.y})");

		// Draw a border around each panel
		Gizmos.color = Color.green;
		for (int i = 0; i < panels.Count; i++)
		{
			var sr = panels[i].GetComponent<SpriteRenderer>();
			if (sr == null) continue;

			// Use the actual bounds (in world space)
			Bounds b = sr.bounds;

			Vector3 topLeft = new Vector3(b.min.x, b.max.y, b.center.z);
			Vector3 topRight = new Vector3(b.max.x, b.max.y, b.center.z);
			Vector3 bottomRight = new Vector3(b.max.x, b.min.y, b.center.z);
			Vector3 bottomLeft = new Vector3(b.min.x, b.min.y, b.center.z);

			// Draw rectangle
			Gizmos.DrawLine(topLeft, topRight);
			Gizmos.DrawLine(topRight, bottomRight);
			Gizmos.DrawLine(bottomRight, bottomLeft);
			Gizmos.DrawLine(bottomLeft, topLeft);
		}
	}
#endif
}
