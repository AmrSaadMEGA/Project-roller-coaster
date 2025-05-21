using UnityEngine;
using System.Collections;

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

	private IEnumerator CheckForArrival()
	{
		ZombieHidingSystem zombieHiding = FindFirstObjectByType<ZombieController>()?.GetComponent<ZombieHidingSystem>();
		while (movementController != null && movementController.IsMoving())
		{
			Debug.Log($"Human {gameObject.name} moving to seat. Zombie Hidden={zombieHiding?.IsHidden}, Hiding={zombieHiding?.IsHiding}, SeatOccupiedByZombie={AssignedSeat?.IsOccupiedByZombie}");
			// Only panic if zombie is visible (not hidden AND not hiding)
			if (zombieHiding != null && !zombieHiding.IsHidden && !zombieHiding.IsHiding)
			{
				HumanScreamingState screamState = GetComponent<HumanScreamingState>();
				if (screamState != null)
				{
					screamState.ScreamAndRunAway(zombieHiding.transform.position, true);
					AssignedSeat = null;
					yield break;
				}
				else
				{
					movementController.StopMoving();
					Vector3 awayDir = (zombieHiding != null) ?
						(transform.position - zombieHiding.transform.position).normalized :
						Random.insideUnitCircle.normalized;
					Vector3 fleeTarget = transform.position + awayDir * 10f;
					movementController.SetDestination(fleeTarget);
				}

				HumanStateController stateController = GetComponent<HumanStateController>();
				if (stateController != null)
					stateController.TriggerPanicInRadius(5f);

				AssignedSeat = null;
				yield break;
			}

			// Only panic if seat is occupied by a VISIBLE zombie
			if (AssignedSeat != null && AssignedSeat.IsOccupiedByZombie &&
				zombieHiding != null && !zombieHiding.IsHidden && !zombieHiding.IsHiding)
			{
				Debug.Log($"Human {gameObject.name} detected zombie in seat during movement!");
				HumanScreamingState screamState = GetComponent<HumanScreamingState>();
				if (screamState != null)
				{
					screamState.ScreamAndRunAway(AssignedSeat.transform.position, true);
				}
				else
				{
					movementController.StopMoving();
					Vector3 awayDir = (transform.position - AssignedSeat.transform.position).normalized;
					Vector3 fleeTarget = transform.position + awayDir * 10f;
					movementController.SetDestination(fleeTarget);
				}

				HumanStateController stateController = GetComponent<HumanStateController>();
				if (stateController != null)
					stateController.TriggerPanicInRadius(5f);

				AssignedSeat = null;
				yield break;
			}

			yield return null;
		}

		if (movementController != null && movementController.HasReachedDestination() &&
			AssignedSeat != null && !AssignedSeat.IsOccupiedByZombie)
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
		ZombieHidingSystem zombieHiding = FindFirstObjectByType<ZombieController>()?.GetComponent<ZombieHidingSystem>();

		// Only consider the seat occupied by a zombie if the zombie is visible (not hidden)
		if (AssignedSeat == null ||
			!AssignedSeat.IsOccupiedByZombie ||
			(zombieHiding != null && (zombieHiding.IsHidden || zombieHiding.IsHiding)))
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
		if (AssignedSeat == null || AssignedSeat.isOccupied) return;

		ZombieHidingSystem zombieHiding = FindFirstObjectByType<ZombieController>()?.GetComponent<ZombieHidingSystem>();
		if (AssignedSeat.IsOccupiedByZombie && zombieHiding != null && !zombieHiding.IsHidden && !zombieHiding.IsHiding)
		{
			Debug.Log($"Human {gameObject.name} tried to occupy seat with visible zombie - aborting!");
			HumanScreamingState screamState0 = GetComponent<HumanScreamingState>();
			if (screamState0 != null)
				screamState0.ScreamAndRunAway(AssignedSeat.transform.position, true);
			AssignedSeat = null;
			return;
		}

		OccupiedSeat = AssignedSeat;
		OccupiedSeat.occupyingHuman = GetComponent<HumanStateController>();
		IsSeated = true;
		transform.position = OccupiedSeat.transform.position;

		// Reset screaming state
		HumanScreamingState screamState = GetComponent<HumanScreamingState>();
		if (screamState != null)
		{
			screamState.StopAllCoroutines();
			screamState.IsScreaming = false;
		}
		HumanMovementController movement = GetComponent<HumanMovementController>();
		if (movement != null)
		{
			movement.StopMoving();
		}

		Animator animator = GetComponent<Animator>();
		if (animator != null)
		{
			animator.Play("Idle"); // Reset to seated/idle animation
		}

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