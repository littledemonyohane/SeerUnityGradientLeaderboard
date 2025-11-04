using UnityEngine;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;
using System.Runtime.InteropServices;

public class UnpackUnity : MonoBehaviour
{
    private void Start()
    {
        string sourcePath = @"D:\SeerLauncher\games\NewSeer\Seer_Data\yoo\ConfigPackage\CacheBundleFiles";
        string targetPath = @"D:\SeerLauncher\games\NewSeer\Seer_Data\yoo\ConfigPackage\rawfile";

        // 确保目标目录存在
        Directory.CreateDirectory(targetPath);

        // 获取所有需要处理的文件
        string[] bundleFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);

        int totalProcessed = 0;
        foreach (string bundleFile in bundleFiles)
        {
            try
            {
                // 为每个文件创建独立的目标目录，避免文件名冲突
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(bundleFile);
                string fileTargetPath = Path.Combine(targetPath, fileNameWithoutExtension);
                Directory.CreateDirectory(fileTargetPath);
        
                // 调用 UnityUnpack 方法
                IntPtr a = Marshal.StringToHGlobalAnsi(bundleFile);
                IntPtr b = Marshal.StringToHGlobalAnsi(fileTargetPath);
        
                int result = UnityUnpack(a, b);
                totalProcessed += result;
        
                // 释放内存
                Marshal.FreeHGlobal(a);
                Marshal.FreeHGlobal(b);
        
                Debug.Log($"处理文件 {bundleFile}: 提取了 {result} 个资源");
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理文件 {bundleFile} 时出错: {ex.Message}");
            }
        }

        Debug.Log($"总共处理了 {totalProcessed} 个资源文件");
    
        // 添加：将生成的.bytes文件解析为.txt文件
        ParseBytesToTxt(targetPath, targetPath + "_txt");
    }


    /// <summary>
    /// 将.bytes文件解析为.txt文件
    /// </summary>
    /// <param name="bytesDirectory">包含.bytes文件的目录</param>
    /// <param name="outputDirectory">输出.txt文件的目录</param>
    public static void ParseBytesToTxt(string bytesDirectory, string outputDirectory)
    {
        try
        {
            // 确保输出目录存在
            Directory.CreateDirectory(outputDirectory);

            // 获取所有.bytes文件
            string[] bytesFiles = Directory.GetFiles(bytesDirectory, "*.bytes", SearchOption.AllDirectories);

            foreach (string bytesFile in bytesFiles)
            {
                try
                {
                    ParseSingleBytesFile(bytesFile, outputDirectory);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"解析文件 {bytesFile} 时出错: {ex.Message}");
                }
            }

            Debug.Log($"完成解析 {bytesFiles.Length} 个.bytes文件");
        }
        catch (Exception ex)
        {
            Debug.LogError($"ParseBytesToTxt执行出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析单个.bytes文件
    /// </summary>
    /// <param name="bytesFilePath">.bytes文件路径</param>
    /// <param name="outputDirectory">输出目录</param>
    private static void ParseSingleBytesFile(string bytesFilePath, string outputDirectory)
    {
        byte[] data = File.ReadAllBytes(bytesFilePath);
        string fileName = Path.GetFileNameWithoutExtension(bytesFilePath);
        string outputFilePath = Path.Combine(outputDirectory, fileName + ".txt");

        // 尝试以文本方式解析数据
        string content = ParseBytesContent(data);

        // 写入文本文件
        File.WriteAllText(outputFilePath, content);
        Debug.Log($"已解析: {fileName}.bytes -> {fileName}.txt");
    }

    /// <summary>
    /// 解析.bytes文件内容
    /// </summary>
    /// <param name="data">原始字节数据</param>
    /// <returns>解析后的文本内容</returns>
    private static string ParseBytesContent(byte[] data)
    {
        try
        {
            // 尝试使用UTF-8编码解析
            string content = System.Text.Encoding.UTF8.GetString(data);
            
            // 清理不可见字符，保留可读内容
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in content)
            {
                // 保留可打印字符和换行符
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ||
                    c == ',' || c == ':' || c == ';' || c == '.' ||
                    c == '!' || c == '?' || c == '-' || c == '_' ||
                    c == '(' || c == ')' || c == '[' || c == ']' ||
                    c == '{' || c == '}' || c == '"' || c == '\'' ||
                    c == '=' || c == '>' || c == '<' || c == '/' ||
                    c == '\\' || c == '|' || c == '*' || c == '&')
                {
                    sb.Append(c);
                }
                // 将控制字符替换为换行符
                else if (char.IsControl(c))
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    {
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
        catch
        {
            // 如果UTF-8解析失败，返回十六进制表示
            return BitConverter.ToString(data).Replace("-", " ");
        }
    }

    public static int UnityUnpack(IntPtr a, IntPtr b)
    {
        try
        {
            string stringAnsi1 = Marshal.PtrToStringAnsi(a);
            string stringAnsi2 = Marshal.PtrToStringAnsi(b);
            Directory.CreateDirectory(stringAnsi2);
            AssetsManager assetsManager = new AssetsManager();
            BundleFileInstance bundleFileInstance = assetsManager.LoadBundleFile(stringAnsi1, true);
            AssetsFileInstance assetsFileInstance =
                assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, 0, false);
            int num = 0;
            foreach (AssetFileInfo assetFileInfo in assetsFileInstance.file.GetAssetsOfType((AssetClassID)49))
            {
                AssetTypeValueField baseField =
                    assetsManager.GetBaseField(assetsFileInstance, assetFileInfo, (AssetReadFlags)0);
                string asString = baseField["m_Name"].AsString;
                byte[] asByteArray = baseField["m_Script"].AsByteArray;
                if (asByteArray != null && asByteArray.Length != 0)
                {
                    File.WriteAllBytes(Path.Combine(stringAnsi2, asString + ".bytes"), asByteArray);
                    ++num;
                }
            }

            assetsManager.UnloadAll(false);
            return num > 0 ? num : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return 0;
        }
    }
}