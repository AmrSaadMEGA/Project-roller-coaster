using UnityEngine;

public class RideWithBackground : MonoBehaviour
{
	[Tooltip("Must match the scrollSpeed in your InfiniteBackgroundScroller")]
	public float scrollSpeed = 2f;

	private Camera mainCam;
	private float screenLeftX;

	void Start()
	{
		mainCam = Camera.main;
	}

	void Update()
	{
		// Move left at the background scroll speed
		transform.Translate(Vector3.left * scrollSpeed * Time.deltaTime, Space.World);

		// Compute world‐space left edge (once per frame)
		screenLeftX = mainCam.ViewportToWorldPoint(
			new Vector3(0, 0.5f, mainCam.nearClipPlane)
		).x;

		// If fully off-screen, destroy
		float halfWidth = GetComponent<SpriteRenderer>().bounds.size.x * 0.5f;
		if (transform.position.x + halfWidth < screenLeftX)
			Destroy(gameObject);
	}
}
