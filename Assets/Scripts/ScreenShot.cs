using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ScreenShot : MonoBehaviour
{
    private byte[] m_bytes;
    public string exportPath = Application.streamingAssetsPath;

    public RectTransform shotRange;
    public RectTransform contentSize;
    public Canvas canvas;

    private Vector2 OriginalResloution;
    private bool OriginalIsFullScreen;
    public GameObject InfoCanvas;

    public Camera cam;
    public Camera RenderCam;
    public RenderTexture RT_3840;
    public RenderTexture RT_7000;

    public AutoDownloadJson ADLJ;
    private float RTScaleOffset = 1f;

    public Transform SSplus;
    public Transform Splus;
    public Transform S;
    public Transform A;
    public Transform B;
    public Transform C;

    public SaveToGallery saveToGallery;
    public void GetSpriteByImg(Canvas _canvas, RectTransform _img)
    {
        StartCoroutine(CaptureByRect(_canvas.pixelRect, _img));
    }

    private void Update()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentSize);
        //shotRange.sizeDelta = contentSize.sizeDelta;
        if (SSplus.childCount + Splus.childCount + S.childCount + A.childCount + B.childCount + C.childCount >= 0
            && RT_3840.height == 3840)
        {
            RTScaleOffset = (float)RT_7000.height / (float)RT_3840.height;
            //// 将原始渲染纹理的内容复制到新的渲染纹理
            //Graphics.Blit(RT, newRenderTexture);

            //// 使用新的渲染纹理替换原始渲染纹理
            //RT.Release();
            //Graphics.CopyTexture(newRenderTexture, RT);
            //newRenderTexture.Release();
        }
    }


    public void ShotScreen()
    {
        //OriginalResloution = new Vector2(Screen.width,Screen.height);
        //OriginalIsFullScreen = Screen.fullScreen;
        //Screen.SetResolution((int)2160, (int)3840, true);
        //Debug.Log($"修改后的分辨率:{Screen.width}*{Screen.height}");
        GetSpriteByImg(canvas, shotRange);
    }

    public void CamSetRT()
    {
        cam.depth = -2;
        if (SSplus.childCount + Splus.childCount + S.childCount + A.childCount + B.childCount + C.childCount >= 0
            && RT_3840.height == 3840)
        {
            cam.targetTexture = RT_7000;
        }
        else
        {
            cam.targetTexture = RT_3840;

        }
    }
    public void CamRemoveRT()
    {
        cam.depth = 0;
        cam.targetTexture = null;
    }

    private IEnumerator CaptureByRect(Rect _mRect, RectTransform _img)
    {

        //等待渲染线程结束  
        yield return new WaitForEndOfFrame();
        //初始化Texture2D  
        //Texture2D _texture = ScreenCapture.CaptureScreenshotAsTexture();
        //Texture2D m_texture = new Texture2D((int)_mRect.width, (int)_mRect.height, TextureFormat.RGB24, false);

        //==============================================
        RenderCam.transform.gameObject.SetActive(true);
        RenderTexture.active = cam.activeTexture;



        cam.Render();

        Texture2D m_texture = new Texture2D(cam.activeTexture.width, (int)Mathf.Abs(cam.activeTexture.height * contentSize.rect.height / 3614f / RTScaleOffset), TextureFormat.RGB24, false);
        //读取屏幕像素信息并存储为纹理数据  
        //m_texture.ReadPixels(_mRect, 0, 0);
        //if (Application.platform == RuntimePlatform.Android)
        //{
        //    float tempHeight = cam.activeTexture.height - Mathf.Abs(cam.activeTexture.height * contentSize.rect.height / 3614f / RTScaleOffset);
        //    _mRect = new Rect(0, tempHeight, cam.activeTexture.width, Mathf.Abs(cam.activeTexture.height * contentSize.rect.height / 3614f / RTScaleOffset));

        //}
        //else
        //{
        //    _mRect = new Rect(0, 0, cam.activeTexture.width, Mathf.Abs(cam.activeTexture.height * contentSize.rect.height / 3614f / RTScaleOffset));

        //}
        _mRect = new Rect(0, 0, cam.activeTexture.width, Mathf.Abs(cam.activeTexture.height * contentSize.rect.height / 3614f / RTScaleOffset));

        m_texture.ReadPixels(_mRect, 0, 0);
        m_texture.Apply();
        m_bytes = m_texture.EncodeToPNG();


        //Texture2D m_texture = new Texture2D(Screen.width, (int)Mathf.Abs(Screen.height * contentSize.rect.height / 3614f), TextureFormat.RGB24, false);
        ////读取屏幕像素信息并存储为纹理数据  
        ////m_texture.ReadPixels(_mRect, 0, 0);
        //_mRect = new Rect(0, Screen.height - Mathf.Abs(Screen.height * contentSize.rect.height / 3614f), Screen.width, Mathf.Abs(Screen.height * contentSize.rect.height / 3614f));
        //m_texture.ReadPixels(_mRect, 0, 0);
        //m_texture.Apply();
        //m_bytes = m_texture.EncodeToPNG();

        //Sprite v_sprite = Sprite.Create(m_texture, new Rect(0, 0, m_texture.width, m_texture.height), Vector2.zero);
        //string v_str_base64 = System.Convert.ToBase64String(m_bytes);

        //==============================================

        string Name = DateTime.Now.ToString("yyyyMMddHHmmss");


        cam.depth = 0;
        cam.targetTexture = null;

        Debug.Log("图片生成完成了");
        if (Application.platform == RuntimePlatform.Android)
        {
            StreamWriter sw;
            string Path_save = "";
            //应用平台判断，路径选择
            if (Application.platform == RuntimePlatform.Android)
            {
                string origin;
                string destination = "/sdcard/DCIM/";
                Debug.Log("找目录前");
                //if (!Directory.Exists(destination))
                //{
                //    Debug.Log("开始创建目录");
                //    Directory.CreateDirectory(destination);
                //    Debug.Log("完成创建目录");
                //}
                destination = destination + "/" + Name + ".png";
                Path_save = destination;
            }
            Debug.Log("存储文件前");
            //保存文件
            File.WriteAllBytes(Path_save, m_bytes);
            InfoCanvas.GetComponent<AlertInfo>().Info = $"文件已保存至相册!";
            //saveToGallery.SaveImageToGallery(m_texture);

            string[] paths = new string[1];
            paths[0] = Path_save;
            using (AndroidJavaClass PlayerActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject playerActivity = PlayerActivity.GetStatic<AndroidJavaObject>("currentActivity");
                using (AndroidJavaObject Conn = new AndroidJavaObject("android.media.MediaScannerConnection", playerActivity, null))
                {
                    Conn.CallStatic("scanFile", playerActivity, paths, null, null);
                }
            }
        }
        else
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktopPath, Name + ".png");

#if UNITY_EDITOR || UNITY_STANDALONE
            filePath = Path.Combine(desktopPath, Name + ".png");
#endif
            Debug.Log("截图已保存至：" + filePath);
            InfoCanvas.GetComponent<AlertInfo>().Info = $"文件已保存至桌面!";
            System.IO.File.WriteAllBytes(filePath, m_bytes);
        }

        InfoCanvas.SetActive(true);

        //Screen.SetResolution((int)OriginalResloution.x, (int)OriginalResloution.y, OriginalIsFullScreen);
        //Debug.Log($"改回的分辨率:{Screen.resolutions[0].width}*{Screen.resolutions[0].height}");

    }

}
