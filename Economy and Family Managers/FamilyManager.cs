using UnityEngine;
using System.Collections.Generic;
using System.Text; // Metin birleştirme için

// family member data class
[System.Serializable]
public class FamilyMember
{
    public string name;
    public bool isAlive = true;

    [Tooltip("Kaç gündür aç olduğu. 0 = Tok, 1+ = Aç")]
    public int daysHungry = 0;
    [Tooltip("Kaç gündür hasta olduğu. 0 = Sağlıklı, 1+ = Hasta")]
    public int daysSick = 0;
    [Tooltip("Şu anda 'Üşüyor' durumunda mı?")]
    public bool isCold = false;

    // limits
    public const int SICKNESS_LIMIT = 3;
    public const int HUNGER_LIMIT = 4;
    

    public bool IsDead() => !isAlive;
    public bool IsGood() => isAlive && daysHungry == 0 && daysSick == 0 && !isCold;
    
    public bool IsHungry() => daysHungry > 0 && daysHungry < (HUNGER_LIMIT - 1);
    public bool IsVeryHungry() => daysHungry >= (HUNGER_LIMIT - 1);
    
    public bool IsSick() => daysSick > 0 && daysSick < (SICKNESS_LIMIT - 1);
    public bool IsVerySick() => daysSick >= (SICKNESS_LIMIT - 1);
    
    public bool IsCold() => isCold;
}

/// <summary>
/// Gün sonu ailesel ve ekonomik olayları (Kira, Yemek, İlaç) yönetir.
/// EconomyManager'dan para çeker ve GameManager'a Game Over sinyali yollar.
/// </summary>
public class FamilyManager : MonoBehaviour
{
    public static FamilyManager instance;

    [Header("Family Members")]
    [SerializeField] private List<FamilyMember> familyMembers = new List<FamilyMember>();

    [Header("Daily Costs")]
    [SerializeField] private int rentCost = 20;
    [SerializeField] private int foodCostPerPerson = 10;
    [SerializeField] private int medicineCostPerPerson = 25;
    [SerializeField] private int heatingCost = 20;

    [Header("Economy Status")]
    [SerializeField] private int rentGracePeriod = 2;
    
    [Header("Event Settings")]
    [Tooltip("Normal hastalanma şansı (örn: 0.1 = %10)")]
    [SerializeField] private float sicknessChanceNormal = 0.1f;
    [Tooltip("Eğer 'Üşüyor' ise hastalanma şansı (örn: 0.4 = %40)")]
    [SerializeField] private float sicknessChanceIfCold = 0.4f;

    // --- EVENTS ---
    public event System.Action<int, int, int> OnCostsCalculated; 
    public event System.Action OnDayEndProcessed;
    public event System.Action<int> OnPreviewMoneyChanged; 

    private EconomyManager economyManager;
    private RadioManager radioManager;
    
    private bool _willBuyFood = false;
    private bool _willBuyMedicine = false;
    private bool _willBuyHeating = false;
    private bool _willBuyRadioUpgrade = false;
    
