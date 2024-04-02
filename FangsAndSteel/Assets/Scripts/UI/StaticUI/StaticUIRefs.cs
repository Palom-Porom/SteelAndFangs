using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
        
public class StaticUIRefs : MonoBehaviour
{
    public static StaticUIRefs Instance { get; private set; }
    public TextMeshProUGUI HpText { get { return hpText; } }
    private TextMeshProUGUI hpText;
    public TextMeshProUGUI AttackText { get { return attackText; } }
    private TextMeshProUGUI attackText;
    public TextMeshProUGUI ReloadText { get { return reloadText; } }
    private TextMeshProUGUI reloadText;
    public TextMeshProUGUI AttackRadiusText { get { return attackRadiusText; } }
    private TextMeshProUGUI attackRadiusText;
     public TextMeshProUGUI MovementText { get { return movementText; } }
    private TextMeshProUGUI movementText;
    public TextMeshProUGUI VisionRadiusText { get { return visionRadiusText; } }
    private TextMeshProUGUI visionRadiusText;

    [SerializeField] private GameObject unitStats;

    public Image TurnIndicator { get { return turnIndicator; } }
    [SerializeField] private Image turnIndicator;
    public TextMeshProUGUI TurnTimer { get { return turnTimer; } }
    [SerializeField] private TextMeshProUGUI turnTimer;

    public GameObject UnitsUI { get { return unitsUI; } }
    [SerializeField] private GameObject unitsUI;

    public Image ShootOnMoveButton { get { return shootOnMoveButton; } }
    [SerializeField] private Image shootOnMoveButton;

    public GameObject NewTurnPanel { get { return newTurnPanel; } }
    [SerializeField] private GameObject newTurnPanel;

    void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        //hpText = unitStats.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        //attackText = unitStats.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        //reloadText = unitStats.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        //attackRadiusText = unitStats.transform.GetChild(3).GetComponent<TextMeshProUGUI>();
        //movementText = unitStats.transform.GetChild(4).GetComponent<TextMeshProUGUI>();
        //visionRadiusText = unitStats.transform.GetChild(5).GetComponent<TextMeshProUGUI>();
    }

    [HideInInspector] public bool endTurnBut;
    [HideInInspector] public bool stopMoveBut;


    [HideInInspector] public bool shootOnMoveBut;
    [HideInInspector] public bool shootOffBut;
    [HideInInspector] public bool autoPursuitBut;
    [HideInInspector] public bool enemyListBut;
    [HideInInspector] public UnitTypes newPursuiteUnitType;

    [HideInInspector] public bool newTurnStartBut;


    public void SetNewTurnButton() { newTurnStartBut = true; }
}
