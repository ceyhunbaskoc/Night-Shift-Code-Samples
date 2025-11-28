using System;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.UI; // <-- Toggle (Checkbox) için bu gerekli
using System.Linq;

//FamilyMemberUI class to hold UI references for each family member
[System.Serializable]
public class FamilyMemberUI
{
    public TextMeshProUGUI nameText;
    public GameObject UIPanel; 
    
    [Header("Status Icons (Stack)")]
    public GameObject statusGood;
    public GameObject statusHungry;
    public GameObject statusVeryHungry;
    public GameObject statusSick;
    public GameObject statusVerySick;
    public GameObject statusCold;
    public GameObject statusDead;
}

// --- UIEndOfDay Sınıfı Kaldırıldı ---

/// <summary>
/// Gün sonu panelinin tüm fonksiyonelliğini yönetir.
/// FamilyManager ve EconomyManager ile konuşur.
/// </summary>
public class UIDayEnd : MonoBehaviour
{
    public static UIDayEnd instance;
    
    [Header("Panel References")]
    public GameObject endOfDayPanel;
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI reportText;
    private TextMeshProUGUI previewMoney;
    
    [Header("Family UI")]
    public List<FamilyMemberUI> familyMembersUI;

    [Header("Costs UI")]
    [SerializeField] private GameObject costsPanel;
    [SerializeField] private PoolObjectType earnTemplate;
    [SerializeField] private PoolObjectType lossTemplate;
    [SerializeField] private PoolObjectType lineTemplate;
    [SerializeField] private PoolObjectType previewMoneyTemplate;
    
    Dictionary<GameObject,CostTemplate> costsDict = new Dictionary<GameObject, CostTemplate>();
    
    public Button finalizeDayButton;

    private FamilyManager familyManager;
    private EconomyManager economyManager;
    private PooledObjectManager pooledObjectManager;
    private List<Toggle> activeToggles = new List<Toggle>();
    private List<GameObject> activeCostPrefabs = new List<GameObject>();

    private CostTemplate _foodTemplate;
    private CostTemplate _medicineTemplate;
    private CostTemplate _heatTemplate;
    private CostTemplate _radioTemplate;
    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Start()
    {
        familyManager = FamilyManager.instance;
        economyManager = EconomyManager.instance;
        pooledObjectManager = PooledObjectManager.Instance;
        familyManager.OnPreviewMoneyChanged += UpdatePreviewMoney;
        finalizeDayButton.onClick.AddListener(familyManager.FinalizeDayEnd);
    }

    private void OnDisable()
    {
        familyManager.OnPreviewMoneyChanged -= UpdatePreviewMoney;
        finalizeDayButton.onClick.RemoveListener(familyManager.FinalizeDayEnd);
    }

    /// <summary>
    /// FamilyManager'dan her para güncellemesi geldiğinde çalışır.
    /// </summary>
    public void UpdatePreviewMoney(int money)
    {
        previewMoney.text = $"{money}$";
        
        // check toggles state on each money change
        CheckAffordability(money);
    }

    /// <summary>
    /// Kalan paraya göre hangi butonların tıklanabilir olacağını belirler.
    /// </summary>
    private void CheckAffordability(int currentPreviewMoney)
    {
        // check eaach cost toggle
        UpdateSingleToggleState(_foodTemplate, familyManager.GetFoodCost(), currentPreviewMoney);
        UpdateSingleToggleState(_medicineTemplate, familyManager.GetMedicineCost(), currentPreviewMoney);
        UpdateSingleToggleState(_heatTemplate, familyManager.GetHeatingCost(), currentPreviewMoney);
        
        // check radio upgrade if applicable
        if (_radioTemplate != null && _radioTemplate.gameObject.activeSelf)
        {
            UpdateSingleToggleState(_radioTemplate, familyManager.GetRadioUpgradeCost(), currentPreviewMoney);
        }
    }
    /// <summary>
    /// Tek bir toggle için para yetiyor mu kontrolü yapar.
    /// </summary>
    private void UpdateSingleToggleState(CostTemplate template, int cost, int currentMoney)
    {
        if (template == null) return;

        // if already selected, always interactable
        if (template.Toggle.isOn)
        {
            template.Toggle.interactable = true;
        }
        // if not selected, check affordability
        else
        {
            // if current money is greater than or equal to cost, enable the toggle
            template.Toggle.interactable = (currentMoney >= cost);
        }
    }