    public List<FamilyMember> FamilyMembers => familyMembers;
    public int RentGracePeriod => rentGracePeriod;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        economyManager = EconomyManager.instance; 
        radioManager = RadioManager.instance;
    }

    // force recalculate from outside
    public void ForceRecalculate()
    {
        RecalculatePreviewMoney();
    }
    /// <summary>
    /// GameManager, gün Sonu panelini açtığında bu fonksiyonu çağırır.
    /// Kira öder ve o anki aile durumunu raporlar.
    /// </summary>
    public string ProcessAutomaticDayEnd(int currentDay)
    {
        StringBuilder report = new StringBuilder();
        
        // disable all toggles for new day
        _willBuyFood = false;
        _willBuyMedicine = false;
        _willBuyHeating = false;
        RecalculatePreviewMoney();

        if (GameManager.instance.IsForcedFinish)
        {
            report.AppendLine($"<color=red>Müşteri seni bayılttı ve bugünkü tüm paranı çaldı.</color>");
        }
        // rent payment (Automatic)
        if (economyManager.Money >= rentCost)
        {
            economyManager.ChangeMoney(-rentCost);
            report.AppendLine($"<color=green>Kira ödendi (-{rentCost}$)</color>");
            rentGracePeriod = 2;
            RecalculatePreviewMoney(); 
        }
        else
        {
            rentGracePeriod--;
            report.AppendLine($"<color=red>KİRA ÖDENEMEDİ! (Para Yetersiz)</color>");
            if (rentGracePeriod < 0)
            {
                report.AppendLine("<color=red>EV SAHİBİ SİZİ EVDEN ATTI!</color>");
                TriggerGameOver("Kira ödenmedi.");
            }
            else
            {
                report.AppendLine($"Kalan kira müddeti: {rentGracePeriod} gün");
            }
        }
        
        // report family status
        report.AppendLine("\n--- AİLE DURUMU ---"); // Ayraç
        bool everyoneIsGood = true;
        foreach (var member in familyMembers)
        {
            if (!member.isAlive)
            {
                report.AppendLine($"<color=red>{member.name}: Öldü</color>");
                everyoneIsGood = false;
                continue;
            }

            // create status with Is functions
            List<string> statuses = new List<string>();
            if (member.IsVeryHungry()) statuses.Add("Çok Aç");
            else if (member.IsHungry()) statuses.Add("Aç");

            if (member.IsVerySick()) statuses.Add("Çok Hasta");
            else if (member.IsSick()) statuses.Add("Hasta");

            if (member.IsCold()) statuses.Add("Üşüyor");
            
            // Eğer bir durumu varsa, raporla
            if (statuses.Count > 0)
            {
                report.AppendLine($"{member.name}: {string.Join(", ", statuses)}");
                everyoneIsGood = false;
            }
        }
        
        if(everyoneIsGood)
        {
            report.AppendLine("<color=green>Ailedeki herkesin durumu iyi.</color>");
        }

        // calculate costs and invoke event
        OnCostsCalculated?.Invoke(GetFoodCost(), GetMedicineCost(), GetHeatingCost());

        return report.ToString(); // Raporu UIDayEnd'e yolla
    }

    #region Toggle Functions
    // Toggle functions called by UI checkboxes

    public void ToggleFood(bool isTicked)
    {
        _willBuyFood = isTicked;
        RecalculatePreviewMoney(); // Önizlemeyi güncelle
    }

    public void ToggleMedicine(bool isTicked)
    {
        _willBuyMedicine = isTicked;
        RecalculatePreviewMoney(); // Önizlemeyi güncelle
    }
    
    public void ToggleHeating(bool isTicked)
    {
        _willBuyHeating = isTicked;
        RecalculatePreviewMoney(); // Önizlemeyi güncelle
    }
    
    public void ToggleRadioUpgrade(bool isTicked)
    {
        _willBuyRadioUpgrade = isTicked;
        RecalculatePreviewMoney(); // Önizlemeyi güncelle
    }
    #endregion
    /// <summary>
    /// Checkbox'lara göre kenarda kalan parayı hesaplar ve UI'a sinyal yollar.
    /// </summary>
    private void RecalculatePreviewMoney()
    {
        int previewMoney = economyManager.Money;
        
        if (_willBuyFood) previewMoney -= GetFoodCost();
        if (_willBuyMedicine) previewMoney -= GetMedicineCost();
        if (_willBuyHeating) previewMoney -= GetHeatingCost();
        
        // send preview money to UI
        OnPreviewMoneyChanged?.Invoke(previewMoney);
    }

    /// <summary>
    /// Gün sonu panelindeki sleep butonuna bağlanır.
    /// Seçili checkbox'ları uygular, günü ilerletir ve paneli kapatır.
    /// </summary>
    public void FinalizeDayEnd()
    {
        // calculate total cost
        int totalCost = 0;
        if (_willBuyFood) totalCost += GetFoodCost();
        if (_willBuyMedicine) totalCost += GetMedicineCost();
        if (_willBuyHeating) totalCost += GetHeatingCost();
        if(_willBuyRadioUpgrade) totalCost += GetRadioUpgradeCost();

        // check if enough money
        if (economyManager.Money < totalCost)
        {
            Debug.LogError("Para yetersiz! (FinalizeDayEnd)");
            return;
        }
        
        // decrease money
        economyManager.ChangeMoney(-totalCost);

        // apply effects to family members
        int aliveCount = 0;
        foreach (var member in familyMembers)
        {
            if (!member.isAlive) continue;

            // ISINMA
            member.isCold = !_willBuyHeating;

            // AÇLIK
            if (!_willBuyFood)
                member.daysHungry++;
            else
                member.daysHungry = 0; // Doydu
                
            if (member.daysHungry > FamilyMember.HUNGER_LIMIT)
            {
                member.isAlive = false;
                continue; 
            }
            
            // sickness
            if (!_willBuyMedicine)
            {
                if (member.daysSick > 0) member.daysSick++; // Hastalık ilerledi
            }
            else
            {
                member.daysSick = 0; // İyileşti
            }
            
            if (member.daysSick > FamilyMember.SICKNESS_LIMIT)
            {
                member.isAlive = false;
                continue;
            }

            aliveCount++;
        }

        if (aliveCount == 0)
        {
            TriggerGameOver("Tüm aile öldü.");
            return;
        }

        if (_willBuyRadioUpgrade)
        {
            radioManager.UpgradeRadio();
        }
        
        // prepare for new day
        StartNewDay();
        
        // send close end of day panel signal
        UIDayEnd.instance.CloseEndOfDayPanel();
    }

    public void TriggerOnDayEndProcessed()
    {
        OnDayEndProcessed?.Invoke();
    }
    /// <summary>
    /// Yeni günün durumlarını (yeni açlık, yeni hastalık) hazırlar.
    /// </summary>
    public void StartNewDay()
    {
        
        foreach (var member in familyMembers)
        {
            if (!member.isAlive) continue; // Ölüler hastalanmaz

            // hungry status is handled in FinalizeDayEnd
            
            if(member.daysSick == 0) // Sadece sağlıklıysa
            {
                float chance = member.isCold ? sicknessChanceIfCold : sicknessChanceNormal;
                if (Random.value < chance)
                {
                    member.daysSick = 1;
                }
            }
        }
    }

    private void TriggerGameOver(string reason)
    {
        Debug.LogError($"GAME OVER: {reason}");
    }
    
    
    public int GetFoodCost()
    {
        int aliveMembers = 0;
        foreach (var member in familyMembers)
        {
            if (member.isAlive) aliveMembers++;
        }
        return foodCostPerPerson * aliveMembers;
    }

    public int GetMedicineCost()
    {
        int totalCost = 0;
        List<FamilyMember> sicks = new List<FamilyMember>();
        for (int i = 0; i < FamilyMembers.Count; i++)
        {
            FamilyMember data = FamilyMembers[i];
            if (data.isAlive)
            {
                if (data.IsSick())
                {
                    totalCost+=medicineCostPerPerson;
                    sicks.Add(data);
                }
                else if (data.IsVerySick())
                {
                    totalCost+=medicineCostPerPerson*2;
                    sicks.Add(data);
                }
            }
        }
        return  totalCost;
    }
    
    public int GetHeatingCost()
    {
        return heatingCost;
    }
    public int GetRentCost()
    {
        return rentCost;
    }
    
    public int GetRadioUpgradeCost()
    {
        return radioManager.NextUpgradeCost;
    }
    
    //for the UI to access family members
    public List<FamilyMember> GetFamilyMembers()
    {
        return familyMembers;
    }
    public bool IsFamilyDead()
    {
        foreach (var member in familyMembers)
        {
            if (member.isAlive) return false;
        }
        return true;
    }

    #region Save/Load Support Functions

    public void LoadFamilyData(List<FamilyMember> savedMembers, int savedGracePeriod)
    {
        familyMembers = savedMembers;
        rentGracePeriod = savedGracePeriod;
    }

    #endregion
}