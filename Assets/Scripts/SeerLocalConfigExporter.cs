using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

/// <summary>
/// Exports locally available Seer config into the JSON formats used by this project.
/// Supports both a real install and a synced mirror root.
/// </summary>
public class SeerLocalConfigExporter
{
    static readonly Regex AllowedNamePattern =
        new Regex(@"^[\u4e00-\u9fffA-Za-z0-9\-\s\u3000\u00b7]+$", RegexOptions.Compiled);

    static readonly Regex HasChinesePattern =
        new Regex(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    readonly LocalUnityAssetReader _reader;

    public SeerLocalConfigExporter(LocalUnityAssetReader reader)
    {
        _reader = reader;
    }

    public SeerLocalConfigExportResult ExportToPersistentData(bool preserveExistingPetSkin = true)
    {
        return ExportToDirectory(SeerResources.UserDataRoot, preserveExistingPetSkin);
    }

    public SeerLocalConfigExportResult ExportToDirectory(string outputDirectory, bool preserveExistingPetSkin = true)
    {
        var result = new SeerLocalConfigExportResult
        {
            InstallRoot = _reader?.InstallRoot ?? "",
            ConfigVersion = _reader?.GetConfigPackageVersion() ?? "",
            DefaultManifestVersion = _reader?.GetDefaultPackageVersion() ?? "",
            BuildInfo = _reader?.GetBuildInfo() ?? "",
        };

        try
        {
            if (_reader == null || !_reader.IsAvailable)
            {
                result.Error = "No valid local Seer source root was found.";
                return result;
            }

            Directory.CreateDirectory(outputDirectory);

            var monstersRoot = BuildMonstersRoot();
            var monstersJsonPath = Path.Combine(outputDirectory, SeerResources.MonstersFile);
            var petSkinJsonPath = Path.Combine(outputDirectory, SeerResources.PetSkinFile);

            result.MonsterCount = monstersRoot.Monsters.Monster.Count;

            File.WriteAllText(
                monstersJsonPath,
                JsonConvert.SerializeObject(monstersRoot, Formatting.Indented),
                Encoding.UTF8);

            if (TryBuildLocalPetSkinsRoot(out var petSkinsRoot, out var petSkinError))
            {
                File.WriteAllText(
                    petSkinJsonPath,
                    JsonConvert.SerializeObject(petSkinsRoot, Formatting.Indented),
                    Encoding.UTF8);

                result.PetSkinCount = petSkinsRoot.PetSkins?.Skin?.Count ?? 0;
                result.PetSkinSource = GetPetSkinSourceLabel();
            }
            else if (preserveExistingPetSkin && File.Exists(petSkinJsonPath))
            {
                result.PetSkinCount = CountExistingPetSkins(petSkinJsonPath);
                result.PetSkinSource = "preserved-existing";
                result.PetSkinError = petSkinError;
            }
            else
            {
                var emptyPetSkinsRoot = CreateEmptyPetSkinsRoot();
                File.WriteAllText(
                    petSkinJsonPath,
                    JsonConvert.SerializeObject(emptyPetSkinsRoot, Formatting.Indented),
                    Encoding.UTF8);

                result.PetSkinCount = 0;
                result.PetSkinSource = "generated-empty";
                result.PetSkinError = petSkinError;
            }

            result.MonstersJsonPath = monstersJsonPath;
            result.PetSkinJsonPath = petSkinJsonPath;
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.ToString();
            return result;
        }
    }

    public MonstersRoot BuildMonstersRoot()
    {
        var names = ExtractMonsterNames(ReadMonstersBytesOrThrow());
        ValidateMonsterNames(names);

        var monsters = new List<Monster>(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            monsters.Add(new Monster
            {
                ID = i + 1,
                DefName = names[i],
            });
        }

        return new MonstersRoot
        {
            Monsters = new Monsters
            {
                Monster = monsters,
            }
        };
    }

    public PetSkinsRoot BuildPetSkinsRoot()
    {
        var sourceLabel = GetPetSkinSourceLabel();
        var petSkinBytes = ReadPetSkinBytesOrThrow();
        var skins = ParsePetSkinRecords(petSkinBytes);
        ValidatePetSkinCount(sourceLabel, skins.Count);
        if (skins.Count == 0)
        {
            throw new InvalidOperationException("No pet skin entries were parsed from the available source.");
        }

        return new PetSkinsRoot
        {
            PetSkins = new PetSkins
            {
                Skin = skins,
            }
        };
    }

    public static PetSkinsRoot CreateEmptyPetSkinsRoot()
    {
        return new PetSkinsRoot
        {
            PetSkins = new PetSkins
            {
                Skin = new List<Skin>(),
            }
        };
    }

    bool TryBuildLocalPetSkinsRoot(out PetSkinsRoot petSkinsRoot, out string error)
    {
        petSkinsRoot = null;
        error = "";

        try
        {
            petSkinsRoot = BuildPetSkinsRoot();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    byte[] ReadMonstersBytesOrThrow()
    {
        if (_reader != null && _reader.TryReadMonstersBytes(out var data))
        {
            return data;
        }

        throw new FileNotFoundException(
            "Missing monsters.bytes in both rawfile and ConfigPackage bundle sources.",
            _reader?.MonstersBytesPath);
    }

    byte[] ReadPetSkinBytesOrThrow()
    {
        if (_reader == null)
        {
            throw new InvalidOperationException("Local reader was not provided.");
        }

        if (File.Exists(_reader.PetSkinTxtPath))
        {
            return File.ReadAllBytes(_reader.PetSkinTxtPath);
        }

        if (_reader.TryReadPetSkinBytes(out var bundleBytes))
        {
            return bundleBytes;
        }

        throw new FileNotFoundException(
            "Missing pet_skin.txt and ConfigPackage pet_skin.bytes bundle asset.",
            _reader.PetSkinTxtPath);
    }

    string GetPetSkinSourceLabel()
    {
        return _reader != null && File.Exists(_reader.PetSkinTxtPath)
            ? "local-pet_skin.txt"
            : "config-bundle-pet_skin.bytes";
    }

    static void ValidateMonsterNames(List<string> names)
    {
        if (names == null || names.Count < 5000)
        {
            throw new InvalidOperationException(
                $"Parsed monster count looks incorrect: {names?.Count ?? 0}.");
        }

        var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < names.Count; i++)
        {
            uniqueNames.Add(names[i]);
        }

        if (uniqueNames.Count < names.Count * 0.85f)
        {
            throw new InvalidOperationException(
                $"Parsed monster names contain too many duplicates: {uniqueNames.Count}/{names.Count} unique.");
        }
    }

    static void ValidatePetSkinCount(string sourceLabel, int count)
    {
        if (count <= 0)
        {
            throw new InvalidOperationException("No pet skin entries were parsed.");
        }

        if (string.Equals(sourceLabel, "local-pet_skin.txt", StringComparison.Ordinal)
            && count < 500)
        {
            throw new InvalidOperationException(
                $"pet_skin.txt parse looks incomplete: {count} entries.");
        }

        if (string.Equals(sourceLabel, "config-bundle-pet_skin.bytes", StringComparison.Ordinal)
            && count < 500)
        {
            throw new InvalidOperationException(
                $"Config bundle pet_skin bytes parse looks incomplete: {count} entries.");
        }
    }

    static List<Skin> ParsePetSkinRecords(byte[] data)
    {
        return LooksLikeBinaryPetSkinData(data)
            ? ParsePetSkinBinaryRecords(data)
            : ParsePetSkinTextRecords(data);
    }

    static List<Skin> ParsePetSkinTextRecords(byte[] data)
    {
        var skins = new List<Skin>(1024);
        var offset = 0;
        var nextSkinId = 1;

        while (offset + 4 <= data.Length)
        {
            if (!TryReadPetSkinTextRecord(data, ref offset, nextSkinId, out var skin))
            {
                offset++;
                continue;
            }

            skins.Add(skin);
            nextSkinId++;
        }

        return skins;
    }

    static List<Skin> ParsePetSkinBinaryRecords(byte[] data)
    {
        var skins = new List<Skin>(1024);
        var offset = 10;

        while (offset + 14 <= data.Length)
        {
            if (!TryReadPetSkinBinaryRecord(data, ref offset, out var skin))
            {
                break;
            }

            skins.Add(skin);
        }

        return skins;
    }

    static bool TryReadPetSkinTextRecord(byte[] data, ref int offset, int skinId, out Skin skin)
    {
        skin = null;
        var recordStart = offset;

        if (offset + 4 > data.Length)
        {
            return false;
        }

        var monId = data[offset] | (data[offset + 1] << 8);
        if (!HasCrlf(data, offset + 2))
        {
            return false;
        }

        offset += 4;
        if (!TryReadUtf8Line(data, ref offset, out var rawName))
        {
            offset = recordStart;
            return false;
        }

        var name = CleanupPetSkinField(rawName);
        if (string.IsNullOrEmpty(name))
        {
            offset = recordStart;
            return false;
        }

        var go = "";
        var goType = "";

        if (TryPeekUtf8Line(data, offset, out var nextLine))
        {
            var cleanedLine = CleanupPetSkinField(nextLine);
            if (LooksLikeGoPath(cleanedLine))
            {
                TryReadUtf8Line(data, ref offset, out go);
                go = CleanupPetSkinField(go);

                if (TryPeekUtf8Line(data, offset, out var maybeGoType))
                {
                    var cleanedType = CleanupPetSkinField(maybeGoType);
                    if (LooksLikeGoType(cleanedType))
                    {
                        TryReadUtf8Line(data, ref offset, out goType);
                        goType = CleanupPetSkinField(goType);
                    }
                }
            }
        }

        skin = new Skin
        {
            ID = skinId,
            MonID = monId,
            Name = name,
            Type = InferSkinType(go, goType),
            SkinKind = CreateDefaultSkinKinds(),
        };
        return true;
    }

    static bool TryReadPetSkinBinaryRecord(byte[] data, ref int offset, out Skin skin)
    {
        skin = null;
        var recordStart = offset;

        if (offset + 14 > data.Length)
        {
            return false;
        }

        var skinId = ReadInt32LittleEndian(data, offset);
        var monId = ReadInt32LittleEndian(data, offset + 4);
        var nameLength = ReadUInt16LittleEndian(data, offset + 8);
        if (skinId <= 0 || monId <= 0 || nameLength <= 0)
        {
            return false;
        }

        offset += 10;
        if (!TryReadUtf8SizedString(data, ref offset, nameLength, out var rawName))
        {
            offset = recordStart;
            return false;
        }

        var name = CleanupPetSkinField(rawName);
        if (string.IsNullOrEmpty(name))
        {
            offset = recordStart;
            return false;
        }

        if (offset + 4 > data.Length)
        {
            offset = recordStart;
            return false;
        }

        var type = ReadInt32LittleEndian(data, offset);
        offset += 4;

        if (!TryReadUtf8SizedString(data, ref offset, out var go))
        {
            offset = recordStart;
            return false;
        }

        if (!TryReadUtf8SizedString(data, ref offset, out var goType))
        {
            offset = recordStart;
            return false;
        }

        skin = new Skin
        {
            ID = skinId,
            MonID = monId,
            Name = name,
            Type = type,
            SkinKind = CreateDefaultSkinKinds(),
        };
        return true;
    }

    static bool TryReadUtf8Line(byte[] data, ref int offset, out string line)
    {
        line = "";
        if (offset >= data.Length)
        {
            return false;
        }

        var lineEnd = FindNextCrlf(data, offset);
        if (lineEnd < 0)
        {
            return false;
        }

        line = Encoding.UTF8.GetString(data, offset, lineEnd - offset);
        offset = lineEnd + 2;
        return true;
    }

    static bool TryReadUtf8SizedString(byte[] data, ref int offset, out string value)
    {
        value = "";
        if (offset + 2 > data.Length)
        {
            return false;
        }

        var byteLength = ReadUInt16LittleEndian(data, offset);
        offset += 2;
        return TryReadUtf8SizedString(data, ref offset, byteLength, out value);
    }

    static bool TryReadUtf8SizedString(byte[] data, ref int offset, int byteLength, out string value)
    {
        value = "";
        if (byteLength < 0 || offset + byteLength > data.Length)
        {
            return false;
        }

        value = byteLength == 0
            ? ""
            : Encoding.UTF8.GetString(data, offset, byteLength);
        offset += byteLength;
        return true;
    }

    static bool TryPeekUtf8Line(byte[] data, int offset, out string line)
    {
        line = "";
        if (offset >= data.Length)
        {
            return false;
        }

        var lineEnd = FindNextCrlf(data, offset);
        if (lineEnd < 0)
        {
            return false;
        }

        line = Encoding.UTF8.GetString(data, offset, lineEnd - offset);
        return true;
    }

    static int FindNextCrlf(byte[] data, int start)
    {
        for (var i = start; i < data.Length - 1; i++)
        {
            if (data[i] == '\r' && data[i + 1] == '\n')
            {
                return i;
            }
        }

        return -1;
    }

    static bool HasCrlf(byte[] data, int offset)
    {
        return offset + 1 < data.Length
               && data[offset] == '\r'
               && data[offset + 1] == '\n';
    }

    static bool LooksLikeBinaryPetSkinData(byte[] data)
    {
        return data != null
               && data.Length > 32
               && data[10] == 1
               && data[11] == 0
               && data[12] == 0
               && data[13] == 0;
    }

    static string CleanupPetSkinField(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .Replace("\0", "")
            .Replace("\t", "")
            .Replace("\f", "")
            .Replace("\v", "")
            .Trim();
    }

    static bool LooksLikeGoPath(string value)
    {
        return !string.IsNullOrEmpty(value)
               && (value.StartsWith("app/", StringComparison.OrdinalIgnoreCase)
                   || value.StartsWith("ui/", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("/"));
    }

    static bool LooksLikeGoType(string value)
    {
        return string.Equals(value, "module", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "panel", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "window", StringComparison.OrdinalIgnoreCase);
    }

    static int InferSkinType(string go, string goType)
    {
        return string.IsNullOrEmpty(go) && string.IsNullOrEmpty(goType) ? 3 : 0;
    }

    static List<SkinKind> CreateDefaultSkinKinds()
    {
        return new List<SkinKind>
        {
            new SkinKind
            {
                ID = 1,
                LifeTime = 0,
                Type = 2,
            }
        };
    }

    static int CountExistingPetSkins(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return Regex.Matches(json, "\"MonID\"").Count;
        }
        catch
        {
            return 0;
        }
    }

    static List<string> ExtractMonsterNames(byte[] data)
    {
        var names = new List<string>(6000);

        for (var i = 0; i < data.Length - 2; i++)
        {
            var byteLength = data[i] | (data[i + 1] << 8);
            if (byteLength < 1 || byteLength > 48 || i + 2 + byteLength > data.Length)
            {
                continue;
            }

            string candidate;
            try
            {
                candidate = Encoding.UTF8.GetString(data, i + 2, byteLength).Trim();
            }
            catch
            {
                continue;
            }

            if (!IsMonsterNameCandidate(candidate))
            {
                continue;
            }

            names.Add(candidate);
            i += byteLength + 1;
        }

        return names;
    }

    static bool IsMonsterNameCandidate(string candidate)
    {
        return !string.IsNullOrEmpty(candidate)
               && candidate.Length <= 32
               && HasChinesePattern.IsMatch(candidate)
               && AllowedNamePattern.IsMatch(candidate);
    }

    static int ReadInt32LittleEndian(byte[] data, int offset)
    {
        return data[offset]
               | (data[offset + 1] << 8)
               | (data[offset + 2] << 16)
               | (data[offset + 3] << 24);
    }

    static int ReadUInt16LittleEndian(byte[] data, int offset)
    {
        return data[offset] | (data[offset + 1] << 8);
    }
}

public class SeerLocalConfigExportResult
{
    public bool Success;
    public string InstallRoot;
    public string ConfigVersion;
    public string DefaultManifestVersion;
    public string BuildInfo;
    public int MonsterCount;
    public int PetSkinCount;
    public string PetSkinSource;
    public string PetSkinError;
    public string MonstersJsonPath;
    public string PetSkinJsonPath;
    public string Error;
}
