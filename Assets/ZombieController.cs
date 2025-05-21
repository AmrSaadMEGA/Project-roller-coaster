using UnityEngine;
using System.Collections;

public class ZombieController : MonoBehaviour
{
	[Header("Animation")]
	[SerializeField] private string eatingStateName = "Eating";
	[SerializeField] private string eatingAttemptStateName = "EatingAttempt";
	[SerializeField] private string moveToSeatStateName = "MoveToSeat";
	[SerializeField] private string throwingStateName = "Throwing";
	[SerializeField] private string crossCartMoveStateName = "CrossCartMove";

	[Header("Current Seat")]
	[SerializeField] private RollerCoasterCart currentCart;
	[SerializeField] private int currentSeatIndex = 0;

	[Header("Movement")]
	[SerializeField] private float seatSwitchDuration = 1.0f;
	[SerializeField] private float cartSwitchDuration = 2.0f;
	[SerializeField] private float adjacentCartDistance = 3.0f; // Maximum distance for adjacent carts

	[Header("Click Detection")]
	[SerializeField] private float seatClickRadius = 1.0f;
	[SerializeField] private float humanClickRadius = 1.5f;
	[SerializeField] private float deadHumanClickRadius = 1.8f;

	[Header("Throwing")]
	[SerializeField] private float throwAnimationDuration = 1.0f;

	private Animator animator;
	private HumanStateController targetHuman;
	private bool isEating = false;
	private bool isMovingBetweenSeats = false;
	private bool isThrowing = false;

	// Debug
	[SerializeField] private bool debugMode = true;

	[SerializeField] private ZombieHidingSystem hidingSystem;
	private RollerCoasterSeat[] allSeats;


	void Awake()
	{
		RollerCoasterSeat[] allSeats = Object.FindObjectsByType<RollerCoasterSeat>(
			FindObjectsInactive.Include,
			FindObjectsSortMode.None
		);

		hidingSystem = GetComponent<ZombieHidingSystem>();
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
		if (isEating)
			return;
		// Handle mouse input
		if (Input.GetMouseButtonDown(0))
		{
			Vector2 inputPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			ProcessInput(inputPosition);
		}

		// Handle touch input
		if (Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			if (touch.phase == TouchPhase.Began)
			{
				Vector2 inputPosition = Camera.main.ScreenToWorldPoint(touch.position);
				ProcessInput(inputPosition);
			}
		}
	}

	private void ProcessInput(Vector2 inputPosition)
	{

		// Get mouse position in world space
		Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

		if (debugMode)
			Debug.Log($"Mouse clicked at position: {mousePosition}");

		if (hidingSystem != null && hidingSystem.IsHidden)
		{
			if (TryUnhideToSeat(mousePosition))
			{
				if (debugMode)
					Debug.Log("Unhiding via seat click");
				return;
			}
		}
		else
		{

			// First, prioritize attacking vulnerable humans directly in front of the zombie
			if (TryEatHuman(mousePosition))
			{
				if (debugMode)
					Debug.Log("Successfully found human to eat");
				return;
			}

			// Next check if we're clicking on an adjacent seat in the same cart
			if (TrySwitchToClickedSeat(mousePosition))
			{
				if (debugMode)
					Debug.Log("Successfully found seat to switch to in same cart");
				return;
			}

			// Next check if we're clicking on an empty seat in another cart
			if (TrySwitchToAnotherCart(mousePosition))
			{
				if (debugMode)
					Debug.Log("Successfully found seat to switch to in another cart");
				return;
			}

			// Last priority: checking if we're clicking on a dead human to throw
			if (TryThrowDeadHuman(mousePosition))
			{
				if (debugMode)
					Debug.Log("Successfully found dead human to throw");
				return;
			}

			if (debugMode)
				Debug.Log("No valid action found for this click");
		}
	}

