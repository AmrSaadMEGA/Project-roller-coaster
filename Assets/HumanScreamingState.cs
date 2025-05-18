using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(HumanMovementController))]
public class HumanScreamingState : MonoBehaviour
{
	[Tooltip("Name of the screaming animation state in the Animator")]
	[SerializeField] private string screamingStateName = "Screaming";

	[SerializeField] private float screamDuration = 2.0f; // Increased from 0.5f

	[Tooltip("How fast humans run when scared")]
	[SerializeField] private float panicRunSpeedMultiplier = 0.7f; // Reduced from 1.5f for slower, more noticeable running

	private Animator animator;
	private HumanMovementController movementController;
	private HumanStateController stateController;
	private float originalMoveSpeed;
	public bool IsScreaming { get; private set; }

	public bool IsSeated()
	{
		// Correct seated check logic
		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();
		return occupant != null && occupant.IsSeated;
	}
	private void Awake()
	{
		animator = GetComponent<Animator>();
		movementController = GetComponent<HumanMovementController>();
		stateController = GetComponent<HumanStateController>();

		// Store original move speed using reflection
		System.Reflection.FieldInfo speedField = typeof(HumanMovementController)
			.GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (speedField != null)
		{
			originalMoveSpeed = (float)speedField.GetValue(movementController);
		}
	}

	/// <summary>
	/// Makes the human scream and run away in a panic
	/// </summary>
	/// <param name="scareSource">The position of what scared the human</param>
	public void ScreamAndRunAway(Vector3 scareSource, bool forceReact = false)
	{
		if ((IsScreaming && !forceReact) ||
			stateController.IsDead() ||
			FindObjectOfType<RollerCoasterGameManager>().BoardingCompleted)
			return;

		StopAllCoroutines();
		StartCoroutine(ScreamAndRunSequence(scareSource));
	}

	private System.Collections.IEnumerator ScreamAndRunSequence(Vector3 scareSource)
	{
		IsScreaming = true;

		// Play screaming animation
		if (animator != null)
		{
			animator.Play(screamingStateName);
		}

		// Wait for scream animation duration (extended)
		yield return new WaitForSeconds(screamDuration);

		// Calculate direction away from the scare source
		Vector3 runDirection = (transform.position - scareSource).normalized;

		// Make sure we're moving in a valid 2D direction
		runDirection.z = 0;

		// If the direction is too small, pick a random direction
		if (runDirection.magnitude < 0.1f)
		{
			runDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
		}

		// Calculate escape target (shorter distance for slower movement)
		Vector3 escapeTarget = transform.position + runDirection * 0.1f; // Reduced from 20f

		// Temporarily increase movement speed for panic running (this is now a reduction)
		TemporarilyIncreaseSpeed();

		// Set destination to run away
		movementController.SetDestination(escapeTarget);

		// Extended time in screaming state
		yield return new WaitForSeconds(3); // Increased from 0.1f
		IsScreaming = false;
	}


	private void TemporarilyIncreaseSpeed()
	{
		// Use reflection to access and modify the private moveSpeed field
		System.Reflection.FieldInfo speedField = typeof(HumanMovementController)
			.GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (speedField != null)
		{
			float currentSpeed = (float)speedField.GetValue(movementController);
			speedField.SetValue(movementController, currentSpeed * panicRunSpeedMultiplier);
		}
	}

	private void OnDestroy()
	{
		// Restore original speed before destruction (if needed)
		System.Reflection.FieldInfo speedField = typeof(HumanMovementController)
			.GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (speedField != null && movementController != null)
		{
			speedField.SetValue(movementController, originalMoveSpeed);
		}
	}
}