using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AlertInfo : MonoBehaviour
{
    public string Info;
    [SerializeField]
    private Text text;
    void ActiveFalse()
    {
        transform.gameObject.SetActive(false);
    }
    void ChangeText()
    {
        text.text = Info;
    }

}
