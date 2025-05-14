using UnityEngine;

[ExecuteInEditMode]
public class IndependentTopSpawnerVisualizer : MonoBehaviour
{
	public IndependentTopSpawner spawner;

	void OnDrawGizmos()
	{
		if (spawner == null)
			return;

		Camera cam = spawner.mainCam ? spawner.mainCam : Camera.main;
		if (cam == null)
			return;

		// Compute top-left corner in world space
		Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, cam.nearClipPlane));

		// Normalize ranges
		float xMin = Mathf.Min(spawner.spawnXOffsetRange.x, spawner.spawnXOffsetRange.y);
		float xMax = Mathf.Max(spawner.spawnXOffsetRange.x, spawner.spawnXOffsetRange.y);
		float yMin = Mathf.Min(spawner.spawnYOffsetRange.x, spawner.spawnYOffsetRange.y);
		float yMax = Mathf.Max(spawner.spawnYOffsetRange.x, spawner.spawnYOffsetRange.y);

		// Define rectangle corners
		Vector3 p1 = topLeft + new Vector3(xMin, yMin, 0f);
		Vector3 p2 = topLeft + new Vector3(xMax, yMin, 0f);
		Vector3 p3 = topLeft + new Vector3(xMax, yMax, 0f);
		Vector3 p4 = topLeft + new Vector3(xMin, yMax, 0f);

		// Draw rectangle gizmo
		Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
		Gizmos.DrawLine(p1, p2);
		Gizmos.DrawLine(p2, p3);
		Gizmos.DrawLine(p3, p4);
		Gizmos.DrawLine(p4, p1);

		// Draw pivot point
		Gizmos.color = Color.red;
		Gizmos.DrawSphere(topLeft, 0.05f);
	}
}
