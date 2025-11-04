using System;
using System.IO;
using System.Threading.Tasks;
using DefaultNamespace.Data;
using DefaultNamespace.MessageBox;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Version
{
    public class VersionManager : MonoBehaviour
    {
        private string _outputPath;
        private const string ZipUrl = "https://raw.githubusercontent.com/oldml/SeerUnityConfig/main/config/monsters.json";

        public MessageBox messageBox;
        
        public class GitHubFileInfo
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string SHA { get; set; }
            public int Size { get; set; }
            public string URL { get; set; }
            public string HtmlURL { get; set; }
            public string GitURL { get; set; }
            public string DownloadURL { get; set; }
            public string Type { get; set; }
            public string Content { get; set; }
            public string Encoding { get; set; }
        }


        private async void Start()
        {
            try
            {
                _outputPath = Path.Combine(Application.persistentDataPath, "monsters.json");

                // 检查远程文件是否有更新
                string sha = await GetFileLastSHA("oldml", "SeerUnityConfig", "config/monsters.json");

                var localSHA = "";

                if (PlayerPrefs.HasKey("localSHA"))
                {
                    // 比较本地缓存版本
                    localSHA = PlayerPrefs.GetString("localSHA");
                }

                if (sha != localSHA || !File.Exists(_outputPath))
                {
                    messageBox.Setup("更新提示", "有新版本可用，是否更新？\n 大小：15MB", async () =>
                    {
                        try
                        {
                            // 等待下载完成且成功
                            bool downloadSuccess = await DownloadMonstersJsonAsync();
                            if (!downloadSuccess)
                            {
                                Debug.LogError("下载失败，无法继续执行");
                            }
                            else
                            {
                                PlayerPrefs.SetString("localSHA", sha);
                            }
                            RefreshMonstersData();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"更新过程中发生错误: {ex.Message}");
                        }
                    });
                }
                else
                {
                    Debug.Log("本地缓存版本一致，无需更新");
                    RefreshMonstersData();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"error: {e.Message}");
            }
        }

        private async Task<string> GetFileLastSHA(string owner, string repo, string filePath)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{filePath}";

            using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("User-Agent", "Unity-App");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError($"获取commit信息失败: {request.error}");
                return null;
            }

            string jsonResponse = request.downloadHandler.text;

            GitHubFileInfo fileInfo = JsonConvert.DeserializeObject<GitHubFileInfo>(jsonResponse);

            if (fileInfo != null)
            {
                string fileSha = fileInfo.SHA;
                Debug.Log($"文件SHA: {fileSha}");
                return fileSha; // 使用SHA作为版本标识
            }

            return null;
        }

        private static async Task<bool> DownloadMonstersJsonAsync(int maxRetries = 3)
        {
            var outputPath = Path.Combine(Application.persistentDataPath, "monsters.json");

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                UnityWebRequest request = UnityWebRequest.Get(ZipUrl);
                request.timeout = 30; // 设置超时时间（秒）

                try
                {
                    var operation = request.SendWebRequest();

                    // 等待请求完成
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

#if UNITY_2020_1_OR_NEWER
                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
                    {
                        Debug.LogError($"下载失败 (尝试 {attempt + 1}/{maxRetries + 1}): {request.error}");
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(1000 * (attempt + 1)); // 递增延迟重试
                        }

                        continue;
                    }

                    // 写入文件
                    byte[] fileBytes = request.downloadHandler.data;
                    await File.WriteAllBytesAsync(outputPath, fileBytes);
                    Debug.Log($"文件已下载到: {outputPath}");
                    return true; // 下载成功
                }
                catch (Exception ex)
                {
                    Debug.LogError($"发生未预期的错误: {ex.Message}");
                    Debug.LogError($"下载失败 (尝试 {attempt + 1}/{maxRetries + 1}): {ex.Message}");
                    Debug.LogError($"内部异常: {ex.InnerException?.Message}");
                    return false; // 未知错误，不重试
                }
                finally
                {
                    request.Dispose(); // 释放资源
                }
            }

            Debug.LogError("达到最大重试次数，下载失败");
            return false;
        }

        private void RefreshMonstersData()
        {
            string outputPath = Path.Combine(Application.persistentDataPath, "monsters.json");
            Debug.Log(outputPath);
            if (File.Exists(outputPath))
            {
                Debug.Log("ReadMonstersData");
                string fileData = File.ReadAllText(outputPath);
                MonstersData.MonstersRoot = JsonConvert.DeserializeObject<MonstersRoot>(fileData);
            }
        }
    }
}