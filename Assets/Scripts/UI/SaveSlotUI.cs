using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 存档槽位选择界面 - 显示3个存档位，支持新建/继续/删除
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject saveSlotPanel;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Button backButton;

    [Header("删除确认")]
    [SerializeField] private GameObject deleteConfirmPanel;
    [SerializeField] private TextMeshProUGUI deleteConfirmText;
    [SerializeField] private Button deleteYesButton;
    [SerializeField] private Button deleteNoButton;

    private List<SaveSlotItem> slotItems = new List<SaveSlotItem>();
    private int pendingDeleteSlot = -1;

    public event System.Action<int> OnSlotSelected; // 选中存档槽位

    void Start()
    {
        backButton?.onClick.AddListener(Hide);
        deleteYesButton?.onClick.AddListener(ConfirmDelete);
        deleteNoButton?.onClick.AddListener(() => deleteConfirmPanel?.SetActive(false));

        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);
    }

    /// <summary>
    /// 显示存档选择界面
    /// </summary>
    public void Show()
    {
        if (saveSlotPanel != null)
            saveSlotPanel.SetActive(true);

        RefreshSlots();
    }

    /// <summary>
    /// 隐藏存档选择界面
    /// </summary>
    public void Hide()
    {
        if (saveSlotPanel != null)
            saveSlotPanel.SetActive(false);
    }

    /// <summary>
    /// 刷新存档槽位显示
    /// </summary>
    public void RefreshSlots()
    {
        // 清除旧的
        foreach (var item in slotItems)
        {
            if (item.gameObject != null)
                Destroy(item.gameObject);
        }
        slotItems.Clear();

        if (SaveSystem.Instance == null || slotPrefab == null || slotContainer == null) return;

        var infos = SaveSystem.Instance.GetAllSlotInfos();
        int activeSlot = SaveSystem.Instance.ActiveSlot;

        foreach (var info in infos)
        {
            var slotObj = Instantiate(slotPrefab, slotContainer);
            var item = slotObj.GetComponent<SaveSlotItem>();

            if (item == null)
            {
                // 手动绑定如果没有组件
                item = slotObj.AddComponent<SaveSlotItem>();
            }

            item.Setup(info, info.slotIndex == activeSlot);
            item.OnPlayClicked += () => SelectSlot(info.slotIndex);
            item.OnDeleteClicked += () => RequestDelete(info.slotIndex);

            slotItems.Add(item);
        }
    }

    private void SelectSlot(int slot)
    {
        if (SaveSystem.Instance == null) return;

        SaveSystem.Instance.SetActiveSlot(slot);
        OnSlotSelected?.Invoke(slot);
        Hide();
    }

    private void RequestDelete(int slot)
    {
        pendingDeleteSlot = slot;

        if (deleteConfirmPanel != null)
        {
            deleteConfirmPanel.SetActive(true);

            string slotName = $"存档 {slot + 1}";
            if (LocalizationSystem.Instance != null)
                deleteConfirmText.text = LocalizationSystem.Instance.GetFormat("confirm_delete", slotName);
            else if (deleteConfirmText != null)
                deleteConfirmText.text = $"确定要删除 {slotName} 吗？\n此操作不可撤销。";
        }
    }

    private void ConfirmDelete()
    {
        if (pendingDeleteSlot >= 0 && SaveSystem.Instance != null)
        {
            SaveSystem.Instance.DeleteSlot(pendingDeleteSlot);
            pendingDeleteSlot = -1;
        }

        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);

        RefreshSlots();
    }
}
