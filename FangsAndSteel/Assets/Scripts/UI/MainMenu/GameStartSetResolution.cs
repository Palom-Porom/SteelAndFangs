using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameStartSetResolution : MonoBehaviour
{
    public TMP_Dropdown dropdown;
    void Awake()
    {
        var resolutions = Screen.resolutions;
        var s = $"{resolutions[resolutions.Length - 1].width}" + "x" + $"{resolutions[resolutions.Length - 1].height}";
        var flag = false;
        foreach (var op in dropdown.options)
        {
            if (s == op.text)
            {
                flag = true;
                break;
            }
        }
        if (!flag)
        {
            //dropdown.options.Add(new TMP_Dropdown.OptionData() { text = s });
            //dropdown.value = 18;
            dropdown.options.Insert(0, new TMP_Dropdown.OptionData() { text = s });
            dropdown.value = 0;
            Screen.SetResolution(resolutions[resolutions.Length - 1].width, resolutions[resolutions.Length-1].height,Screen.fullScreen);
            Debug.Log("���");
        }
        
    }

        
        

}
