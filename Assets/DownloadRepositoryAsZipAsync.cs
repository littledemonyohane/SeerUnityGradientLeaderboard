using System;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine.Networking; // 需要引入 SharpZipLib
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

public class DownloadRepositoryAsZipAsync : MonoBehaviour
{
    string persistentPath;

    private string zipPath;
    private string extractPath;
    
    public static MonstersRoot MonstersRootData;

    private async void Start()
    {
        try
        {
            persistentPath = Application.persistentDataPath;
            zipPath = Path.Combine(persistentPath, "SeerUnityConfig.zip");
            extractPath = Path.Combine(persistentPath, "Extracted");

            // 等待下载完成且成功
            bool downloadSuccess = await DownloadZipAsync();
            if (!downloadSuccess)
            {
                Debug.LogError("下载失败，无法继续执行");
                return;
            }

            ReadMonstersData(4500);

            // Debug.Log($"ZIP路径: {zipPath}");
            // Debug.Log($"解压路径: {extractPath}");

            // // 确保解压目录存在
            // if (!Directory.Exists(extractPath))
            // {
            //     Directory.CreateDirectory(extractPath);
            // }
            //
            // // 检查ZIP文件是否存在
            // if (File.Exists(zipPath))
            // {
            //     ExtractZipToSandbox(zipPath, extractPath);
            // }
            // else
            // {
            //     Debug.LogError($"ZIP文件不存在: {zipPath}");
            // }
        }
        catch (Exception e)
        {
            Debug.LogError($"error: {e.Message}");
        }
    }


    private void ExtractZipToSandbox(string zipFilePath, string extractPath)
    {
        try
        {
            // 检查ZIP文件是否存在
            if (!File.Exists(zipFilePath))
            {
                Debug.LogError($"ZIP文件不存在: {zipFilePath}");
                return;
            }

            // 确保解压目录存在
            if (!Directory.Exists(extractPath))
            {
                Directory.CreateDirectory(extractPath);
            }

            using (var zipInputStream = new ZipInputStream(File.OpenRead(zipFilePath)))
            {
                ZipEntry entry;
                while ((entry = zipInputStream.GetNextEntry()) != null)
                {
                    try
                    {
                        string entryName = entry.Name.Replace('/', Path.DirectorySeparatorChar);
                        string fullPath = Path.Combine(extractPath, entryName);

                        // 防止路径遍历攻击
                        if (!fullPath.StartsWith(extractPath))
                        {
                            Debug.LogWarning($"跳过不安全的路径: {entryName}");
                            continue;
                        }

                        // 创建目录
                        string directory = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // 提取文件
                        if (!entry.IsDirectory && !string.IsNullOrEmpty(Path.GetFileName(entry.Name)))
                        {
                            using (var fileStream = File.Create(fullPath))
                            {
                                zipInputStream.CopyTo(fileStream);
                            }

                            Debug.Log($"已解压: {entry.Name}");
                        }
                    }
                    catch (Exception entryEx)
                    {
                        Debug.LogError($"解压条目 {entry?.Name} 时出错: {entryEx.Message}");
                    }
                }
            }

            Debug.Log("ZIP解压完成");
        }
        catch (Exception ex)
        {
            Debug.LogError($"解压失败: {ex.Message}");
            Debug.LogError($"堆栈跟踪: {ex.StackTrace}");
        }
    }


    public static async Task<bool> DownloadZipAsync(int maxRetries = 3)
{
    // string zipUrl = "https://github.com/oldml/SeerUnityConfig/archive/refs/heads/main.zip";
    string zipUrl = "https://raw.githubusercontent.com/oldml/SeerUnityConfig/main/config/monsters.json";
    // string outputPath = Path.Combine(Application.persistentDataPath, "SeerUnityConfig.zip");
    string outputPath = Path.Combine(Application.persistentDataPath, "monsters.json");

    for (int attempt = 0; attempt <= maxRetries; attempt++)
    {
        UnityWebRequest request = UnityWebRequest.Get(zipUrl);
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


    public void ReadMonstersData(int petId)
    {
        string outputPath = Path.Combine(Application.persistentDataPath, "Extracted", "SeerUnityConfig-main", "config");
        string monsterJsonPath = Path.Combine(outputPath, "monsters.json");
        Debug.Log(monsterJsonPath);
        if (File.Exists(monsterJsonPath))
        {
            Debug.Log("ReadMonstersData");
            string fileData = File.ReadAllText(monsterJsonPath);
            MonstersRootData = JsonConvert.DeserializeObject<MonstersRoot>(fileData);
            Debug.Log($"{MonstersRootData.Monsters.Monster.Find(x => x.ID == petId).DefName}");

            AutoDownloadJson.petData = MonstersRootData; 
        }
    }
}