	private bool TryUnhideToSeat(Vector2 mousePosition)
	{
		RollerCoasterCart[] allCarts = Object.FindObjectsByType<RollerCoasterCart>(FindObjectsInactive.Include, FindObjectsSortMode.None);

		foreach (RollerCoasterCart cart in allCarts)
		{
			for (int i = 0; i < cart.seats.Length; i++)
			{
				RollerCoasterSeat seat = cart.seats[i];
				float distance = Vector2.Distance(mousePosition, seat.transform.position);

				if (distance < seatClickRadius && !seat.isOccupied)
				{
					// Remove: hidingSystem.SetHideState(false);
					ClearAllZombieSeats();
					StartCoroutine(SwitchCart(cart, i));
					return true;
				}
			}
		}

		return false;
	}
	private IEnumerator FinishEatingAttempt()
	{
		float eatAnimLength = GetAnimationDuration(animator, eatingAttemptStateName);
		yield return new WaitForSeconds(eatAnimLength);
		isEating = false;
	}
	private IEnumerator FinishEating()
	{
		float eatAnimLength = GetAnimationDuration(animator, eatingStateName);
		yield return new WaitForSeconds(eatAnimLength);
		isEating = false;
	}
	private bool TryThrowDeadHuman(Vector2 mousePosition)
	{

		// Find all humans in the scene
		HumanSeatOccupant[] allHumans = Object.FindObjectsByType<HumanSeatOccupant>(FindObjectsInactive.Include, FindObjectsSortMode.None);

		// Track the closest dead human that was directly clicked
		HumanSeatOccupant clickedHuman = null;
		DeadHumanThrower clickedThrower = null;
		float closestDistance = deadHumanClickRadius; // Initialize with max allowed distance

		foreach (HumanSeatOccupant human in allHumans)
		{
			// Add early exit for living humans
			HumanStateController humanState = human.GetComponent<HumanStateController>();
			if (humanState != null && !humanState.IsDead())
			{
				// To this:
				if (humanState != null && !humanState.IsDead())
				{
					continue; // ✅ Correct - skips living humans
				}
			}
			// First check if the mouse is directly clicking on this human
			float distanceToMouse = Vector2.Distance(mousePosition, human.transform.position);

			// CRITICAL FIX: Only consider humans who were DIRECTLY clicked on
			// This prevents the zombie from throwing corpses the player didn't click on
			if (distanceToMouse > deadHumanClickRadius)
			{
				// Skip if the player didn't click directly on this human
				continue;
			}

			if (debugMode)
				Debug.Log($"Checking dead human {human.name}, distance to mouse: {distanceToMouse}");

			// Skip if no seat or no DeadHumanThrower component
			if (human.OccupiedSeat == null)
				continue;

			// Skip if not throwable
			DeadHumanThrower thrower = human.GetComponent<DeadHumanThrower>();
			if (thrower == null || !thrower.CanBeThrown())
				continue;

			// Get the cart and seat index of the human
			RollerCoasterCart humanCart = human.OccupiedSeat.GetComponentInParent<RollerCoasterCart>();
			if (humanCart == null)
				continue;

			int humanSeatIndex = humanCart.GetSeatIndex(human.OccupiedSeat);

			// CRITICAL CHANGE: For throwing, we want to target humans in the same seat position 
			// but in front of the zombie (not necessarily in the same cart)
			bool isInSameSeatPosition = (humanSeatIndex == currentSeatIndex);
			bool isInFrontCart = IsCartInFront(currentCart, humanCart);

			if (!isInSameSeatPosition)
			{
				if (debugMode)
					Debug.Log($"Click was on dead human {human.name}, but it's not in the correct seat position. Human seat: {humanSeatIndex}, Zombie seat: {currentSeatIndex}");
				continue;
			}

			if (!isInFrontCart)
			{
				if (debugMode)
					Debug.Log($"Click was on dead human {human.name}, but it's not in a cart in front of the zombie.");
				continue;
			}

			// If this is closer than any previously found valid target, use it
			if (distanceToMouse < closestDistance)
			{
				closestDistance = distanceToMouse;
				clickedHuman = human;
				clickedThrower = thrower;

				if (debugMode)
					Debug.Log($"Click detected on valid throwable dead human: {human.name}, distance: {distanceToMouse}");
			}
		}

		// If we found a valid dead human to throw
		if (clickedHuman != null && clickedThrower != null)
		{
			if (debugMode)
				Debug.Log($"Throwing dead human: {clickedHuman.name}, distance: {closestDistance}");

			// Start throwing animation for zombie
			StartCoroutine(ThrowHuman(clickedHuman, clickedThrower));
			return true;
		}

		return false;
	}

	// Helper method to determine if targetCart is in front of sourceCart
	private bool IsCartInFront(RollerCoasterCart sourceCart, RollerCoasterCart targetCart)
	{
		// Get the cart positions
		Vector3 sourcePos = sourceCart.transform.position;
		Vector3 targetPos = targetCart.transform.position;

		// FIXED: For this game, "in front" means the target cart has a LARGER x position
		// (carts move left to right, or front is to the right)
		bool isFront = targetPos.x > sourcePos.x;

		if (debugMode)
			Debug.Log($"Cart check: Is {targetCart.name} in front of {sourceCart.name}? {isFront} (Source X: {sourcePos.x}, Target X: {targetPos.x})");

		return isFront;
	}