    public void ShowPanel(int currentDay)
    {
        ClearCostList();
        
        dayText.text = $"END OF DAY {currentDay + 1}";
        CreatePreviewMoneyText();
        
        DailyEarnText();
        SetInitialReport(currentDay); // prepare report text
        
        DailyRentText();
        
        // Referansları saklayarak oluşturma
        _foodTemplate = FamilyFoodCost();
        _medicineTemplate = FamilyMedicineCost();
        _heatTemplate = FamilyHeatCost();
        
        // Radyo upgrade kontrolü
        if (RadioManager.instance != null && (GameManager.instance.CurrentDay + 1) % 5 == 0 && !RadioManager.instance.CheckMaxLevel())
        {
            _radioTemplate = CreateRadioUpgradeText();
        }
        else
        {
            _radioTemplate = null;
        }

        CreateLine();
        
        UpdateFamilyMemberUI(familyManager.FamilyMembers);
        previewMoney.gameObject.transform.SetAsLastSibling();
        // İlk para durumu kontrolü
        familyManager.ForceRecalculate();

        endOfDayPanel.SetActive(true);
    }

    public CostTemplate DailyText(PoolObjectType template, bool isToogleActive)
    {
        GameObject costText = pooledObjectManager.Spawn(template, Vector3.zero, Quaternion.identity);
        costText.transform.SetParent(costsPanel.transform, false);
        
        activeCostPrefabs.Add(costText);
        if (!costsDict.ContainsKey(costText))
        {
            CostTemplate costTemplate = costText.GetComponent<CostTemplate>();
            costsDict.Add(costText, costTemplate);
        }
        CostTemplate cost = costsDict[costText];
        cost.Toggle.gameObject.SetActive(isToogleActive);
        
        cost.Toggle.isOn = false; 
        cost.Toggle.interactable = true; // start open by default
        cost.Toggle.onValueChanged.RemoveAllListeners(); // remove old listeners

        return cost;
    }

    public void DailyEarnText()
    {
        CostTemplate cost = DailyText(earnTemplate, false);
        cost.DescriptionText.text = "TODAY'S EARNINGS";
        int change = economyManager.Money - economyManager.PreviousMoney;
        
        // Renk ayarları...
        if (change >= 0)
        {
            cost.DescriptionText.color = Color.white;
            cost.CostText.color = Color.white;
            cost.CostText.text = $"+{change}$";
        }
        else
        {
            cost.DescriptionText.color = Color.red;
            cost.CostText.color = Color.red;
            cost.CostText.text = $"{change}$";
        }
    }

    public void DailyRentText()
    {
        CostTemplate cost = DailyText(lossTemplate, false);
        cost.DescriptionText.text = "RENT";
        cost.CostText.text = $"{-familyManager.GetRentCost()}$";
    }
    
    public CostTemplate FamilyFoodCost()
    {
        CostTemplate cost = DailyText(lossTemplate, true);
        cost.DescriptionText.text = "FOOD";
        cost.CostText.text = $"{-familyManager.GetFoodCost()}$";
        cost.Toggle.onValueChanged.AddListener(familyManager.ToggleFood);
        return cost;
    }

    public CostTemplate FamilyMedicineCost()
    {
        List<FamilyMember> sicks = new List<FamilyMember>();
        foreach(var m in familyManager.FamilyMembers)
        {
            if(m.isAlive && (m.IsSick() || m.IsVerySick())) sicks.Add(m);
        }

        if (sicks.Count > 0)
        {
            CostTemplate cost = DailyText(lossTemplate, true);
            var sickNames = sicks.Select(data => data.name);
            string joinedNames = string.Join(", ", sickNames);

            cost.DescriptionText.text = $"Buy Drugs ({joinedNames})";
            cost.CostText.text = $"{-familyManager.GetMedicineCost()}$";
            cost.Toggle.onValueChanged.AddListener(familyManager.ToggleMedicine);
            return cost;
        }
        return null;
    }

