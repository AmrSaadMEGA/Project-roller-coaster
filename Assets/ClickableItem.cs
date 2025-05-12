using UnityEngine;

public class ClickableItem : MonoBehaviour
{
	public string itemName = "DefaultItem";

	void OnMouseDown()
	{
		InventoryManager.Instance.AddItem(itemName);
		InventoryManager.Instance.ShowInventory();
		Destroy(gameObject); // Remove the item from the scene after pickup
	}
}
