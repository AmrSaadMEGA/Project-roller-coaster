using UnityEngine;
using System.Collections;
using static RollerCoasterGameManager;

[RequireComponent(typeof(HumanStateController))]
public class HumanMovementController : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float moveSpeed = 2f;
	[SerializeField] private float arrivalDistance = 0.1f;
	[SerializeField] private float pathUpdateInterval = 0.5f;

	[Header("Animation")]
	[SerializeField] private string walkAnimationState = "Walking";
	[SerializeField] private string idleAnimationState = "Idle";

	[Header("Debug")]
	[SerializeField] private bool showPath = false;

	// Private fields
	private Vector3 targetPosition;
	private bool isMoving = false;
	private HumanStateController stateController;
	private Animator animator;
	private float pathUpdateTimer;

	// Add to HumanMovementController class
	[Header("Zombie Awareness")]
	[SerializeField] private float zombieDetectionRange = 5f;

	private ZombieController zombie;
	private HumanScreamingState humanScreamState;

	private void Awake()
	{
		stateController = GetComponent<HumanStateController>();
		animator = GetComponent<Animator>();
		targetPosition = transform.position;

		// Add to existing Awake
		zombie = FindObjectOfType<ZombieController>();
		humanScreamState = GetComponent<HumanScreamingState>();
	}

	private void Start()
	{
		pathUpdateTimer = Random.Range(0f, pathUpdateInterval); // Stagger updates
	}

	private void Update()
	{
		// First priority: Check for zombie occupation in target seat
		if (isMoving && IsTargetSeatOccupiedByZombie())
		{
			Debug.Log($"Human {gameObject.name} detected zombie in target seat! Fleeing!");
			ReactToZombiePresence();
			return;
		}
		// Add to beginning of Update
		if (ShouldReactToZombie())
		{
			ReactToZombiePresence();
			return;
		}
		// Add this check at the start of Update()
		if (isMoving && ShouldReactToZombie())
		{
			ReactToZombiePresence();
			return;
		}
		// Don't move if dead
		if (stateController != null && stateController.IsDead())
		{
			isMoving = false;
			return;
		}

		// If we're moving, update position
		if (isMoving)
		{
			MoveTowardsTarget();
		}
	}
	private bool IsTargetSeatOccupiedByZombie()
	{
		// Get the HumanSeatOccupant component
		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();

		// If we have no occupant or no target seat, we can't check
		if (occupant == null || occupant.AssignedSeat == null)
			return false;

		// Check if the target seat is occupied by a zombie
		return occupant.AssignedSeat.IsOccupiedByZombie;
	}
	private bool IsZombieNearby()
	{
		ZombieController zombie = FindObjectOfType<ZombieController>();
		return zombie != null &&
			   Vector3.Distance(transform.position, zombie.transform.position) < zombieDetectionRange &&
			   !zombie.GetComponent<ZombieHidingSystem>().IsHidden;
	}
	private bool ShouldReactToZombie()
	{
		// FIXED: More robust check for zombie visibility
		RollerCoasterGameManager gameManager = FindObjectOfType<RollerCoasterGameManager>();
		ZombieHidingSystem hidingSystem = zombie?.GetComponent<ZombieHidingSystem>();

		// Critical fix - check if zombie OR hiding system is null, and ensure IsHidden is checked properly
		if (zombie == null || hidingSystem == null)
			return false;

		// IMPORTANT FIX: Add this additional verification to ensure the zombie is ACTUALLY visible
		// This helps prevent false positives that trigger the infinite loop
		if (hidingSystem.IsHidden || hidingSystem.IsHiding)
			return false;

		// Only react when:
		// 1. Human is alive
		// 2. We're not in zombie boarding or ride phases
		// 3. Zombie is close enough
		// 4. Zombie is DEFINITELY visible (not hidden or in transition)
		return !stateController.IsDead() &&
			   gameManager.CurrentState != GameState.ZombieBoarding &&
			   gameManager.CurrentState != GameState.RideInProgress &&
			   Vector3.Distance(transform.position, zombie.transform.position) < zombieDetectionRange;
	}
	private void ReactToZombiePresence(Vector3 zombiePosition = default)
	{
		StopMoving();

		// If no position was provided, try to find the zombie
		if (zombiePosition == default)
		{
			ZombieController zombie = FindObjectOfType<ZombieController>();
			if (zombie != null)
			{
				zombiePosition = zombie.transform.position;
			}
		}

		// Clear seat assignment if we were heading to one
		HumanSeatOccupant seatOccupant = GetComponent<HumanSeatOccupant>();
		if (seatOccupant != null)
		{
			seatOccupant.AssignedSeat = null;
		}

		// Trigger screaming state
		humanScreamState.ScreamAndRunAway(zombiePosition, true);

		// Trigger panic in other humans
		HumanStateController stateController = GetComponent<HumanStateController>();
		if (stateController != null)
		{
			stateController.TriggerPanicInRadius(5f);
		}
	}
	/// <summary>
	/// Sets a new destination for the human to move towards.
	/// </summary>
	public void SetDestination(Vector3 destination)
	{
		// Don't accept new destinations if dead
		if (stateController != null && stateController.IsDead())
			return;

		targetPosition = destination;
		isMoving = true;

		// Play walking animation
		if (animator != null)
		{
			animator.Play(walkAnimationState);
		}
	}

	/// <summary>
	/// Stop movement immediately
	/// </summary>
	public void StopMoving()
	{
		isMoving = false;

		// Play idle animation
		if (animator != null)
		{
			animator.Play(idleAnimationState);
		}
	}

	/// <summary>
	/// Returns true if the human is currently moving towards a destination
	/// </summary>
	public bool IsMoving()
	{
		return isMoving;
	}

	/// <summary>
	/// Returns true if the human has reached its destination
	/// </summary>
	public bool HasReachedDestination()
	{
		return Vector3.Distance(transform.position, targetPosition) <= arrivalDistance;
	}

	private void MoveTowardsTarget()
	{
		// FIXED: More careful check for zombie visibility that prevents false positives
		ZombieHidingSystem hidingSystem = FindObjectOfType<ZombieHidingSystem>();
		if (hidingSystem != null && !hidingSystem.IsHidden && !hidingSystem.IsHiding)
		{
			// Add debug to track this condition
			Debug.Log($"Human {gameObject.name} detected unhidden zombie during movement!");
			ReactToZombiePresence(hidingSystem.transform.position);
			return;
		}

		// IMPORTANT: Only check for visible zombies when they're definitely not hidden
		bool zombieVisible = false;
		if (hidingSystem != null && !hidingSystem.IsHidden && !hidingSystem.IsHiding)
		{
			zombieVisible = IsZombieNearby();
		}

		bool zombieInSeat = IsTargetSeatOccupiedByZombie();

		if (zombieVisible || zombieInSeat)
		{
			Debug.Log($"Human {gameObject.name} detected zombie during movement! Visible={zombieVisible}, InSeat={zombieInSeat}");
			Vector3 zombiePosition = zombieVisible ?
				FindObjectOfType<ZombieController>().transform.position :
				GetComponent<HumanSeatOccupant>()?.AssignedSeat?.transform.position ?? transform.position;

			ReactToZombiePresence(zombiePosition);
			return;
		}

		// Rest of the existing MoveTowardsTarget method remains unchanged
		// Check if we've arrived at the destination
		float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
		if (distanceToTarget <= arrivalDistance)
		{
			// We've arrived
			isMoving = false;

			// Play idle animation
			if (animator != null)
			{
				animator.Play(idleAnimationState);
			}

			return;
		}

		// Calculate direction and movement
		Vector3 direction = (targetPosition - transform.position).normalized;
		Vector3 movement = direction * moveSpeed * Time.deltaTime;

		// Apply movement
		transform.Translate(movement);

		// Handle sprite flipping based on movement direction (for 2D)
		if (direction.x < 0)
		{
			// Moving left
			transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
		}
		else if (direction.x > 0)
		{
			// Moving right
			transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
		}
	}

	private void OnDrawGizmos()
	{
		if (!showPath || !Application.isPlaying) return;

		// Show path to target
		if (isMoving)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(transform.position, targetPosition);
			Gizmos.DrawWireSphere(targetPosition, 0.2f);
		}
	}
}