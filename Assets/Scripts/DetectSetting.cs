using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DetectSetting : MonoBehaviour
{
    [SerializeField]
    private Toggle DetectToggle;
    [SerializeField]
    private AutoDownloadJson ADLJ;
    void Start()
    {
        DetectToggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (isOn)
        {
            ADLJ.IsDetectDuplicates = true;
        }
        else
        {
            ADLJ.IsDetectDuplicates = false;
        }
    }
}
