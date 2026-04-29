using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public static class TextureLoader
    {
        public static IEnumerator LoadTexture(int id, GameObject currentObj)
        {
            string address = SeerResources.PetHeadUrl(id);

            // 检查本地缓存文件是否存在
            string cachePath = Path.Combine(Application.persistentDataPath, $"{id}.png");
            Texture2D t2d = null;

            // 如果本地缓存文件存在，直接从本地加载
            if (File.Exists(cachePath))
            {
                byte[] fileData = File.ReadAllBytes(cachePath);
                t2d = new Texture2D(200, 200);
                t2d.LoadImage(fileData);
                Debug.Log($"从本地缓存加载图片: {id}.png");
            }
            else
            {
                // 本地无缓存，从网络下载
                UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(address);
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError(uwr.error);
                    currentObj.transform.Find("NoImage").gameObject.SetActive(true);

                    // 等待一段时间后重试
                    yield return new WaitForSeconds(1.0f);
                    yield return LoadTexture(id, currentObj);
                }
                else
                {
                    t2d = DownloadHandlerTexture.GetContent(uwr);
                    // 保存到本地缓存
                    byte[] textureBytes = t2d.EncodeToPNG();
                    File.WriteAllBytes(cachePath, textureBytes);
                    Debug.Log($"图片已下载并缓存: {cachePath}");
                }
            }

            // 应用图片到UI
            if (t2d != null)
            {
                Sprite spr = Sprite.Create(t2d, new Rect(0, 0, t2d.width, t2d.height), new Vector2(0.5f, 0.5f));
                currentObj.transform.Find("NoImage").gameObject.SetActive(false);
                currentObj.transform.Find("Header").GetComponent<Image>().sprite = spr;
            }
        }
    }

}