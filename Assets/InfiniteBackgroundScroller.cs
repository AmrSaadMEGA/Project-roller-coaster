using System.Collections.Generic;
using UnityEngine;

public class InfiniteBackgroundScroller : MonoBehaviour
{
	[Tooltip("Exactly 3 background panels (Left, Middle, Right)")]
	public List<Transform> panels;

	[Tooltip("Scrolling speed (units per second)")]
	public float scrollSpeed = 2f;

	private float panelWidth;
	private Camera mainCam;
	private float screenLeftEdge;

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

		// Line up side by side
		for (int i = 0; i < panels.Count; i++)
		{
			panels[i].position = new Vector3(
				transform.position.x + i * panelWidth,
				panels[i].position.y,
				panels[i].position.z
			);
		}
	}

	void Update()
	{
		// Scroll all panels left
		foreach (var p in panels)
			p.Translate(Vector3.left * scrollSpeed * Time.deltaTime, Space.World);

		// Compute world‐space left edge
		screenLeftEdge = mainCam.ViewportToWorldPoint(
			new Vector3(0, 0.5f, mainCam.nearClipPlane)
		).x;

		// Find leftmost
		Transform leftmost = panels[0];
		foreach (var p in panels)
			if (p.position.x < leftmost.position.x)
				leftmost = p;

		float rightEdge = leftmost.position.x + panelWidth * 0.5f;
		if (rightEdge < screenLeftEdge)
		{
			// Wrap it to the right of the rightmost panel
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
}
