using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Photon.Pun;

/// <summary>
/// InventorySystem - FIXED VERSION
/// 
/// FIXES:
/// - Uses RPC to hide scene items on ALL clients when picked up
/// - Uses PhotonNetwork.Destroy for networked items
/// </summary>
public class InventorySystem : MonoBehaviourPun
{
    [Header("UI References")]
    public Image[] slotImages;

    [Header("Hand/Equip Settings")]
    public GameObject handPrefab;

    [Header("Inventory Settings")]
    public int maxSlots = 3;

    [Header("Throwing Settings")]
    public float throwForce = 15f;
    public float throwUpwardForce = 3f;
    public KeyCode throwKey = KeyCode.G;
    
    [Header("Consume/Use Settings")]
    public KeyCode consumeKey = KeyCode.C;
    
    [Header("Throw Point")]
    public Transform throwPoint;

    private List<ItemData> inventoryItems = new List<ItemData>();
    private int currentSlot = -1;
    private GameObject currentEquippedItem = null;

    public List<ItemData> items => inventoryItems;

    void Start()
    {
        if (throwPoint == null)
        {
            Transform found = transform.Find("ThrowPoint");
            if (found != null)
                throwPoint = found;
            else
            {
                GameObject throwPointObj = new GameObject("ThrowPoint");
                throwPointObj.transform.SetParent(transform);
                throwPointObj.transform.localPosition = new Vector3(0f, 1.2f, 1f);
                throwPoint = throwPointObj.transform;
            }
        }

        if (slotImages == null || slotImages.Length == 0 || slotImages[0] == null)
            AutoFindUISlots();

        UpdateUI();
    }

    void AutoFindUISlots()
    {
        List<Image> foundSlots = new List<Image>();
        try
        {
            GameObject[] taggedSlots = GameObject.FindGameObjectsWithTag("InventorySlot");
            System.Array.Sort(taggedSlots, (a, b) => a.name.CompareTo(b.name));
            foreach (GameObject slot in taggedSlots)
            {
                Image img = slot.GetComponent<Image>();
                if (img != null) foundSlots.Add(img);
            }
        }
        catch (UnityException) { }
        
        if (foundSlots.Count > 0)
            slotImages = foundSlots.ToArray();
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleEquipSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleEquipSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleEquipSlot(2);

        if (Input.GetKeyDown(throwKey) && currentSlot != -1)
            ThrowItem(currentSlot);
        
        if (Input.GetKeyDown(consumeKey) && currentSlot != -1)
            UseEquippedItem();
    }

    // ==================== ADD ITEM ====================
    public bool AddItem(Item item)
    {
        if (inventoryItems.Count >= maxSlots)
        {
            Debug.Log("Inventory full!");
            return false;
        }

        ItemData data = new ItemData(item.itemName, item.icon, item.itemPrefab);
        
        ManualItem manual = item.GetComponent<ManualItem>();
        bool isManualItem = (manual != null);
        
        if (isManualItem)
        {
            data.isManual = true;
            data.isConsumable = false;
            Debug.Log($"<color=cyan>[Inventory] ✓ Added MANUAL to inventory!</color>");
        }
        else
        {
            ConsumableItem consumable = item.GetComponent<ConsumableItem>();
            if (consumable != null)
            {
                data.isConsumable = true;
                data.consumableType = consumable.consumableType;
                data.effectDuration = consumable.effectDuration;
                data.effectAmount = consumable.effectAmount;
            }
        }
        
        inventoryItems.Add(data);
        Debug.Log($"✅ Added {data.itemName} to inventory");
        
        // For Manual: Sync collection state BEFORE hiding
        if (isManualItem)
        {
            int actorNumber = PhotonNetwork.IsConnected ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
            SyncManualCollected(actorNumber);
        }
        
        // ✅ FIXED: Hide/Destroy item for ALL players
        HideItemForAll(item.gameObject);
        
        UpdateUI();
        return true;
    }
    
