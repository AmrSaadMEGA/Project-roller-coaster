using UnityEngine;

[RequireComponent(typeof(HumanStateController))]
[RequireComponent(typeof(HumanMovementController))]
public class HumanSeatOccupant : MonoBehaviour
{
	[Tooltip("The seat this human is occupying")]
	[SerializeField] private RollerCoasterSeat occupiedSeat;

	private HumanStateController humanController;
	private HumanMovementController movementController;
	private bool isSeated = false;

	// Make the occupied seat accessible
	public RollerCoasterSeat OccupiedSeat => occupiedSeat;

	void Awake()
	{
		humanController = GetComponent<HumanStateController>();
		movementController = GetComponent<HumanMovementController>();
	}

	void Start()
	{
		if (occupiedSeat != null)
		{
			// Register this human with the seat
			occupiedSeat.occupyingHuman = humanController;
			isSeated = true;

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
	}

	void Update()
	{
		// If we have a movement controller and assigned seat but not yet seated
		if (!isSeated && movementController != null && occupiedSeat != null)
		{
			// Check if we've reached the seat position
			if (movementController.HasReachedDestination() && !movementController.IsMoving())
			{
				// We've arrived at the seat, occupy it
				OccupySeat(occupiedSeat);
			}
		}
	}

	/// <summary>
	/// Sets a new seat for this human to occupy and moves them toward it
	/// </summary>
	/// <param name="seat">The seat to occupy</param>
	public void AssignSeat(RollerCoasterSeat seat)
	{
		// Unregister from current seat if any
		if (occupiedSeat != null && occupiedSeat.occupyingHuman == humanController)
		{
			occupiedSeat.occupyingHuman = null;
		}

		// Set new seat
		occupiedSeat = seat;
		isSeated = false;

		// Start moving to the seat
		if (movementController != null && seat != null)
		{
			movementController.SetDestination(seat.transform.position);
		}
	}

	/// <summary>
	/// Immediately occupies a seat without movement
	/// </summary>
	/// <param name="seat">The seat to occupy</param>
	public void OccupySeat(RollerCoasterSeat seat)
	{
		// Unregister from current seat if any
		if (occupiedSeat != null && occupiedSeat.occupyingHuman == humanController)
		{
			occupiedSeat.occupyingHuman = null;
		}

		// Set and register with new seat
		occupiedSeat = seat;

		if (seat != null)
		{
			// Register this human with the seat
			seat.occupyingHuman = humanController;

			// Set position to seat's position
			transform.position = seat.transform.position;

			// Stop any movement
			if (movementController != null)
			{
				movementController.StopMoving();
			}

			isSeated = true;

			Debug.Log($"Human {gameObject.name} occupied seat {seat.gameObject.name}");
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