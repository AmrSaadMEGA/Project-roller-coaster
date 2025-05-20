// Modify HumanSeatOccupant.cs to track the assigned seat before occupation

using UnityEngine;

public class HumanSeatOccupant : MonoBehaviour
{
	// Reference to the currently occupied seat
	public RollerCoasterSeat OccupiedSeat { get; private set; }

	// NEW: Track assigned seat before occupation
	public RollerCoasterSeat AssignedSeat { get; set; }

	// Track whether the human is seated
	public bool IsSeated { get; private set; }

	// Reference to the movement controller
	private HumanMovementController movementController;

	private void Awake()
	{
		movementController = GetComponent<HumanMovementController>();
	}

	public void AssignSeat(RollerCoasterSeat seat)
	{
		if (seat == null) return;

		// Store the assigned seat immediately
		AssignedSeat = seat;

		// Set destination to the seat position
		if (movementController != null)
		{
			movementController.SetDestination(seat.transform.position);
		}

		// Start coroutine to check for arrival
		StartCoroutine(CheckForArrival());
	}

	private System.Collections.IEnumerator CheckForArrival()
	{
		ZombieHidingSystem zombieHiding = FindObjectOfType<ZombieController>()?.GetComponent<ZombieHidingSystem>();

		// Wait until movement ends
		while (movementController != null && movementController.IsMoving())
		{
			// FIXED: Only check for zombie visibility if it's definitely NOT hidden AND NOT in the process of hiding
			// This is the key fix - only react if zombie is DEFINITELY exposed (not hidden AND not hiding)
			if (zombieHiding != null && zombieHiding.IsHidden == false && zombieHiding.IsHiding == false)
			{
				// Signal zombie detection and trigger panic
				HumanScreamingState screamState = GetComponent<HumanScreamingState>();
				if (screamState != null)
				{
					// Get zombie position for direction awareness
					Transform zombieTransform = zombieHiding.transform;
					screamState.ScreamAndRunAway(zombieTransform.position, true);
				}
				else
				{
					// Fallback if no ScreamingState component
					movementController.StopMoving();
					Vector3 awayDir = (zombieHiding != null) ?
						(transform.position - zombieHiding.transform.position).normalized :
						new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
					Vector3 fleeTarget = transform.position + awayDir * 10f;
					movementController.SetDestination(fleeTarget);
				}

				// Trigger panic in nearby humans as well
				HumanStateController stateController = GetComponent<HumanStateController>();
				if (stateController != null)
				{
					stateController.TriggerPanicInRadius(5f);
				}

				// Clear assigned seat since we're fleeing
				AssignedSeat = null;
				yield break;
			}

			// Original seat-specific check (keep this too)
			if (AssignedSeat != null && AssignedSeat.IsOccupiedByZombie)
			{
				Debug.Log($"Human {gameObject.name} detected zombie took their assigned seat during movement!");

				// Signal zombie detection and trigger panic
				HumanScreamingState screamState = GetComponent<HumanScreamingState>();
				if (screamState != null)
				{
					// Use true parameter to indicate this is a critical reaction (seat stolen)
					screamState.ScreamAndRunAway(AssignedSeat.transform.position, true);
				}
				else
				{
					// Fallback if no ScreamingState component
					movementController.StopMoving();
					Vector3 awayDir = (transform.position - AssignedSeat.transform.position).normalized;
					Vector3 fleeTarget = transform.position + awayDir * 10f;
					movementController.SetDestination(fleeTarget);
				}

				// Trigger panic in nearby humans as well
				HumanStateController stateController = GetComponent<HumanStateController>();
				if (stateController != null)
				{
					stateController.TriggerPanicInRadius(5f);
				}

				// Clear assigned seat since it's now taken by zombie
				AssignedSeat = null;
				yield break;
			}

			yield return null;
		}

		// Only occupy if we've actually reached the destination and the seat isn't zombie-occupied
		if (movementController != null &&
			movementController.HasReachedDestination() &&
			AssignedSeat != null &&
			!AssignedSeat.IsOccupiedByZombie)
		{
			OccupySeat();
		}
		else
		{
			Debug.Log($"Human {gameObject.name} aborted seat occupation!");
		}
	}

	// Change 2: Add a method to check for zombie in assigned seat
	// Add this method to the HumanSeatOccupant class:

	public bool CheckForZombieInAssignedSeat()
	{
		if (AssignedSeat == null || !AssignedSeat.IsOccupiedByZombie)
			return false;

		Debug.Log($"Human {gameObject.name} detected zombie in their assigned seat!");

		// Trigger panic and screaming
		HumanScreamingState screamState = GetComponent<HumanScreamingState>();
		if (screamState != null)
		{
			screamState.ScreamAndRunAway(AssignedSeat.transform.position, true);
		}

		// Clear assignment
		AssignedSeat = null;
		return true;
	}
	private void OccupySeat()
	{
		if (AssignedSeat == null) return;

		// Set references
		OccupiedSeat = AssignedSeat;
		OccupiedSeat.occupyingHuman = GetComponent<HumanStateController>();

		// Set seated status
		IsSeated = true;

		// Move to exact position of the seat
		transform.position = OccupiedSeat.transform.position;

		Debug.Log($"Human {gameObject.name} occupied seat {OccupiedSeat.name}");
	}

	// Call this when the human leaves the seat
	public void LeaveSeat()
	{
		if (OccupiedSeat != null)
		{
			Debug.Log($"Human {gameObject.name} leaving seat {OccupiedSeat.name}");
			OccupiedSeat.occupyingHuman = null;
		}

		OccupiedSeat = null;
		AssignedSeat = null;
		IsSeated = false;
	}
}