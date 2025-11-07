using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScreenSetting : MonoBehaviour
{
    [SerializeField]
    private Toggle FullScreenToggle;

    private int originalWidth;
    private int originalHeight;
    //private bool isFullScreen;

    private void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            // Application.targetFrameRate = 60;
            FullScreenToggle.transform.gameObject.SetActive(false);
        }
        originalWidth = Screen.width;
        originalHeight = Screen.height;
        //isFullScreen = Screen.fullScreen;
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            // Application.targetFrameRate = 120;
            FullScreenToggle.isOn = false;
        }
        else
        {
            FullScreenToggle.isOn = true;
        }
        FullScreenToggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (isOn)
        {
            Screen.fullScreen = true;
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
        }
        else
        {
            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(1280, 720, false);
        }
    }
}
