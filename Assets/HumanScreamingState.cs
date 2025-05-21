using System.Collections;
using UnityEngine;

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

	// Add to HumanScreamingState class
	[SerializeField] private string jumpStateName = "Jump";
	[SerializeField] private float jumpHeight = 2f;
	[SerializeField] private float jumpDuration = 0.5f;
	public bool IsScreaming { get; set; }
	public void ForceJumpAndDespawn()
	{
		StopAllCoroutines();
		StartCoroutine(ImmediateJumpSequence());
	}

	private IEnumerator ImmediateJumpSequence()
	{
		// Immediate state cleanup
		IsScreaming = true;
		animator.Play(jumpStateName);

		// Disable all interactions
		Collider2D[] colliders = GetComponents<Collider2D>();
		foreach (Collider2D col in colliders) col.enabled = false;

		// Teleport to jump start position
		Vector3 startPos = transform.position;
		Vector3 peakPos = startPos + Vector3.up * jumpHeight;

		// Instant jump visualization
		transform.position = peakPos;
		yield return new WaitForSeconds(0.1f);
		Destroy(gameObject);
	}
	public void JumpAndDespawn()
	{
		if (IsScreaming) return;

		// Cancel any existing movement
		HumanMovementController movement = GetComponent<HumanMovementController>();
		if (movement != null)
		{
			movement.StopMoving();
			movement.enabled = false;
		}

		// Clear seat occupation
		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();
		if (occupant != null)
		{
			occupant.LeaveSeat();
			Destroy(occupant);
		}

		StartCoroutine(JumpSequence());
	}
	private System.Collections.IEnumerator JumpSequence()
	{
		IsScreaming = true;
		if(transform.tag == "Male")
		{
			AudioHandler.instance.Play("Man Jump");
		}
		else if (transform.tag == "Female")
		{
			AudioHandler.instance.Play("Woman Jump");
		}
		animator.Play(jumpStateName);

		// Disable collider to prevent further interactions
		Collider2D col = GetComponent<Collider2D>();
		if (col != null) col.enabled = false;

		Vector3 startPos = transform.position;
		Vector3 peakPos = startPos + Vector3.up * jumpHeight;

		float elapsed = 0;
		while (elapsed < jumpDuration / 2)
		{
			transform.position = Vector3.Lerp(startPos, peakPos, elapsed / (jumpDuration / 2));
			elapsed += Time.deltaTime;
			yield return null;
		}

		elapsed = 0;
		while (elapsed < jumpDuration / 2)
		{
			transform.position = Vector3.Lerp(peakPos, startPos, elapsed / (jumpDuration / 2));
			elapsed += Time.deltaTime;
			yield return null;
		}

		Destroy(gameObject);
	}
	public bool IsSeated()
	{
		// Correct seated check logic
		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();
		return occupant != null && occupant.IsSeated;
	}
	private void Awake()
	{
		animator = GetComponentInChildren<Animator>();
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
	public void ScreamAndRunAway(Vector3 threatPosition, bool forceReact = false)
	{
		HumanStateController state = GetComponent<HumanStateController>();
		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();

		if (state != null && state.IsBeingDespawned) return;
		if ((IsScreaming && !forceReact) ||
			stateController.IsDead() ||
			(occupant != null && occupant.IsSeated)) // Add seated check
		{
			return;
		}

		StopAllCoroutines();
		StartCoroutine(ScreamAndRunSequence(threatPosition));
	}

	private System.Collections.IEnumerator ScreamAndRunSequence(Vector3 scareSource)
	{
		IsScreaming = true;

		// Play screaming animation
		if (animator != null)
		{
			animator.Play(screamingStateName);
			if (transform.tag == "Male")
			{
				AudioHandler.instance.Play("Man Scream");
			}
			else if (transform.tag == "Female")
			{
				AudioHandler.instance.Play("Woman Scream");
			}
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