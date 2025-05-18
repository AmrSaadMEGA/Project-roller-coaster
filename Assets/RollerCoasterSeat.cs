using UnityEngine;

public class RollerCoasterSeat : MonoBehaviour
{
	[Tooltip("Reference to the human sitting in this seat (null if empty)")]
	// Initialize seat index (call this from cart)
	public HumanStateController occupyingHuman;
	public int SeatIndex { get; private set; } // Add this property

	[Tooltip("Whether this seat can be occupied")]
	public bool isOccupied => occupyingHuman != null;

	// NEW: Track zombie occupation
	public bool IsOccupiedByZombie { get; private set; }

	// Call this when the zombie occupies the seat
	public void SetZombieOccupation(bool isOccupied)
	{
		IsOccupiedByZombie = isOccupied;
	}
	public void SetSeatIndex(int index)
	{
		SeatIndex = index;
	}

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
	private void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag("Zombie"))
		{
			SetZombieOccupation(true); // Mark seat as zombie-occupied
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