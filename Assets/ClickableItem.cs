using UnityEngine;

public class ClickableItem : MonoBehaviour
{
	[Tooltip("Must match one of the names in InventoryManager → All Items")]
	public string itemName = "DefaultItem";

	private void OnMouseDown()
	{
		// Add to inventory
		InventoryManager.Instance.AddItem(itemName);
		// Optional: log full inventory to console
		InventoryManager.Instance.ShowInventory();
		// Remove from scene
		Destroy(gameObject);
	}
}
