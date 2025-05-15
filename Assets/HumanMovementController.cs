using UnityEngine;
using System.Collections;

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

	private void Awake()
	{
		stateController = GetComponent<HumanStateController>();
		animator = GetComponent<Animator>();
		targetPosition = transform.position;
	}

	private void Start()
	{
		pathUpdateTimer = Random.Range(0f, pathUpdateInterval); // Stagger updates
	}

	private void Update()
	{
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