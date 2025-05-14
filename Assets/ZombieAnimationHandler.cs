using UnityEngine;

/// <summary>
/// This component handles animation events from the Zombie's Animator
/// </summary>
public class ZombieAnimationHandler : MonoBehaviour
{
	private ZombieController zombieController;

	void Awake()
	{
		// Get the zombie controller from the parent (or this game object)
		zombieController = GetComponentInParent<ZombieController>();

		if (zombieController == null)
		{
			Debug.LogError("ZombieAnimationHandler couldn't find a ZombieController component");
		}
	}

	// Animation event for when eating animation ends
	public void OnEatingAnimationEnd()
	{
		if (zombieController != null)
		{
			zombieController.HandleEatingAnimationEnd();
		}
	}

	// Animation event for when throwing animation ends
	public void OnThrowingAnimationEnd()
	{
		// This method will be called by the animator when the throwing animation completes
		// Currently, the zombie controller's throwing coroutine handles this timing
	}

	// Animation event for when cross-cart movement animation ends
	public void OnCrossCartMoveEnd()
	{
		// This method will be called by the animator when the cross-cart move animation completes
		// Currently, the zombie controller's cart switch coroutine handles this timing
	}
}