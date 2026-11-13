using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Aquarium.UnityAdapter
{
    // Canonical path checks for validation sandboxes. Normal play does not call this.
    public static class SaveRootGuard
    {
        public static string Canonicalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Path is required.");
            string full = Path.GetFullPath(path);
            var missing = new List<string>();
            string existing = full;
            while (!string.IsNullOrEmpty(existing) && !Directory.Exists(existing) && !File.Exists(existing))
            {
                string name = Path.GetFileName(existing);
                if (string.IsNullOrEmpty(name)) break;
                missing.Insert(0, name);
                existing = Path.GetDirectoryName(existing);
            }
            if (string.IsNullOrEmpty(existing) || (!Directory.Exists(existing) && !File.Exists(existing)))
                throw new DirectoryNotFoundException("No existing ancestor for " + path);
            string resolved = ResolveExisting(existing);
            for (int i = 0; i < missing.Count; i++) resolved = Path.Combine(resolved, missing[i]);
            return resolved;
        }

        public static string CreateFreshDirectory(string requested, params string[] forbidden)
        {
            string full = Path.GetFullPath(requested);
            string parent = Path.GetDirectoryName(full);
            string leaf = Path.GetFileName(full);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                throw new InvalidOperationException("Save root must be a new child directory.");
            if (!Directory.Exists(parent))
                throw new DirectoryNotFoundException("Parent of the save root must already exist.");
            string candidate = Path.Combine(Canonicalize(parent), leaf);
            if (Directory.Exists(candidate) || File.Exists(candidate))
                throw new InvalidOperationException("Save root already exists. Refusing to delete or reuse it.");
            RejectInside(candidate, forbidden);
            Directory.CreateDirectory(candidate);
            return Canonicalize(candidate);
        }

        public static bool SameOrInside(string candidate, string boundary)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(boundary)) return false;
            string left = Trim(candidate);
            string right = Trim(boundary);
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = right + Path.DirectorySeparatorChar;
            return left.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        public static void RejectInside(string candidate, params string[] boundaries)
        {
            string canonical = Canonicalize(candidate);
            for (int i = 0; i < boundaries.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(boundaries[i])) continue;
                string boundary = Canonicalize(boundaries[i]);
                if (SameOrInside(canonical, boundary))
                    throw new InvalidOperationException("Path is inside a forbidden root.");
            }
        }

        static string ResolveExisting(string existing)
        {
            string root = Path.GetPathRoot(existing);
            string rest = existing.Substring(root.Length);
            string current = root;
            string[] parts = rest.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string actual = FindChild(current, parts[i]);
                string next = Path.Combine(current, actual);
                string link = ReadLink(next);
                if (link != null)
                {
                    if (!Path.IsPathRooted(link)) link = Path.GetFullPath(Path.Combine(current, link));
                    if (seen.Contains(link)) throw new InvalidOperationException("Symlink loop.");
                    seen.Add(link);
                    if (seen.Count > 32) throw new InvalidOperationException("Symlink chain is too deep.");
                    current = ResolveExisting(link);
                }
                else current = next;
            }
            return current;
        }

        static string FindChild(string directory, string name)
        {
            string folded = null;
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                string child = Path.GetFileName(entry);
                if (string.Equals(child, name, StringComparison.Ordinal)) return child;
                if (string.Equals(child, name, StringComparison.OrdinalIgnoreCase)) folded = child;
            }
            if (folded != null) return folded;
            throw new DirectoryNotFoundException("Path component does not exist.");
        }

        static string ReadLink(string path)
        {
            byte[] buffer = new byte[4096];
            int length = readlink(path, buffer, buffer.Length);
            if (length < 0) return null;
            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        static string Trim(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        [DllImport("libc", SetLastError = true)]
        static extern int readlink(string pathname, byte[] buffer, int bufferSize);
    }
}