	// NEW METHOD: Check if two carts are adjacent to each other
	private bool AreCartsAdjacent(RollerCoasterCart cart1, RollerCoasterCart cart2)
	{
		float distance = Vector3.Distance(cart1.transform.position, cart2.transform.position);

		// Check if the distance is within our defined threshold
		bool areAdjacent = distance <= adjacentCartDistance;

		if (debugMode)
			Debug.Log($"Cart adjacency check: {cart1.name} and {cart2.name} - Distance: {distance}, Adjacent: {areAdjacent}");

		return areAdjacent;
	}

	private IEnumerator ThrowHuman(HumanSeatOccupant human, DeadHumanThrower thrower)
	{
		isThrowing = true;

		// Play throw animation for zombie
		if (animator != null)
			animator.Play(throwingStateName);

		// Tell the human to start its throw animation
		thrower.ThrowHuman(transform.position);

		// Wait for throw animation to complete
		yield return new WaitForSeconds(throwAnimationDuration);

		isThrowing = false;
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

			// CRITICAL FIX: Check if the seat is already occupied
			if (seat.isOccupied)
			{
				if (debugMode)
					Debug.Log($"Seat {i} in current cart is occupied, can't move there");
				continue; // Skip if seat is already occupied
			}

			// Check if mouse is close enough to the seat
			float distanceToSeat = Vector2.Distance(mousePosition, seat.transform.position);
			if (distanceToSeat < seatClickRadius)
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

	private bool TrySwitchToAnotherCart(Vector2 mousePosition)
	{
		// Find all carts in the scene
		RollerCoasterCart[] allCarts = Object.FindObjectsByType<RollerCoasterCart>(FindObjectsInactive.Include, FindObjectsSortMode.None);

		foreach (RollerCoasterCart cart in allCarts)
		{
			// Skip current cart
			if (cart == currentCart)
				continue;

			// NEW FIX: Only allow movement to adjacent carts
			if (!AreCartsAdjacent(currentCart, cart))
			{
				if (debugMode)
					Debug.Log($"Cart {cart.name} is not adjacent to current cart, skipping");
				continue;
			}

			// Check each seat in the cart
			for (int i = 0; i < cart.seats.Length; i++)
			{
				// Only consider the same seat index as we're currently in
				if (i != currentSeatIndex)
					continue;

				RollerCoasterSeat seat = cart.seats[i];

				// CRITICAL FIX: Explicit check if seat is occupied
				if (seat.isOccupied)
				{
					if (debugMode)
						Debug.Log($"Seat {i} in cart {cart.name} is occupied, can't move there");
					continue; // Skip if seat is occupied
				}

				// Check if mouse is close enough to the seat
				float distanceToSeat = Vector2.Distance(mousePosition, seat.transform.position);
				if (distanceToSeat < seatClickRadius)
				{
					if (debugMode)
						Debug.Log($"Moving to cart {cart.name}, seat {i}");
					if (FindFirstObjectByType<RollerCoasterGameManager>().CurrentState != RollerCoasterGameManager.GameState.ZombieBoarding)
					{
						GetComponent<ZombieHidingSystem>().SetHideState(false);
					}
					ClearAllZombieSeats();
					StartCoroutine(SwitchCart(cart, i));
					return true;
				}
			}
		}

		return false;
	}

	private bool TryEatHuman(Vector2 mousePosition)
	{
		// Find all humans in the scene
		HumanSeatOccupant[] allHumans = Object.FindObjectsByType<HumanSeatOccupant>(FindObjectsInactive.Include, FindObjectsSortMode.None);

		// Track the human that was directly clicked
		HumanSeatOccupant clickedHuman = null;
		HumanStateController clickedController = null;
		float closestDistance = humanClickRadius;

		foreach (HumanSeatOccupant human in allHumans)
		{
			// First check if the mouse is directly clicking on this human
			float distanceToMouse = Vector2.Distance(mousePosition, human.transform.position);
			if (distanceToMouse > humanClickRadius)
			{
				continue; // Skip if the player didn't click directly on this human
			}

			// Make sure the human has a valid occupied seat
			if (human.OccupiedSeat == null)
				continue;

			// Get the cart this human belongs to
			RollerCoasterCart humanCart = human.OccupiedSeat.GetComponentInParent<RollerCoasterCart>();
			if (humanCart == null)
				continue; // Skip if no cart

			// Skip if the human is in the same cart as the zombie
			if (humanCart == currentCart)
			{
				if (debugMode)
					Debug.Log($"Human {human.name} is in the same cart as the zombie.");
				continue;
			}

			// Check if the human's cart is adjacent and in front of the current cart
			bool isAdjacent = AreCartsAdjacent(currentCart, humanCart);
			bool isInFront = IsCartInFront(currentCart, humanCart);

			if (!isAdjacent || !isInFront)
			{
				if (debugMode)
					Debug.Log($"Human {human.name} is in a non-adjacent or non-front cart. Adjacent: {isAdjacent}, In Front: {isInFront}");
				continue;
			}

			// Get the seat index of the human
			int humanSeatIndex = humanCart.GetSeatIndex(human.OccupiedSeat);

			// Check if the human is in the same seat position as the zombie
			if (humanSeatIndex != currentSeatIndex)
			{
				if (debugMode)
					Debug.Log($"Human {human.name} is in seat {humanSeatIndex}, which doesn't match zombie's seat {currentSeatIndex}.");
				continue;
			}

			// MODIFIED: Remove vulnerability check here
			HumanStateController humanController = human.GetComponent<HumanStateController>();
			if (humanController == null)
			{
				if (debugMode)
					Debug.Log($"Human {human.name} has no state controller");
				continue;
			}

			// Track the closest valid target
			if (distanceToMouse < closestDistance)
			{
				closestDistance = distanceToMouse;
				clickedHuman = human;
				clickedController = humanController;

				if (debugMode)
					Debug.Log($"VALID TARGET: {human.name} in adjacent front cart (seat {humanSeatIndex})");
			}
		}

		// If a valid human is found, attack them
		// In TryEatHuman method, replace the attack section with:
		if (clickedHuman != null && clickedController != null)
		{
			RollerCoasterCart targetCart = clickedHuman.OccupiedSeat.GetComponentInParent<RollerCoasterCart>();
			int targetSeatIndex = targetCart.GetSeatIndex(clickedHuman.OccupiedSeat);

			// Force reset screaming state for seated humans
			HumanScreamingState screamState = clickedHuman.GetComponent<HumanScreamingState>();
			if (screamState != null && clickedHuman.IsSeated)
			{
				screamState.StopAllCoroutines();
				screamState.IsScreaming = false;
				Animator humanAnimator = clickedHuman.GetComponent<Animator>();
				if (humanAnimator != null) humanAnimator.Play("Idle");
			}

			if (clickedController.IsDefensive())
			{
				animator.Play(eatingAttemptStateName);
				bool isEatPlayed = false;
				foreach (string audioSource in AudioHandler.instance.playingSoundNames)
				{
					if (audioSource == "Eat Attempt" || audioSource == "Eat Attemp")
					{
						isEatPlayed = true;
						break;
					}
				}
				if (isEatPlayed == true)
				{
					AudioHandler.instance.Stop("Eat");
					AudioHandler.instance.Stop("Eat Attempt");
					AudioHandler.instance.Play("Eat Attempt");
				}
				else
				{
					AudioHandler.instance.Play("Eat Attempt");
				}
				FinishEatingAttempt();
				clickedController.EscapeAndDespawn();
				StartCoroutine(CheckAdjacentAfterEscape(targetCart, targetSeatIndex));
				return true;
			}
			else if (clickedController.IsVulnerable())
			{
				animator.Play(eatingStateName);
				bool isEatPlayed = false;
				foreach (string audioSource in AudioHandler.instance.playingSoundNames)
				{
					if (audioSource == "Eat" || audioSource == "Eat Attemp")
					{
						isEatPlayed = true;
						break;
					}
				}
				if (isEatPlayed == true)
				{
					AudioHandler.instance.Stop("Eat Attempt");
					AudioHandler.instance.Stop("Eat");
					AudioHandler.instance.Play("Eat");
				}
				else
				{
					AudioHandler.instance.Play("Eat");
				}
				targetHuman = clickedController;
				FinishEating();
				StartCoroutine(CheckAdjacentAfterEscape(targetCart, targetSeatIndex));
				return true;
			}
		}
		return false;
	}
	private IEnumerator CheckAdjacentAfterEscape(RollerCoasterCart cart, int seatIndex)
	{
		yield return new WaitForSeconds(0.3f);

		if (cart.TryGetAdjacentSeat(seatIndex, out int adjacentIndex))
		{
			RollerCoasterSeat adjacentSeat = cart.seats[adjacentIndex];
			HumanStateController adjacentHuman = adjacentSeat.occupyingHuman;

			if (adjacentHuman != null && adjacentHuman.IsDefensive())
			{
				adjacentHuman.EscapeAndDespawn();
			}
		}
	}
	private IEnumerator SwitchSeat(int newSeatIndex)
	{
		// Mark zombie as visible when moving
		ZombieHidingSystem hidingSystem = GetComponent<ZombieHidingSystem>();
		if (hidingSystem != null) hidingSystem.SetHideState(false);

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

	private IEnumerator SwitchCart(RollerCoasterCart newCart, int newSeatIndex)
	{
		ZombieHidingSystem hidingSystem = GetComponent<ZombieHidingSystem>();
		bool wasHidden = hidingSystem != null && hidingSystem.IsHidden;

		// 1. Handle unhiding first
		if (wasHidden)
		{
			// Play unhide animation
			if (animator != null && !string.IsNullOrEmpty(hidingSystem.UnhideAnimationState))
			{
				animator.Play(hidingSystem.UnhideAnimationState);
				yield return new WaitForSeconds(GetAnimationDuration(animator, hidingSystem.UnhideAnimationState));
			}

			// Update hidden state AFTER animation
			hidingSystem.SetHideState(false);
		}

		// 2. Handle movement animations
		isMovingBetweenSeats = true;

		// Play cart move animation only if not coming from hidden state
		if (!wasHidden && animator != null && !string.IsNullOrEmpty(crossCartMoveStateName))
		{
			animator.Play(crossCartMoveStateName);
		}

		// 3. Movement logic (unchanged)
		Vector3 startPos = transform.position;
		Vector3 targetPos = newCart.seats[newSeatIndex].transform.position;

		float elapsedTime = 0f;
		while (elapsedTime < cartSwitchDuration)
		{
			float normalizedTime = elapsedTime / cartSwitchDuration;
			float height = Mathf.Sin(normalizedTime * Mathf.PI) * 0.5f;
			Vector3 newPosition = Vector3.Lerp(startPos, targetPos, normalizedTime);
			newPosition.y += height;
			transform.position = newPosition;
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// 4. Finalize
		newCart.seats[newSeatIndex].SetZombieOccupation(true);
		currentCart = newCart;
		currentSeatIndex = newSeatIndex;
		transform.position = targetPos;
		isMovingBetweenSeats = false;
	}
	private float GetAnimationDuration(Animator animator, string stateName)
	{
		if (animator == null) return 0f;

		RuntimeAnimatorController ac = animator.runtimeAnimatorController;
		foreach (AnimationClip clip in ac.animationClips)
		{
			if (clip.name == stateName)
			{
				return clip.length;
			}
		}
		return 0f;
	}
	public void ClearAllZombieSeats()
	{
		if (allSeats == null) return;

		foreach (RollerCoasterSeat seat in allSeats)
		{
			if (seat != null)
			{
				seat.SetZombieOccupation(false);
				Debug.Log($"Cleared zombie occupation flag for seat: {seat.name}");
			}
		}
	}
	// Call this when hiding starts
	public void PrepareToHide()
	{
		ClearAllZombieSeats();
	}
	// This method will be called by the ZombieAnimationHandler
	public void HandleEatingAnimationEnd()
	{
		if (targetHuman != null)
		{
			targetHuman.Die(true); // Pass true for zombie-caused death
			ZombieHungerSystem hungerSystem = FindFirstObjectByType<ZombieHungerSystem>();
			if (hungerSystem != null)
			{
				hungerSystem.DecreaseHunger();
			}
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

			// NEW: Visualize adjacent cart distance
			Gizmos.color = new Color(0f, 1f, 0f, 0.2f); // Transparent green
			Gizmos.DrawWireSphere(currentCart.transform.position, adjacentCartDistance);

			// Visualize click radius for debugging
			if (debugMode)
			{
				// Show human click radius in red
				Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
				Gizmos.DrawWireSphere(transform.position, humanClickRadius);

				// Show dead human click radius in purple
				Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.3f);
				Gizmos.DrawWireSphere(transform.position, deadHumanClickRadius);
			}
		}
	}
}