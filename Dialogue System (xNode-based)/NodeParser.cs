using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using XNode;

public class NodeParser : MonoBehaviour
{
    public static NodeParser instance;
    public DialogueSystemGraph graph;
    private Coroutine _parser;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;
    public Image speakerImage;
    public Button choiceButton1;
    public Button choiceButton2;
    TextMeshProUGUI choiceTextComp1;
    TextMeshProUGUI choiceTextComp2;
    BaseNode _currentNode;
    GameManager _gameManager;
    public event Action OnDialogueComplete;
    public static event Action<PackageType> OnPackageGiven;
    public static event Action<PackageType> OnPackageTaken;

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Start()
    {
        _gameManager = GameManager.instance;
    }

    public void StartDialogue(DialogueSystemGraph newGraph, DialogueStartType startType = DialogueStartType.Main)
    {
        this.graph = newGraph; 
    
        // Grafikteki node'ları gez ve istenen tipteki StartNode'u bul
        bool foundStart = false;
        foreach (BaseNode b in graph.nodes)
        {
            // Hem StartNode mu? Hem de tipi bizim istediğimiz mi?
            if (b is StartNode startNode && startNode.startType == startType)
            {
                graph.current = b;
                foundStart = true;
                break;
            }
        }

        if(foundStart && graph.current != null) 
        {
            if (_parser != null)
            {
                StopCoroutine(_parser);
                _parser = null;
            }
        
            // clear UI
            speakerNameText.text = "";
            dialogueText.text = "";
            speakerImage.gameObject.SetActive(false);
            
            _parser = StartCoroutine(ParseNode());
        }
        else
        {
            // if not found, log warning and end dialogue
            Debug.LogWarning($"Grafikte '{startType}' tipinde bir StartNode bulunamadı. Diyalog pas geçiliyor.");
            EndDialogue(); 
        }
    }
    

    IEnumerator ParseNode()
    {
        _currentNode = graph.current;

        choiceButton1.gameObject.SetActive(false);
        choiceButton2.gameObject.SetActive(false);
        // start node kontrolü
        if (_currentNode is StartNode)
        {
            NextNode("exit");
        }
        // dialogue node kontrolü
        else if (_currentNode is DialogueNode node) 
        {
            // update UI
            speakerNameText.text = node.speakerName;
            dialogueText.text = node.dialogueText;
            if (node.GetSprite() != null)
            {
                speakerImage.sprite = node.GetSprite();
                speakerImage.gameObject.SetActive(true);
            }
            else
            {
                speakerImage.gameObject.SetActive(false);
            }

            // Seçenek Kontrolü
            if (node.GetChoices() != null && node.GetChoices().Count > 0)
            {
                CreateChoiceButtons(node.GetChoices()); // Listeyi parametre olarak gönder
            }
            else
            {
                // Seçenek yoksa düz devam et
                StartCoroutine(AutoSkipRoutine(node.GetWaitTime()));
            }
        }
    
        yield return null;
    }
    void CreateChoiceButtons(List<string> choices)
    {
        choiceButton1.onClick.RemoveAllListeners();
        choiceButton2.onClick.RemoveAllListeners();

        choiceButton1.interactable = true;
        choiceButton2.interactable = true;

        // Birinci seçenek var mı kontrol et ve ayarla
        if (choices.Count > 0)
        {
            choiceButton1.gameObject.SetActive(true);
            Debug.Log("1. seçenek oluşturuluyor...");
            DialogueNode diagNode = _currentNode as DialogueNode;
            if (diagNode != null && diagNode.isPackageDecisionNode)
            {
                if (_gameManager.CurrentCustomerEncounter.packageAction != PackageAction.None)
                {
                    if (_gameManager.CurrentCustomerEncounter.packageAction == PackageAction.Take)
                    {
                        if (!_gameManager.currentPackageTypes.Contains(
                                _gameManager.CurrentCustomerEncounter.packageType))
                        {
                            choiceButton1.interactable = false;
                        }
                    }
                }
            }

            if(choiceTextComp1 == null)
                choiceTextComp1 = choiceButton1.GetComponentInChildren<TextMeshProUGUI>();
            // text'i ayarla
            choiceTextComp1.text = choices[0];
        
            // yeni listener ekle
            choiceButton1.onClick.AddListener(() => OnChoiceSelected(0));
        }

        // İkinci seçenek var mı kontrol et ve ayarla
        if (choices.Count > 1)
        {
            choiceButton2.gameObject.SetActive(true);
            Debug.Log("2. seçenek oluşturuluyor...");
            
            if (choiceTextComp2 == null)
                choiceTextComp2 = choiceButton2.GetComponentInChildren<TextMeshProUGUI>();
            choiceTextComp2.text = choices[1];
        
            // Yeni listener ekle
            choiceButton2.onClick.AddListener(() => OnChoiceSelected(1));
        }
    }
    
