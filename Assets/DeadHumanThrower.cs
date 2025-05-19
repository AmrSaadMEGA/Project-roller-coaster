using UnityEngine;
using System.Collections;

/// <summary>
/// Extension to HumanStateController for handling the throwing of dead humans
/// </summary>
public class DeadHumanThrower : MonoBehaviour
{
	[Header("Throwing Animation")]
	[Tooltip("Name of the throwing animation state in the Animator")]
	[SerializeField] private string throwingStateName = "Throwing";

	[Tooltip("Duration of the throw animation before the human disappears")]
	[SerializeField] private float throwDuration = 1.5f;

	[Tooltip("Arc height of the throw animation")]
	[SerializeField] private float throwArcHeight = 2f;

	[Tooltip("How far the human travels horizontally during throw")]
	[SerializeField] private float throwDistance = 3f;

	[Tooltip("Direction to throw (usually away from the track)")]
	[SerializeField] private Vector2 throwDirection = Vector2.right;

	[Header("Debug")]
	[SerializeField] private bool debugMode = false;

	private HumanStateController humanController;
	private HumanSeatOccupant seatOccupant;
	private Animator animator;
	private bool isBeingThrown = false;

	private void Awake()
	{
		humanController = GetComponent<HumanStateController>();
		seatOccupant = GetComponent<HumanSeatOccupant>();
		animator = GetComponent<Animator>();
	}

	/// <summary>
	/// Called by ZombieController to throw this human
	/// </summary>
	public void ThrowHuman(Vector2 zombiePosition)
	{
		// Double-check that the human is dead and can be thrown
		if (!humanController.IsDead() || isBeingThrown)
		{
			Debug.LogWarning($"Cannot throw {gameObject.name}: IsDead={humanController.IsDead()}, isBeingThrown={isBeingThrown}");
			return;
		}

		if (debugMode)
			Debug.Log($"Starting throw animation for {gameObject.name}");

		// Empty the seat first
		if (seatOccupant != null && seatOccupant.OccupiedSeat != null)
		{
			RollerCoasterSeat seat = seatOccupant.OccupiedSeat;
			seat.occupyingHuman = null;

			if (debugMode)
				Debug.Log($"Emptied seat for {gameObject.name}");
		}

		// Play throw animation
		if (animator != null)
		{
			animator.Play(throwingStateName);
		}

		// Calculate throw direction based on zombie position
		Vector2 actualThrowDirection = new Vector2(
			zombiePosition.x > transform.position.x ? throwDirection.x : -throwDirection.x,
			throwDirection.y
		);

		// Start the throwing movement
		StartCoroutine(ThrowAnimation(actualThrowDirection));
	}

	/// <summary>
	/// Animates the human being thrown in an arc
	/// </summary>
	private IEnumerator ThrowAnimation(Vector2 direction)
	{
		isBeingThrown = true;

		Vector3 startPosition = transform.position;
		Vector3 targetPosition = startPosition + new Vector3(direction.x * throwDistance, 0, 0);

		float elapsedTime = 0f;

		while (elapsedTime < throwDuration)
		{
			float normalizedTime = elapsedTime / throwDuration;

			// Calculate position along a parabolic arc
			float height = Mathf.Sin(normalizedTime * Mathf.PI) * throwArcHeight;

			// Lerp horizontal position and add vertical arc
			Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, normalizedTime);
			newPosition.y = startPosition.y + height;

			transform.position = newPosition;

			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Destroy the human or deactivate it after the throw is complete
		if (debugMode)
			Debug.Log($"Throw animation complete for {gameObject.name}, destroying object");

		Destroy(gameObject);
	}

	/// <summary>
	/// Check if this human can be thrown (must be dead and not already being thrown)
	/// </summary>
	public bool CanBeThrown()
	{
		if (humanController == null) return false;

		if (debugMode)
		{
			Debug.Log($"{gameObject.name} throw check: " +
					 $"IsDead={humanController.IsDead()} " +
					 $"State={humanController.CurrentStateAsString}");
		}

		// Explicit state validation
		return humanController.IsDead() &&
			  humanController.CurrentStateAsString == "Dead" && // Double-check
			  !isBeingThrown;
	}
}