using UnityEngine;
using System.Collections;
using System.IO;

public class SaveToGallery : MonoBehaviour
{
    public string imageName = "Seer.png";

    public void SaveImageToGallery(Texture2D texture)
    {
        Debug.Log("准备在安卓上保存");
        byte[] bytes = texture.EncodeToPNG();
        string path = Path.Combine(Application.persistentDataPath, imageName);
        File.WriteAllBytes(path, bytes);

        // Refresh Android gallery
        AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection");
        mediaScanner.CallStatic("scanFile", new string[] { path }, new string[] { "image/png" }, null);
    }
}