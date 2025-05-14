using UnityEngine;
using System.Collections;

public class ZombieController : MonoBehaviour
{
	[Header("Animation")]
	[SerializeField] private string eatingStateName = "Eating";
	[SerializeField] private string moveToSeatStateName = "MoveToSeat";

	[Header("Current Seat")]
	[SerializeField] private RollerCoasterCart currentCart;
	[SerializeField] private int currentSeatIndex = 0;

	[Header("Movement")]
	[SerializeField] private float seatSwitchDuration = 1.0f;

	[Header("Click Detection")]
	[SerializeField] private float seatClickRadius = 1.0f;
	[SerializeField] private float humanClickRadius = 1.5f;

	private Animator animator;
	private HumanStateController targetHuman;
	private bool isMovingBetweenSeats = false;

	// Debug
	[SerializeField] private bool debugMode = true;

	void Awake()
	{
		animator = GetComponentInChildren<Animator>();
		if (animator == null)
			Debug.LogError("ZombieController requires an Animator component.");
	}

	void Start()
	{
		if (currentCart == null)
		{
			Debug.LogError("ZombieController requires a RollerCoasterCart reference.");
			return;
		}

		// Position zombie at the starting seat
		if (currentSeatIndex >= 0 && currentSeatIndex < currentCart.seats.Length)
		{
			transform.position = currentCart.seats[currentSeatIndex].transform.position;
		}
	}

	void Update()
	{
		if (isMovingBetweenSeats)
			return;

		if (Input.GetMouseButtonDown(0))
		{
			// Get mouse position in world space
			Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

			// First check if we're clicking on an adjacent seat in the same cart
			if (TrySwitchToClickedSeat(mousePosition))
				return;

			// Otherwise, try to eat a human
			TryEatHuman(mousePosition);
		}
	}

	private bool TrySwitchToClickedSeat(Vector2 mousePosition)
	{
		// For each seat in the current cart
		for (int i = 0; i < currentCart.seats.Length; i++)
		{
			// Skip the current seat
			if (i == currentSeatIndex)
				continue;

			RollerCoasterSeat seat = currentCart.seats[i];

			// Check if mouse is close enough to the seat
			float distanceToSeat = Vector2.Distance(mousePosition, seat.transform.position);
			if (distanceToSeat < seatClickRadius) // Now using configurable radius
			{
				// Check if seats are adjacent (in a two-seat setup)
				if (currentCart.TryGetAdjacentSeat(currentSeatIndex, out int adjacentSeatIndex) &&
					i == adjacentSeatIndex)
				{
					if (debugMode)
						Debug.Log($"Moving to seat {i}");

					StartCoroutine(SwitchSeat(i));
					return true;
				}
			}
		}

		if (debugMode)
			Debug.Log("No valid seat clicked");

		return false;
	}

	private void TryEatHuman(Vector2 mousePosition)
	{
		// Find all humans in the scene
		HumanSeatOccupant[] allHumans = FindObjectsOfType<HumanSeatOccupant>();

		foreach (HumanSeatOccupant human in allHumans)
		{
			// Make sure the human has a valid occupied seat
			if (human.OccupiedSeat == null)
				continue;

			// Get the cart this human belongs to
			RollerCoasterCart humanCart = human.OccupiedSeat.GetComponentInParent<RollerCoasterCart>();
			if (humanCart == null || humanCart == currentCart)
				continue; // Skip if no cart or same cart as zombie

			// Get the seat index of the human
			int humanSeatIndex = humanCart.GetSeatIndex(human.OccupiedSeat);

			// CRITICAL CHECK: Only attack if the human is in the SAME seat position
			if (humanSeatIndex == currentSeatIndex)
			{
				// Check if the mouse click is close to this human
				float distanceToMouse = Vector2.Distance(mousePosition, human.transform.position);

				if (debugMode)
					Debug.Log($"Found possible target: {human.name} in seat {humanSeatIndex}, distance to mouse: {distanceToMouse}");

				// We only target this human if the click is reasonably close to them
				if (distanceToMouse < humanClickRadius) // Now using configurable radius
				{
					if (debugMode)
						Debug.Log($"VALID TARGET: {human.name} in same seat position (seat {humanSeatIndex}) as zombie (seat {currentSeatIndex})");

					// Get the human state controller
					HumanStateController humanController = human.GetComponent<HumanStateController>();
					if (humanController != null && humanController.IsVulnerable())
					{
						targetHuman = humanController;
						if (animator != null)
							animator.Play(eatingStateName);
						return;
					}
					else
					{
						if (debugMode)
							Debug.Log("Human found but not vulnerable");
					}
				}
			}
		}

		if (debugMode)
			Debug.Log("No valid human targets found in the clicked position");
	}

	private IEnumerator SwitchSeat(int newSeatIndex)
	{
		isMovingBetweenSeats = true;

		// Play move animation if available
		if (animator != null && !string.IsNullOrEmpty(moveToSeatStateName))
		{
			animator.Play(moveToSeatStateName);
		}

		// Get start and target positions
		Vector3 startPos = transform.position;
		Vector3 targetPos = currentCart.seats[newSeatIndex].transform.position;

		// Move over time
		float elapsedTime = 0f;
		while (elapsedTime < seatSwitchDuration)
		{
			transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / seatSwitchDuration);
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Ensure final position is exact
		transform.position = targetPos;

		// Update current seat
		currentSeatIndex = newSeatIndex;
		isMovingBetweenSeats = false;
	}
	// This method will be called by the ZombieAnimationHandler
	public void HandleEatingAnimationEnd()
	{
		if (targetHuman != null)
		{
			targetHuman.Die();
			targetHuman = null;
		}
	}

	// Visualize the seats in the editor
	void OnDrawGizmos()
	{
		if (currentCart != null)
		{
			// Draw current seat position
			if (currentSeatIndex >= 0 && currentSeatIndex < currentCart.seats.Length)
			{
				RollerCoasterSeat seat = currentCart.seats[currentSeatIndex];
				if (seat != null)
				{
					// Draw current seat in yellow
					Gizmos.color = Color.yellow;
					Gizmos.DrawWireSphere(seat.transform.position, 0.5f);
				}
			}

			// Draw all seats in the cart
			for (int i = 0; i < currentCart.seats.Length; i++)
			{
				if (i == currentSeatIndex) continue; // Skip current seat

				RollerCoasterSeat seat = currentCart.seats[i];
				if (seat != null)
				{
					// Draw other seats in cyan
					Gizmos.color = Color.cyan;
					Gizmos.DrawWireSphere(seat.transform.position, 0.3f);
				}
			}

			// Visualize click radius for debugging
			if (debugMode)
			{
				// Show human click radius in red
				Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
				Gizmos.DrawWireSphere(transform.position, humanClickRadius);
			}
		}
	}
}