    /// <summary>
    /// Hide an item for ALL players using RPC
    /// </summary>
    void HideItemForAll(GameObject itemObject)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Destroy(itemObject);
            return;
        }
        
        PhotonView itemPV = itemObject.GetComponent<PhotonView>();
        
        if (itemPV != null && itemPV.ViewID != 0)
        {
            // PhotonNetwork spawned object
            if (itemPV.IsMine || PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(itemObject);
                Debug.Log($"<color=green>[Inventory] PhotonNetwork.Destroy</color>");
            }
        }
        else
        {
            // Scene object - hide via RPC using object name
            string itemName = itemObject.name;
            photonView.RPC("RPC_HideItem", RpcTarget.All, itemName);
            Debug.Log($"<color=green>[Inventory] RPC_HideItem sent: {itemName}</color>");
        }
    }
    
    [PunRPC]
    void RPC_HideItem(string itemName)
    {
        Debug.Log($"<color=yellow>[Inventory] RPC_HideItem: {itemName}</color>");
        
        // Find ALL objects with this name and hide them
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name == itemName)
            {
                obj.SetActive(false);
                Debug.Log($"<color=green>[Inventory] Hidden: {obj.name}</color>");
            }
        }
    }

    // ==================== USE EQUIPPED ITEM ====================
    public void UseEquippedItem()
    {
        if (currentSlot < 0 || currentSlot >= inventoryItems.Count)
        {
            Debug.Log("[Inventory] No item equipped!");
            return;
        }
        
        ItemData itemData = inventoryItems[currentSlot];
        
        if (itemData.isManual)
        {
            Debug.Log($"<color=cyan>[Inventory] Opening Manual...</color>");
            ManualItem.OpenManualUI();
            return;
        }
        
        if (itemData.isConsumable)
        {
            Debug.Log($"[Inventory] Consuming {itemData.itemName}...");
            ApplyConsumableEffect(itemData);
            RemoveEquippedItem();
            return;
        }
        
        Debug.Log($"[Inventory] Using {itemData.itemName}");
    }
    
    public void ConsumeEquippedItem() => UseEquippedItem();
    
    void ApplyConsumableEffect(ItemData itemData)
    {
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller == null) return;
        
        switch (itemData.consumableType)
        {
            case ConsumableType.StaminaBoost:
                controller.ActivateStaminaBoost(itemData.effectDuration);
                break;
            case ConsumableType.StaminaRefill:
                controller.CurrentStamina = controller.MaxStamina;
                break;
        }
    }
    
    public void RemoveEquippedItem()
    {
        if (currentSlot < 0 || currentSlot >= inventoryItems.Count) return;
        
        if (currentEquippedItem != null)
        {
            Destroy(currentEquippedItem);
            currentEquippedItem = null;
        }
        
        inventoryItems.RemoveAt(currentSlot);
        currentSlot = -1;
        UpdateUI();
    }
    
    public ItemData GetEquippedItem()
    {
        if (currentSlot < 0 || currentSlot >= inventoryItems.Count) return null;
        return inventoryItems[currentSlot];
    }
    
    public int GetCurrentSlot() => currentSlot;
    public GameObject GetEquippedObject() => currentEquippedItem;

    // ==================== THROW ITEM ====================
    public void ThrowItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventoryItems.Count) return;

        ItemData itemData = inventoryItems[slotIndex];
        
        if (itemData.isManual)
        {
            Debug.Log("[Inventory] Cannot throw the manual!");
            return;
        }
        
        if (itemData.itemPrefab == null) return;

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + transform.forward + Vector3.up;
        GameObject thrownItem = Instantiate(itemData.itemPrefab, spawnPos, Quaternion.identity);

        Rigidbody rb = thrownItem.GetComponent<Rigidbody>();
        if (rb == null) rb = thrownItem.AddComponent<Rigidbody>();
        rb.isKinematic = false;

        Camera cam = Camera.main;
        Vector3 throwDir = cam != null ? cam.transform.forward : transform.forward;
        throwDir = (throwDir + Vector3.up * 0.2f).normalized;
        rb.AddForce(throwDir * throwForce + Vector3.up * throwUpwardForce, ForceMode.Impulse);

        Collider col = thrownItem.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
            Collider playerCol = GetComponent<Collider>();
            if (playerCol == null) playerCol = GetComponentInChildren<Collider>();
            if (playerCol != null)
            {
                Physics.IgnoreCollision(col, playerCol, true);
                StartCoroutine(ReenableCollision(col, playerCol, 0.5f));
            }
        }

        Item itemScript = thrownItem.GetComponent<Item>();
        if (itemScript == null) itemScript = thrownItem.AddComponent<Item>();
        itemScript.itemName = itemData.itemName;
        itemScript.icon = itemData.icon;
        itemScript.itemPrefab = itemData.itemPrefab;
        itemScript.enabled = true;

        inventoryItems.RemoveAt(slotIndex);
        
        if (currentSlot == slotIndex)
        {
            if (currentEquippedItem != null)
            {
                Destroy(currentEquippedItem);
                currentEquippedItem = null;
            }
            currentSlot = -1;
        }
        else if (slotIndex < currentSlot)
        {
            currentSlot--;
        }
        
        UpdateUI();
    }

    System.Collections.IEnumerator ReenableCollision(Collider a, Collider b, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (a != null && b != null)
            Physics.IgnoreCollision(a, b, false);
    }

    // ==================== HELPER METHODS ====================
    public bool HasItem(string itemName)
    {
        foreach (ItemData item in inventoryItems)
            if (item.itemName == itemName) return true;
        return false;
    }

    public ItemData GetItemByName(string itemName)
    {
        foreach (ItemData item in inventoryItems)
            if (item.itemName == itemName) return item;
        return null;
    }

    public void RemoveItemByName(string itemName)
    {
        for (int i = 0; i < inventoryItems.Count; i++)
        {
            if (inventoryItems[i].itemName == itemName)
            {
                if (currentSlot == i) UnequipCurrent();
                else if (i < currentSlot) currentSlot--;
                inventoryItems.RemoveAt(i);
                UpdateUI();
                return;
            }
        }
    }

    public bool IsInventoryFull() => inventoryItems.Count >= maxSlots;

    public void ClearInventory()
    {
        UnequipCurrent();
        inventoryItems.Clear();
        UpdateUI();
    }

    // ==================== UPDATE UI ====================
    void UpdateUI()
    {
        if (slotImages == null || slotImages.Length == 0) return;
        
        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] == null) continue;
            
            if (i < inventoryItems.Count && inventoryItems[i] != null)
            {
                slotImages[i].sprite = inventoryItems[i].icon;
                slotImages[i].enabled = true;
                slotImages[i].color = (i == currentSlot) ? Color.yellow : Color.white;
            }
            else
            {
                slotImages[i].sprite = null;
                slotImages[i].enabled = false;
                slotImages[i].color = new Color(1, 1, 1, 0.2f);
            }
        }
    }

    // ==================== EQUIP/UNEQUIP ====================
    void ToggleEquipSlot(int slotIndex)
    {
        if (slotIndex >= inventoryItems.Count) return;

        if (currentSlot == slotIndex)
            UnequipCurrent();
        else
        {
            UnequipCurrent();
            EquipSlot(slotIndex);
        }
    }

    void EquipSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventoryItems.Count) return;

        ItemData itemData = inventoryItems[slotIndex];

        if (!itemData.isManual && itemData.itemPrefab != null && handPrefab != null)
        {
            currentEquippedItem = Instantiate(itemData.itemPrefab, handPrefab.transform);
            currentEquippedItem.transform.localPosition = Vector3.zero;
            currentEquippedItem.transform.localRotation = Quaternion.identity;
            
            Rigidbody rb = currentEquippedItem.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            
            Collider col = currentEquippedItem.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            
            Item itemScript = currentEquippedItem.GetComponent<Item>();
            if (itemScript != null) itemScript.enabled = false;
        }
        
        currentSlot = slotIndex;
        UpdateUI();
        
        if (itemData.isManual)
            Debug.Log("<color=cyan>[Inventory] Manual equipped! Press C to read or TAB anytime.</color>");
    }

    void UnequipCurrent()
    {
        if (currentEquippedItem != null)
        {
            Destroy(currentEquippedItem);
            currentEquippedItem = null;
        }
        currentSlot = -1;
        UpdateUI();
    }

    // ==================== MANUAL SYNC ====================
    void SyncManualCollected(int actorNumber)
    {
        Debug.Log($"<color=yellow>[Inventory] SyncManualCollected - actor {actorNumber}</color>");
        
        if (PhotonNetwork.IsConnected)
            photonView.RPC("RPC_ManualCollectedSync", RpcTarget.All, actorNumber);
        else
            OnManualCollectedLocal(actorNumber);
    }
    
    [PunRPC]
    void RPC_ManualCollectedSync(int collectorActorNumber)
    {
        Debug.Log($"<color=green>[Inventory] RPC_ManualCollectedSync! Collector: {collectorActorNumber}</color>");
        OnManualCollectedLocal(collectorActorNumber);
    }
    
    void OnManualCollectedLocal(int collectorActorNumber)
    {
        ManualItem.SetCollected(collectorActorNumber);
        
        Debug.Log($"<color=green>[Inventory] Manual synced! Holder: {collectorActorNumber}</color>");
        
        if (PhotonNetwork.IsConnected)
        {
            bool isLocalCollector = PhotonNetwork.LocalPlayer.ActorNumber == collectorActorNumber;
            
            if (isLocalCollector)
            {
                ManualUI manualUI = FindObjectOfType<ManualUI>(true);
                if (manualUI != null)
                {
                    manualUI.OnManualCollected();
                    Debug.Log("<color=lime>[Inventory] ManualUI enabled!</color>");
                }
            }
        }
        else
        {
            ManualUI manualUI = FindObjectOfType<ManualUI>(true);
            if (manualUI != null)
                manualUI.OnManualCollected();
        }
    }
    
    // ==================== DEBUG ====================
    public void PrintInventory()
    {
        Debug.Log("=== INVENTORY ===");
        for (int i = 0; i < inventoryItems.Count; i++)
        {
            string equipped = (i == currentSlot) ? " [EQUIPPED]" : "";
            string type = inventoryItems[i].isManual ? " [MANUAL]" : 
                         (inventoryItems[i].isConsumable ? " [CONSUMABLE]" : "");
            Debug.Log($"Slot {i + 1}: {inventoryItems[i].itemName}{type}{equipped}");
        }
    }
}