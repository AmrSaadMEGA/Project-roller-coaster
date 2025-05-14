using UnityEngine;

[RequireComponent(typeof(HumanStateController))]
public class HumanSeatOccupant : MonoBehaviour
{
	[Tooltip("The seat this human is occupying")]
	[SerializeField] private RollerCoasterSeat occupiedSeat;

	private HumanStateController humanController;

	// Make the occupied seat accessible
	public RollerCoasterSeat OccupiedSeat => occupiedSeat;

	void Awake()
	{
		humanController = GetComponent<HumanStateController>();
	}

	void Start()
	{
		if (occupiedSeat != null)
		{
			// Register this human with the seat
			occupiedSeat.occupyingHuman = humanController;

			// Position the human at the seat
			transform.position = occupiedSeat.transform.position;

			// Add a collider if it doesn't exist (2D version)
			if (GetComponent<Collider2D>() == null)
			{
				// Add a circle collider for 2D detection
				CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
				collider.radius = 0.5f;
				collider.offset = new Vector2(0, 0.5f); // Adjust based on your sprites
			}
		}
		else
		{
			Debug.LogWarning("HumanSeatOccupant has no assigned seat!", this);
		}
	}

	void OnDestroy()
	{
		// When the human is destroyed (e.g., dies), free up the seat
		if (occupiedSeat != null)
		{
			occupiedSeat.occupyingHuman = null;
		}
	}
}