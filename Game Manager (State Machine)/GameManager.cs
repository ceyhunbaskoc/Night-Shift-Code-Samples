using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    
    [Header("Game Data")]
    [SerializeField] private int currentDay = 0;
    [SerializeField] private int customerIndex = 0;

    [Header("Ignition Settings")] 
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private GameObject ignitionObject; // Kontak objesi
    [SerializeField] private Material ignitionHover;   // Üzerine gelinceki materyal
    [SerializeField] private float ignitionTurnDuration;   // Üzerine gelinceki materyal
    [SerializeField] private LayerMask interactableLayer; // Sadece bu katmanla etkileşim kur
    
    [Header("Passenger Settings")]
    [Tooltip("Müşterinin inmesi/binmesi için beklenecek (animasyon/ses) süresi")]
    [SerializeField] private float pGetInOutDuration;
    [Tooltip("Müşterinin uzakta spawn olacağı nokta (yolla birlikte hareket eder)")]
    [SerializeField] private Transform pRoadSpawnPosition;
    [Tooltip("Müşterinin indikten sonra ışınlanacağı nokta (yolla birlikte hareket eder)")]
    [SerializeField] private Transform pGetOutPosition;
    [Tooltip("Müşterinin arabaya bindiğinde ışınlanacağı nokta (taksiye göre sabit)")]
    [SerializeField] private Transform pTaxiPosition;
    [Tooltip("Müşteri indikten kaç sn sonra prefab'ın yok edileceği")]
    [SerializeField] private float pDestroyDuration;
    
    // Global referanslar
    private CustomerEncounter _currentCustomerEncounter;
    private GameObject _currentActivePrefab;
    private CustomerMover _customerMover;
    
    [Header("Raycast Settings")]
    [SerializeField] private float raycastDistance = 5f; // Ne kadar uzağı kontrol etsin

    // İç durum değişkenleri
    private bool _dayStarted = false;
    private bool _isHovering = false;     // Şu an 'hover' durumunda mıyız? (Optimizasyon için)
    private bool _isFull = false;
    private bool _isForcedGetOut = false; // Oyuncu [D] tuşuna basarsa 'true' olur
    private bool _isForcedFinish = false;

    private bool isPlayerMurdered = false;
    
    // Cache'lenmiş component'ler
    private Material _originalMaterial;   
    private Renderer _ignitionRenderer; 
    private Camera _mainCamera;
    
    public int CurrentDay => currentDay;

    public int CustomerIndex => customerIndex;
    public CustomerEncounter CurrentCustomerEncounter => _currentCustomerEncounter;

    public List<PackageType> currentPackageTypes = new List<PackageType>();
    public bool IsFull => _isFull;
    public bool IsForcedFinish => _isForcedFinish;

    public bool IsPlayerMurdered => isPlayerMurdered;
    public enum State
    {
        Newspaper,
        Ignition,
        Started,
        Driving,
        GetIn,
        GetOut,
        Finished,
        GameOver
    }
    [SerializeField] private State _currentState = State.Newspaper;
    public State CurrentState
    {
        get => _currentState;
        set
        {
            if (_currentState == value) return; // aynı state ise atla
            State oldState = _currentState;
            _currentState = value;
            OnStateChanged?.Invoke(oldState, _currentState); // event tetiklenir
        }
    }
    public event Action<State, State> OnStateChanged;
    
    // Manager Referansları
    RoadSpawner _roadSpawner;
    NodeParser _nodeParser;
    EconomyManager _economyManager;

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    void Start()
    {
        // Manager'ları cache'le
        _roadSpawner = RoadSpawner.instance;
        _nodeParser = NodeParser.instance;
        _economyManager = EconomyManager.instance;
        
        OnStateChanged += HandleStateChanged; // State Machine'i bağla
        _mainCamera = Camera.main;
        
        // Kontak objesinin materyalini cache'le
        if (ignitionObject != null)
        {
            _ignitionRenderer = ignitionObject.GetComponent<Renderer>();
            if (_ignitionRenderer != null)
            {
                _originalMaterial = _ignitionRenderer.material;
            }
            else
            {
                Debug.LogError("ignitionObject'te Renderer component'i bulunamadı!");
            }
        }
    }

    private void OnDisable()
    {
        OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }
        
        // --- Input Yönetimi (State'e göre) ---
        
        // Sadece 'Ignition' state'indeyken kontağı kontrol et
        if (CurrentState == State.Ignition)
        {
            CreateRayCast();
            
            if (_isHovering && Input.GetMouseButtonDown(0))
            {
                StartCoroutine(TurnIgnitionOn(90f));
            }
        }

        // Sadece müşteri varken ('_isFull') ve sürüş/diyalog halindeyken ('Driving') çalışır
        if (CurrentState == State.Driving && _isFull && Input.GetKeyDown(KeyCode.D))
        {
            Debug.Log("Oyuncu [D] tuşuna bastı. Müşteri ZORLA atılıyor.");
            _isForcedGetOut = true; // Bunu "zorla atılma" olarak işaretle
            CurrentState = State.GetOut; // GetOut state'ini tetikle
        }
    }
    public void StartFreshGame()
    {
        // 1. Sayaçları mutlak sıfıra çek
        currentDay = 0; 
        customerIndex = 0;
    
        // 2. Paketleri ve bonusları temizle
        currentPackageTypes.Clear();
        EconomyManager.instance.ResetPackageBonuses(); // Varsa reset fonksiyonun

        // 3. Dünya ve Arananlar listesini SIFIRDAN oluştur
        _roadSpawner.NewDayReset();
        ResetIgnitionRotation();
        WantedManager.instance.UpdateWanted(); // Yeni oyun olduğu için rastgele oluşturulsun

        // 4. State'i ayarla
        CurrentState = State.Newspaper;

        // 5. Gazeteyi aç
        NewspaperUIManager.instance.UpdateNewspaperUI();
        NewspaperUIManager.instance.OpenNewspaperUI();

        // 6. İLK KAYDI AL (Böylece Slot'ta "Day 1" veya "Day 0" olarak görünür)
        SaveLoadManager.instance.SaveCurrentDay();
    }

    /// <summary>
    /// Attach to a CloseEndOfDayPanel function in UIDayEnd to start a new day.
    /// </summary>
    public void StartNewDay()
    {
         currentDay++;
         WantedManager.instance.UpdateWanted();
         _roadSpawner.NewDayReset();
         ResetIgnitionRotation();
         CurrentState = State.Newspaper;
         SaveLoadManager.instance.SaveCurrentDay();
    }
    
    public void StartLoadedDay()
    {
        _roadSpawner.NewDayReset();
        ResetIgnitionRotation();

        CurrentState = State.Newspaper;
    
        NewspaperUIManager.instance.UpdateNewspaperUI();
        NewspaperUIManager.instance.OpenNewspaperUI();

    }
    public void StartIgnitionPhase()
    {
        CurrentState = State.Ignition;
    }
    /// <summary>
    /// Oyunun ana döngüsünü yöneten State Machine'in kalbi.
    /// </summary>
    private void HandleStateChanged(State oldState, State newState)
    {
        Debug.Log($"State değişti: {oldState} -> {newState}");

        switch (newState)
        {
            case State.Newspaper:
                break;
            case State.Ignition:
                _economyManager.UpdatePreviousMoney();
                CinemachineController.instance.SetCameraActive(true);
                break;
            case State.Started:
                ShowDayInfoText();
                StartDay();
                break;
            case State.Driving:
                // Müşteriler arası "boş sürüş" süresi
                float nextcustomerDelay = Random.Range(13f, 20f);
                Debug.Log($"Next customer delay: {nextcustomerDelay}");

                if (oldState == State.Started)
                {
                    customerIndex = 0; // Günün ilk müşterisi
                    StartCoroutine(DriveToNextCustomer(nextcustomerDelay));
                }
                else if (oldState == State.GetIn)
                {
                    // Müşteri bindi, diyalog başladı. [D] tuşunu bekle.
                    Debug.Log("Müşteri arabada. Diyalog başladı. Sürüş (Diyalog) modu.");
                }
                else if (oldState == State.GetOut)
                {
                    // Müşteri indi, bir sonrakine geç.
                    _economyManager.ResetPackageBonuses();
                    customerIndex++;
                    StartCoroutine(DriveToNextCustomer(nextcustomerDelay));
                }
                break;
            case State.GetIn:
                StartCoroutine(PassengerGetIn());
                break;
            case State.GetOut:
                StartCoroutine(PassengerGetOutSequence());
                break;
            case State.Finished:
                if(!_isForcedFinish)
                    StartCoroutine(DayFinished());
                else
                    ForcedFinishDay();
                CinemachineController.instance.SetCameraActive(false);
                break;
            case State.GameOver:
                break;
        }
    }

    /// <summary>
    /// 'Ignition' state'indeyken kontağa bakıp bakmadığımızı kontrol eder.
    /// </summary>
    void CreateRayCast()
    {
        RaycastHit hit;
        Vector3 rayOrigin = _mainCamera.transform.position;
        Vector3 rayDirection = _mainCamera.transform.forward;
        bool didHit = Physics.Raycast(rayOrigin, rayDirection, out hit, raycastDistance, interactableLayer);

        if (didHit && hit.collider.gameObject == ignitionObject)
        {
            if (!_isHovering)
            {
                _ignitionRenderer.material = ignitionHover;
                _isHovering = true;
            }
        }
        else
        {
            if (_isHovering)
            {
                _ignitionRenderer.material = _originalMaterial;
                _isHovering = false;
            }
        }
    }

    /// <summary>
    /// Kontağı çevirme animasyonunu oynatır ve günü başlatır.
    /// </summary>
    IEnumerator TurnIgnitionOn(float targetValue)
    {
        _isHovering = false; // Input'u tekrar almasın
        Quaternion startRotation = ignitionObject.transform.rotation;
        Vector3 startAngles = startRotation.eulerAngles;
        Quaternion targetRotation = Quaternion.Euler(targetValue, startAngles.y, startAngles.z);
        float elapsedTime = 0f;
    
        while (elapsedTime < ignitionTurnDuration)
        {
            float t = elapsedTime / ignitionTurnDuration;
            ignitionObject.transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    
        ignitionObject.transform.rotation = targetRotation;
        _dayStarted = true;
        _ignitionRenderer.material = _originalMaterial;
        CurrentState = State.Started;
    }
    void ShowDayInfoText()
    {
        string dayInfo = $"Day {currentDay+1}";
        UIManager.instance.HandleDayInfoText(dayInfo);
    }
    /// <summary>
    /// Günü başlatır ve arabayı hızlandırır.
    /// </summary>
    void StartDay()
    {
        RoadSpawner.instance.IncreaseSpeed();
        CurrentState = State.Driving;
    }
    
    /// <summary>
    /// Bir sonraki müşteriye "sürer", müşterinin 3D modelini spawn eder
    /// ve bekleme süresi bitince 'GetIn' state'ini tetikler.
    /// </summary>
    IEnumerator DriveToNextCustomer(float driveDelay)
    {
        Debug.Log($"Bir sonraki müşteriye gidiliyor... ({driveDelay} saniye)");
        
        // 1. Müşteri verisini al
        _currentCustomerEncounter = DayProgression.instance.GetNextCustomer(currentDay, customerIndex);
        if (_currentCustomerEncounter == null)
        {
            // Müşteri yoksa, günü bitir
            Debug.Log("Bugünkü tüm müşteriler bitti. Gün sonu.");
            //Game over check
            
            EndsManager.instance.InvokeEndingCheck();
            

            if (!EndsManager.instance.IsGameOver)
            {
                
                CurrentState = State.Finished;
                
            }
            else
            
                CurrentState = State.GameOver;
            yield break; // Coroutine'i burada bitir.
        }

        // 2. Müşterinin görünmesi için bekle
        float pVisualSpawnTime = driveDelay / 2f;
        yield return new WaitForSeconds(pVisualSpawnTime);

        // 3. Müşteriyi uzakta spawn et
        SpawnCustomerVisual(driveDelay); 

        // 4. Kalan süreyi bekle
        yield return new WaitForSeconds(driveDelay - pVisualSpawnTime);
        
        // 5. Süre doldu, 'GetIn' state'ini tetikle
        CurrentState = State.GetIn;
    }

    /// <summary>
    /// Müşterinin 3D prefab'ını, tam doğru Z mesafesinde yaratır.
    /// </summary>
    void SpawnCustomerVisual(float driveDelay) // driveDelay, örn: 20 saniye
    {
        if (_currentCustomerEncounter == null || _currentCustomerEncounter.customerPoolType == PoolObjectType.None)
        {
            Debug.LogError("Spawn edilecek müşteri prefab'ı bulunamadı!");
            return;
        }
        
        if(_currentActivePrefab != null)
            Destroy(_currentActivePrefab);

        // --- HESAPLAMA BAŞLANGICI ---

        // Müşteri ne zaman spawn olacak? (Örn: 20sn / 2 = 10sn sonra)
        float pVisualSpawnTime = driveDelay / 2f;
        
        // Müşteri spawn olduktan sonra araba durana kadar ne kadar süre geçecek?
        float travelTime = (driveDelay - pVisualSpawnTime) + _roadSpawner.ChangeSpeedTime;

        // Müşteri bu 'travelTime' boyunca (yol ile kayarak) ne kadar mesafe kat edecek?
        float travelDistance = _roadSpawner.CurrentRoadSpeed * travelTime;

        // 4. Spawn Pozisyonunu Hesapla:
        //    Hedef (pTaxiPosition Z) + Otomatik Hesaplanan Uzaklık (travelDistance)
        Vector3 spawnPos = new Vector3(
            pRoadSpawnPosition.position.x,
            pRoadSpawnPosition.position.y,
            pRoadSpawnPosition.position.z + travelDistance 
        );
        
        // --- HESAPLAMA SONU ---

        _currentActivePrefab = PooledObjectManager.Instance.Spawn(
            _currentCustomerEncounter.customerPoolType,
            spawnPos,
            pRoadSpawnPosition.rotation
        );
        
        _customerMover = _currentActivePrefab.GetComponent<CustomerMover>();
    }
    
    /// <summary>
    /// Arabayı yavaşlatır, müşteriyi kapıya ışınlar ve diyaloğu başlatır.
    /// </summary>
    IEnumerator PassengerGetIn()
    {
        _roadSpawner.DecreaseSpeed();
        yield return new WaitForSeconds(_roadSpawner.ChangeSpeedTime); // Araba tamamen durana kadar bekle
        if (_customerMover != null)
        {
            // CustomerMover'a "Yürümeye başla" ve "Hedefin burası" komutunu ver
            _customerMover.StartWalkingToTarget(pGetOutPosition);
        }
        else
        {
            Debug.LogError("CustomerMover script'i bulunamadı! (Prefab spawn edilmemiş olabilir)");
        }
        yield return new WaitForSeconds(pGetInOutDuration);
        
        // Müşteriyi kapının dibine (pTaxiPosition) ışınla
        if (_currentActivePrefab != null)
        {
            _currentActivePrefab.transform.position = pTaxiPosition.position;
        }
        if (_customerMover != null)
        {
            _customerMover.IsIn = true; // Yolla birlikte kaymayı durdur
            _customerMover.IsWalk = false; // Yolla birlikte kaymayı durdur
        }
        
        _isFull = true;
        
        _roadSpawner.IncreaseSpeed();
        CurrentState = State.Driving;
        // 1. Diyalog başlat
        bool dialogueFinished = false;
        Action onComplete = () => { dialogueFinished = true; };
        _nodeParser.OnDialogueComplete += onComplete;
        
        _nodeParser.StartDialogue(_currentCustomerEncounter.dialogueGraph, DialogueStartType.Main);
        
        // Diyalog bitene kadar (veya zorla atılana kadar) burada bekle
        while (!dialogueFinished && !_isForcedGetOut)
        {
            yield return null;
        }
        
        // Event'ten çık
        _nodeParser.OnDialogueComplete -= onComplete;

        // Diyalog bitti, oyuncuya karar vermesi için ek süre tanınıyor
        float timer = 0f;
        float waitDuration = Random.Range(5f, 10f);

        // WaitForSeconds YERİNE bu döngüyü kullan:
        while (timer < waitDuration)
        {
            // Eğer bu bekleme süresi içindeyken [D]'ye basarsa (Atarsa)
            if (_isForcedGetOut) yield break; // Beklemeyi kes ve fonksiyondan çık

            timer += Time.deltaTime;
            yield return null;
        }
        // Eğer zorla atılmadıysa, normal bitiş sinyalini ver
        if (!_isForcedGetOut)
        {
            OnConversationFinished();
        }
        
        
    }

    /// <summary>
    /// Müşterinin inme sekansını (normal veya zorla) yönetir.
    /// </summary>
    IEnumerator PassengerGetOutSequence()
    {
        _roadSpawner.DecreaseSpeed(); // Arabayı yavaşlat
        _isFull = false;
        
        if (_currentCustomerEncounter != null && _currentCustomerEncounter.dialogueGraph != null)
        {
            bool dialogueFinished = false;
            Action onComplete = () => { dialogueFinished = true; };
            _nodeParser.OnDialogueComplete += onComplete;

            // Hangi start node'u kullanacağız?
            DialogueStartType typeToPlay = _isForcedGetOut ? DialogueStartType.ThrowOut : DialogueStartType.GetOut;

            // Tek grafiği, ilgili mod ile başlat
            _nodeParser.StartDialogue(_currentCustomerEncounter.dialogueGraph, typeToPlay);

            while (!dialogueFinished)
            {
                yield return null;
            }
            _nodeParser.OnDialogueComplete -= onComplete;
        }

        // Müşteri bize yumruk attı, o gün kazandığımız para çalındı ve gün bitti
        if (_currentCustomerEncounter.isForceQuittable&&!_isForcedGetOut)
        {
            _economyManager.ForceFinishResults(_currentCustomerEncounter);
            if (_currentActivePrefab != null)
            {
                StartCoroutine(PassengerPrefabDisable(_currentActivePrefab));
                _currentActivePrefab = null; // Referansı temizle
                _customerMover = null;
            }
            _isForcedGetOut = false;
            _isForcedFinish = true;
            CurrentState = State.Finished;
            yield break;
        }
        
        if (_isForcedGetOut) _economyManager.ForceQuitResults(_currentCustomerEncounter);
        else _economyManager.QuitResults(_currentCustomerEncounter);

        
        yield return new WaitForSeconds(pGetInOutDuration); 
        
        // (...sonra ışınla)
        if (_currentActivePrefab != null)
        {
            _currentActivePrefab.transform.position = pGetOutPosition.position;
        }
        if (_customerMover != null)
        {
            _customerMover.IsIn = false; // Tekrar yolla birlikte kaymaya başlasın
            _customerMover.IsWalk = false; // Tekrar yolla birlikte kaymaya başlasın
        }
        
        if (_currentActivePrefab != null)
        {
            StartCoroutine(PassengerPrefabDisable(_currentActivePrefab));
            _currentActivePrefab = null; 
            _customerMover = null;
        }
        
        _roadSpawner.IncreaseSpeed();
        _isForcedGetOut = false;
        CurrentState = State.Driving;
    }

    /// <summary>
    /// Müşteri prefab'ını temizler ve havuza geri döndürür.
    /// </summary>
    IEnumerator PassengerPrefabDisable(GameObject objectToReturn)
    {
        yield return new WaitForSeconds(pDestroyDuration);

        
        CustomerMover moverToReset = objectToReturn.GetComponent<CustomerMover>();
        
        // 2. O objenin component'ini resetle
        if (moverToReset != null)
        {
            moverToReset.ResetValues();
        }
        else
        {
            Debug.LogWarning("Objede CustomerMover script'i bulunamadı, resetlenemedi.");
        }

        PooledObjectManager.Instance.ReturnToPool(objectToReturn);
        
    }

    /// <summary>
    /// DialogueManager bu fonksiyonu ana diyalog bittiğinde çağırır.
    /// </summary>
    public void OnConversationFinished()
    {
        if (CurrentState == State.GetOut) return;
        
        if (CurrentState == State.Driving && _isFull)
        {
            Debug.Log("Diyalog normal bir şekilde bitti. Müşteri NORMAL iniyor.");
            _isForcedGetOut = false; 
            CurrentState = State.GetOut; 
        }
    }

    /// <summary>
    /// Günün bittiğini gösteren paneli açar.
    /// </summary>
    IEnumerator DayFinished()
    {
        //continue 4 seconds and show day end panel
        yield return new WaitForSeconds(4f);
        if(CurrentState!=State.Finished)
            CurrentState = State.Finished;
        _dayStarted = false;
        //fade in fade out
        StartCoroutine(UIHelperFunctions.instance.FadeCoroutine(() =>
        {
            UIDayEnd.instance.ShowPanel(currentDay);
            _roadSpawner.SetSpeedToZero();
        }));
    }
    
    void ForcedFinishDay()
    {
        _dayStarted = false;
        if(CurrentState!=State.Finished)
            CurrentState = State.Finished;
        StartCoroutine(UIHelperFunctions.instance.FadeCoroutine(() =>
        {
            UIDayEnd.instance.ShowPanel(currentDay);
            _roadSpawner.SetSpeedToZero();
            _isForcedFinish = false;
        }));
    }
    
    /// <summary>
    /// Kontak için atılan ışını Scene ekranında çizer.
    /// </summary>
    private void OnDrawGizmos()
    {
        Camera cam = (_mainCamera != null) ? _mainCamera : Camera.main;
        if (cam == null) return;
        Vector3 rayOrigin = cam.transform.position;
        Vector3 rayDirection = cam.transform.forward;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(rayOrigin, rayDirection * raycastDistance);
    }

    public void ResetIgnitionRotation()
    {
        if (ignitionObject != null)
        {
            ignitionObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    #region Save/Load Support Functions

    public void LoadGameData(int day, int cIndex, List<PackageType> packages)
    {
        currentDay = day;
        customerIndex = cIndex;
        currentPackageTypes = packages;
    
        // Yükleme sonrası gerekli UI güncellemeleri
        // Örneğin gün sayısını ekranda göstermek vb.
        // State'i Newspaper veya Started olarak ayarlayabilirsin.
        CurrentState = State.Newspaper; 
    }

    #endregion
}