    void OnChoiceSelected(int index)
    {
        DialogueNode diagNode = _currentNode as DialogueNode;
        if (diagNode != null && diagNode.isPackageDecisionNode)
        {
            // package actions
            if (_gameManager.CurrentCustomerEncounter.packageAction != PackageAction.None)
            {
                if (_gameManager.CurrentCustomerEncounter.packageAction == PackageAction.Give)
                {
                    if (index == 0)
                    {
                        _gameManager.currentPackageTypes.Add(_gameManager.CurrentCustomerEncounter.packageType);
                        OnPackageGiven?.Invoke(_gameManager.CurrentCustomerEncounter.packageType);
                    }
                    else if (index == 1)
                    {
                        //do nothing but
                        //if member of organization
                        if (_gameManager.CurrentCustomerEncounter.isMemberOfOrganization)
                        {
                            // decrease reputation
                            EconomyManager.instance.ChangeOrganizationReputation(-10);
                        }
                    }
                }
                else if (_gameManager.CurrentCustomerEncounter.packageAction == PackageAction.Take)
                {
                    if (index == 0)
                    {
                        if (_gameManager.currentPackageTypes.Contains(_gameManager.CurrentCustomerEncounter
                                .packageType))
                        {
                            _gameManager.currentPackageTypes.Remove(_gameManager.CurrentCustomerEncounter.packageType);
                            OnPackageTaken?.Invoke(_gameManager.CurrentCustomerEncounter.packageType);
                            //package results
                            EconomyManager.instance.PackageResults(_gameManager.CurrentCustomerEncounter);
                            // if member of organization check
                            if (_gameManager.CurrentCustomerEncounter.isMemberOfOrganization)
                            {
                                // increase reputation
                                EconomyManager.instance.ChangeOrganizationReputation(20);
                            }
                        }
                        else
                        {
                            //do nothing
                            //zaten buton interactable false yapıldı, yani basılamaz buraya da girmez.
                        }
                    }
                    else if (index == 1)
                    {
                        if (_gameManager.currentPackageTypes.Contains(_gameManager.CurrentCustomerEncounter
                                .packageType))
                        {
                            _gameManager.currentPackageTypes.Remove(_gameManager.CurrentCustomerEncounter.packageType);
                            OnPackageTaken?.Invoke(_gameManager.CurrentCustomerEncounter.packageType);
                        }
                        else
                        {
                            //do nothing but
                            //if member of organization
                            if (_gameManager.CurrentCustomerEncounter.isMemberOfOrganization)
                            {
                                // decrease reputation
                                EconomyManager.instance.ChangeOrganizationReputation(-20);
                            }
                        }
                    }

                }
            }
        }

        string choice = $"choices {index}";
        
        NextNode(choice);
    }
    IEnumerator AutoSkipRoutine(float waitTime = 3f)
    {
        yield return new WaitForSeconds(waitTime); // bekle
        NextNode("exit");
    }
    public void NextNode(string fieldName)
    {
        if(_parser != null)
        {
            StopCoroutine(_parser);
            _parser = null;
        }
        foreach (NodePort port in graph.current.Ports)
        {
            if (port.fieldName == fieldName)
            {
                // EĞER BAĞLANTI YOKSA HATA VERMEMESİ İÇİN KONTROL:
                if (port.IsConnected) 
                {
                    graph.current = port.Connection.node as BaseNode;
                    _parser = StartCoroutine(ParseNode());
                }
                else
                {
                    EndDialogue();
                }
                return; // port finded, exit the method
            }
        }
        EndDialogue();
    }
    void EndDialogue()
    {
        // clear UI
        dialogueText.text = ""; 
        speakerNameText.text = "";
        choiceButton1.gameObject.SetActive(false);
        choiceButton2.gameObject.SetActive(false);
        
        // GameManager'a haber ver
        OnDialogueComplete?.Invoke();
    }
}