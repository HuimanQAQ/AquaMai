using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AMDaemon;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Core.Helpers;

/**
 * LocalAssets is not necessarily a single directory in the game root.
 * Data packages and option packages may ship their own LocalAssets as well:
 *
 *   <StreamingAssets>/A###/LocalAssets                (e.g. A500/LocalAssets)
 *   <AppImage.OptionMountRootPath>/N###/LocalAssets   (option packages mounted by amdaemon)
 *
 * Priority, from lowest to highest:
 *
 *   <game root>/LocalAssets  <  <StreamingAssets>/A###/LocalAssets  <  <OptionMountRoot>/N###/LocalAssets
 *
 * Callers get the directories in that order, so loading them one by one makes the later ones
 * override the earlier ones.
 */
public static class LocalAssets
{
    private const string LocalAssetsDirName = "LocalAssets";

    /**
     * All existing LocalAssets roots, ordered from the lowest priority to the highest one.
     */
    public static List<string> GetRoots()
    {
        var roots = new List<string>();

        AddUnique(roots, FileSystem.ResolvePath(LocalAssetsDirName));

        foreach (var dataDir in GetDataDirs())
        {
            AddUnique(roots, Path.Combine(dataDir, LocalAssetsDirName));
        }

        return roots;
    }

    /**
     * Resolve a configured LocalAssets path into the list of directories to read, in priority order.
     *
     * - Exactly "LocalAssets", or a path starting with "LocalAssets/" (e.g. the default
     *   "LocalAssets/Camouflages"): the same sub directory under every LocalAssets root above,
     *   so package provided content is picked up as well. Paths like "LocalAssetsExtra/xxx"
     *   do not match and are treated as a normal relative path.
     * - Any other relative path: resolved from the game root, used as is.
     * - An absolute path: used as is, no LocalAssets traversal.
     */
    public static List<string> ResolveAll(string configuredPath)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(configuredPath)) return result;

        var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());

        if (Path.IsPathRooted(expanded))
        {
            // Keep the separator when the path is a file system root (e.g. "C:\"), otherwise
            // trimming it would turn it into a drive relative path ("C:").
            var root = Path.GetPathRoot(expanded) ?? string.Empty;
            var absolute = expanded.Length > root.Length ? expanded.TrimEnd('\\', '/') : root;
            AddUnique(result, absolute);
            return result;
        }

        var normalized = expanded.Replace('\\', '/').Trim('/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        if (normalized.Equals(LocalAssetsDirName, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var root in GetRoots()) AddUnique(result, root);
            return result;
        }

        if (normalized.StartsWith(LocalAssetsDirName + "/", StringComparison.OrdinalIgnoreCase))
        {
            var subDir = normalized.Substring(LocalAssetsDirName.Length + 1);
            foreach (var root in GetRoots()) AddUnique(result, Path.Combine(root, subDir));
            return result;
        }

        AddUnique(result, FileSystem.ResolvePath(expanded));
        return result;
    }

    /**
     * Data package directories (A###) followed by option package directories (N###).
     *
     * Those are the two naming patterns DataManager.GetDirs() looks for (A??? under
     * StreamingAssets, N??? under the option mount root); directories with other names are not
     * game data directories and are intentionally ignored here.
     */
    private static IEnumerable<string> GetDataDirs()
    {
        var dirs = new List<string>();

        try
        {
            var streamingAssets = Application.streamingAssetsPath;
            if (Directory.Exists(streamingAssets))
            {
                dirs.AddRange(
                    Directory.GetDirectories(streamingAssets, "A???", SearchOption.TopDirectoryOnly)
                        .OrderBy(it => it, StringComparer.OrdinalIgnoreCase));
            }
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[LocalAssets] Failed to enumerate data packages: {e.Message}");
        }

        try
        {
            // Same source as DataManager.GetDirs (it is called with AppImage.OptionMountRootPath),
            // so patches replacing the option mount root, e.g. OptionLoadFix, apply here as well.
            var optionRoot = AppImage.OptionMountRootPath;
            if (!string.IsNullOrEmpty(optionRoot) && Directory.Exists(optionRoot))
            {
                dirs.AddRange(
                    Directory.GetDirectories(optionRoot, "N???", SearchOption.TopDirectoryOnly)
                        .OrderBy(it => it, StringComparer.OrdinalIgnoreCase));
            }
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[LocalAssets] Failed to enumerate option packages: {e.Message}");
        }

        return dirs;
    }

    private static void AddUnique(List<string> list, string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        if (list.Any(it => string.Equals(it, dir, StringComparison.OrdinalIgnoreCase))) return;
        list.Add(dir);
    }
}