    public CostTemplate FamilyHeatCost()
    {
        CostTemplate cost = DailyText(lossTemplate, true);
        cost.DescriptionText.text = "HEAT";
        cost.CostText.text = $"{-familyManager.GetHeatingCost()}$";
        cost.Toggle.onValueChanged.AddListener(familyManager.ToggleHeating);
        return cost;
    }

    public CostTemplate CreateRadioUpgradeText()
    {
        CostTemplate cost = DailyText(lossTemplate, true); // toggle aktif olacak
        cost.DescriptionText.text = "RADIO UPGRADE";
        cost.CostText.text = $"{-familyManager.GetRadioUpgradeCost()}$";
        cost.Toggle.onValueChanged.AddListener(familyManager.ToggleRadioUpgrade);
        return cost;
    }

    void CreateLine()
    {
        GameObject line = pooledObjectManager.Spawn(lineTemplate, Vector3.zero, Quaternion.identity);
        line.transform.SetParent(costsPanel.transform, false);
        activeCostPrefabs.Add(line);
    }

    void CreatePreviewMoneyText()
    {
        GameObject previewMoneyTextGO = pooledObjectManager.Spawn(previewMoneyTemplate, Vector3.zero, Quaternion.identity);
        previewMoneyTextGO.transform.SetParent(costsPanel.transform, false);
        previewMoney = previewMoneyTextGO.GetComponent<TextMeshProUGUI>();
        activeCostPrefabs.Add(previewMoneyTextGO);
    }

    /// <summary>
    /// FamilyManager'daki verilere bakarak UI'daki aile listesini ve durum yığınlarını günceller.
    /// </summary>
    public void UpdateFamilyMemberUI(List<FamilyMember> members)
    {
        for (int i = 0; i < familyMembersUI.Count; i++)
        {
            if (i < members.Count)
            {
                FamilyMember data = members[i];
                FamilyMemberUI ui = familyMembersUI[i];
                
                ui.UIPanel.SetActive(true);
                ui.nameText.text = data.name;
                
                //first close all status icons
                ui.statusGood.SetActive(false);
                ui.statusHungry.SetActive(false);
                ui.statusVeryHungry.SetActive(false);
                ui.statusSick.SetActive(false);
                ui.statusVerySick.SetActive(false);
                ui.statusCold.SetActive(false);
                ui.statusDead.SetActive(false);
                
                // enable only relevant status icons
                if (data.IsDead())
                {
                    ui.statusDead.SetActive(true);
                }
                else
                {
                    if (data.IsGood()) ui.statusGood.SetActive(true); 
                    
                    if (data.IsHungry()) ui.statusHungry.SetActive(true);
                    if (data.IsVeryHungry()) ui.statusVeryHungry.SetActive(true);
                    if (data.IsSick()) ui.statusSick.SetActive(true);
                    if (data.IsVerySick()) ui.statusVerySick.SetActive(true);
                    if (data.IsCold()) ui.statusCold.SetActive(true);
                }
            }
            else
            {
                familyMembersUI[i].UIPanel.SetActive(false);
            }
        }
    }
    
    private void SetInitialReport(int currentDay)
    {
        // say FamilyManager to process day end and get report
        string autoReport = familyManager.ProcessAutomaticDayEnd(currentDay);
        
        // print reports to UI
        reportText.text = autoReport; 
    }

    /// <summary>
    /// kapatma butonuna atanacak olan fonksiyon
    /// </summary>
    public void CloseEndOfDayPanel()
    {
        StartCoroutine(UIHelperFunctions.instance.FadeCoroutine(() =>
        {
            ResetToggleEvents();
            GameManager.instance.StartNewDay();
            FamilyManager.instance.TriggerOnDayEndProcessed();
            endOfDayPanel.SetActive(false);
        }));
    }

    public void ResetToggleEvents()
    {
        foreach (var toggle in activeToggles)
        {
            toggle.onValueChanged.RemoveAllListeners();
        }
        
        activeToggles.Clear();
    }
    
    /// <summary>
    /// Spawn edilen tüm cost prefablarını havuza geri döndürür.
    /// </summary>
    private void ClearCostList()
    {
        foreach (var costGO in activeCostPrefabs)
        {
            pooledObjectManager.ReturnToPool(costGO);
        }
        activeCostPrefabs.Clear();
    }
}