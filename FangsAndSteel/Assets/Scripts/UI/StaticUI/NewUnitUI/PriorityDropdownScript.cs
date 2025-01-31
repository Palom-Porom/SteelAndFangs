using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
//using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UI;

public class PriorityDropdownScript : MonoBehaviour
{
    public TMP_Dropdown dropdown0;
    public TMP_Dropdown dropdown1;
    public TMP_Dropdown dropdown2;
    public TMP_Dropdown dropdown3;
    public TMP_Dropdown dropdown4;
    public TMP_Dropdown dropdown5;
    //public TMP_Dropdown dropdown6;

    public static List<TMP_Dropdown> dropdownLST;
    public void Start()
    {
        List<string> priorityList = new List<string>();
        priorityList.Add("���������");
        priorityList.Add("�� ���. HP");
        priorityList.Add("���������");
        priorityList.Add("����������");
        priorityList.Add("��������������� ���������");
        priorityList.Add("����");
        //priorityList.Add("A���������");
        priorityList.Add(" ");
        dropdownLST = new List<TMP_Dropdown>(6) { dropdown0, dropdown1, dropdown2, dropdown3, dropdown4, dropdown5 };
        //Debug.Log(dropdownLST[0]);

        foreach (var priority in priorityList)
        {
            foreach (var dropdown in dropdownLST) {dropdown.options.Add(new TMP_Dropdown.OptionData() { text = priority });}
        }

        foreach (var dropdown in dropdownLST) 
        { 
            dropdown.onValueChanged.AddListener(delegate { DropdownItemSelected(dropdown);}); 
        }

        DropdownItemSelected(0, (int)priorityItems.Nearest);
        DropdownItemSelected(1, (int)priorityItems.ByMinHp);
        DropdownItemSelected(2, (int)priorityItems.Infantry);
        DropdownItemSelected(3, (int)priorityItems.MachineGunner);
        DropdownItemSelected(4, (int)priorityItems.AntiTankInf);
        DropdownItemSelected(5, (int)priorityItems.Tank);
            
    }
    public void DropdownItemSelected(TMP_Dropdown dropdown)
    {
        int index = dropdown.value;
        foreach (var x in dropdownLST)
        {
            if (dropdown == x)
            {
                var t = dropdown.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                t.text = dropdown.options[index].text;
            }
            else if (x.value == index & index != (int)priorityItems.Empty)
            {
                x.value = (int)priorityItems.Empty;
                DropdownItemSelected(x);
            }
        }
        StaticUIRefs.Instance.isNeededPrioritiesUpdate = true;
    }

    public static void DropdownItemSelected(int dropdownIndex, int priorityIndex)
    {
        dropdownLST[dropdownIndex].value = priorityIndex;
        var t = dropdownLST[dropdownIndex].transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        t.text = dropdownLST[dropdownIndex].options[priorityIndex].text;
        foreach (var x in dropdownLST)
        {
            if (x.value == priorityIndex & priorityIndex != (int)priorityItems.Empty & dropdownLST.IndexOf(x) != dropdownIndex)
            {
                DropdownItemSelected(dropdownLST.IndexOf(x), (int)priorityItems.Empty);
            }
        }
    }

    public static PriorityInfo ReturnDropdownPriorityInfo()
    {
        var dropdownPriority = new PriorityInfo();
        var unitPrioritiesLST = new List<(UnitTypes,int)>();
        int distancePriority = -1;
        int minHpPriority = -1;
        foreach (var dropdown in dropdownLST)
        {
            if (dropdown.value == (int)priorityItems.Infantry)
            {
                unitPrioritiesLST.Add((UnitTypes.BaseInfantry, dropdownLST.IndexOf(dropdown)));
            }
            else if (dropdown.value == (int)priorityItems.MachineGunner)
            {
                unitPrioritiesLST.Add((UnitTypes.MachineGunner, dropdownLST.IndexOf(dropdown)));
            }
            else if (dropdown.value == (int)priorityItems.AntiTankInf)
            {
                unitPrioritiesLST.Add((UnitTypes.AntyTank, dropdownLST.IndexOf(dropdown)));
            }
            else if (dropdown.value == (int)priorityItems.Tank)
            {
                unitPrioritiesLST.Add((UnitTypes.Tank, dropdownLST.IndexOf(dropdown)));
            }
            else if (dropdown.value == (int)priorityItems.Nearest)
            {
                distancePriority = dropdownLST.IndexOf(dropdown);
            }
            else if (dropdown.value == (int)priorityItems.ByMinHp)
            {
                minHpPriority = dropdownLST.IndexOf(dropdown);
            }
        }
        dropdownPriority.unitsPriorities = unitPrioritiesLST.ToNativeArray(Allocator.TempJob);
        dropdownPriority.distancePriority = distancePriority;
        dropdownPriority.minHpPriority = minHpPriority;
        return dropdownPriority;
    }
    public enum priorityItems
    {
        Nearest,
        ByMinHp,    
        Infantry,
        MachineGunner,
        AntiTankInf,
        Tank,
        Empty
    }

    //[Flags]
    //public enum priorityUnitTypes
    //{
    //    None = 0,
    //    BaseInfantry = 1,
    //    MachineGunner = 2,
    //    AntiTankInf = 4,
    //    Tank = 8,
    //    Artillery = 16,
    //    Everything = 31
    //}

}
    public struct PriorityInfo
    {
        public NativeArray<(UnitTypes, int)> unitsPriorities;
        public int distancePriority;
        public int minHpPriority;
    }
