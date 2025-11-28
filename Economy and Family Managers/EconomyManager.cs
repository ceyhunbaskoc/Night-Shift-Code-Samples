using System;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager instance;
    [Header("Main Values")]
    [SerializeField] private int money=50;
    private int _previousMoney;
    
    [SerializeField] private int reputation=100;
    [SerializeField] private int organizationReputation=0;

    [Header("Package Bonuses")] 
    private int packageMoneyBonus;
    private int packageReputationBonus;

    public int Money
    {
        get => money;
        private set
        {
            money = value;
        }
    }
    public int Reputation
    {
        get => reputation;
        private set
        {
            reputation = value;
        }
    }
    public int OrganizationReputation
    {
        get => organizationReputation;
        private set
        {
            organizationReputation = value;
        }
    }
    public int PreviousMoney => _previousMoney;

    public event Action<int,int> OnTransactionFinished;


    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Start()
    {
        _previousMoney = Money;
    }

    public void ChangeMoney(int amount)
    {
        Money += amount;
    }

    public void ChangeReputation(int amount)
    {
        Reputation += amount;
    }

    public void ForceQuitResults(CustomerEncounter encounter)
    {
        int moneyChange = encounter.forceQuitResults.x;
        int reputationChange = encounter.forceQuitResults.y;
        
        ChangeMoney(moneyChange);
        ChangeReputation(reputationChange);

        OnTransactionFinished?.Invoke(moneyChange, reputationChange);
    }
    
    public void QuitResults(CustomerEncounter encounter)
    {
        int moneyChange = encounter.quitResults.x;
        int reputationChange = encounter.quitResults.y;
        
        ChangeMoney(moneyChange);
        ChangeReputation(reputationChange);

        OnTransactionFinished?.Invoke(moneyChange+packageMoneyBonus, reputationChange+packageReputationBonus);
    }
    
    public void ForceFinishResults(CustomerEncounter encounter)
    {
        Money=_previousMoney;

        OnTransactionFinished?.Invoke(0,0);
    }

    public void PackageResults(CustomerEncounter encounter)
    {
        packageMoneyBonus= encounter.packageResults.x;
        packageReputationBonus = encounter.packageResults.y;
        
    }
    public void ChangeOrganizationReputation(int amount=0)
    {
        OrganizationReputation += amount;
    }

    public void ResetPackageBonuses()
    {
        packageMoneyBonus = 0;
        packageReputationBonus = 0;
    }

    public void UpdatePreviousMoney()
    {
        _previousMoney = Money;
    }

    #region Save/Load Support Functions

    public void LoadEconomy(int savedMoney, int savedReputation, int savedOrgReputation)
    {
        money = savedMoney;
        reputation = savedReputation;
        organizationReputation = savedOrgReputation;
        UpdatePreviousMoney();
    }

    #endregion
}
