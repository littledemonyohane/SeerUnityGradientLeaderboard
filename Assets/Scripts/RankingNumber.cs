using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RankingNumber : MonoBehaviour
{
    public Dropdown dropDown;
    public GameObject SSplus;
    public GameObject Splus;
    public GameObject S;
    public GameObject A;
    public GameObject B;
    public GameObject C;
    public GameObject D;
    public GameObject E;
    private void Start()
    {
        dropDown.onValueChanged.AddListener(oOnDropdownValueChanged);
    }

    void oOnDropdownValueChanged(int value)
    {
        Debug.Log("Selected option: " + dropDown.options[value].text);
        switch (value)
        {
            case 0:
                SSplus.SetActive(true);
                Splus.SetActive(true);
                S.SetActive(true);
                A.SetActive(true);
                B.SetActive(true);
                C.SetActive(true);
                D.SetActive(false);
                E.SetActive(false);
                break;
            case 7:
                SSplus.SetActive(true);
                Splus.SetActive(true);
                S.SetActive(true);
                A.SetActive(true);
                B.SetActive(true);
                C.SetActive(true);
                D.SetActive(true);
                E.SetActive(true);
                break;
            case 6:
                SSplus.SetActive(true);
                Splus.SetActive(true);
                S.SetActive(true);
                A.SetActive(true);
                B.SetActive(true);
                C.SetActive(true);
                D.SetActive(true);
                E.SetActive(false);
                break;
            case 5:
                SSplus.SetActive(true);
                Splus.SetActive(true);
                S.SetActive(true);
                A.SetActive(true);
                B.SetActive(true);
                C.SetActive(false);
                D.SetActive(false);
                E.SetActive(false);
                break;
            case 4:
                SSplus.SetActive(true);
                Splus.SetActive(true);
                S.SetActive(true);
                A.SetActive(true);
                B.SetActive(false);
                C.SetActive(false);
                D.SetActive(false);
                E.SetActive(false);
                break;
            case 3:
                SSplus.SetActive(true);
                Splus.SetActive(true);
                S.SetActive(true);
                A.SetActive(false);
                B.SetActive(false);
                C.SetActive(false);
                D.SetActive(false);
                E.SetActive(false);
                break;
            case 2:
                SSplus.SetActive(true);
                Splus.SetActive(true);
                S.SetActive(false);
                A.SetActive(false);
                B.SetActive(false);
                C.SetActive(false);
                D.SetActive(false);
                E.SetActive(false);
                break;
            case 1:
                SSplus.SetActive(true);
                Splus.SetActive(false);
                S.SetActive(false);
                A.SetActive(false);
                B.SetActive(false);
                C.SetActive(false);
                D.SetActive(false);
                E.SetActive(false);
                break;
            default: 
                break;
        }
    }

}
