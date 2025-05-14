using UnityEngine;

public class RollerCoasterSeat : MonoBehaviour
{
	[Tooltip("Reference to the human sitting in this seat (null if empty)")]
	public HumanStateController occupyingHuman;

	[Tooltip("Whether this seat can be occupied")]
	public bool isOccupied => occupyingHuman != null;

	private void Awake()
	{
		// Make sure there's a collider for detection
		if (GetComponent<Collider2D>() == null)
		{
			// Add a box collider for 2D seat detection
			BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
			collider.size = new Vector2(1.0f, 1.0f);
		}
	}

	// Visual indicator for debugging - can be removed in final version
	private void OnDrawGizmos()
	{
		Gizmos.color = isOccupied ? Color.red : Color.green;
		Gizmos.DrawSphere(transform.position, 0.2f);

		// Draw seat name for easier debugging
		UnityEditor.Handles.color = Color.white;
		UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, gameObject.name);
	}
}