using System;
using System.IO;
using System.Text;

namespace Aquarium.Core.Session
{
    // Serialization belongs to the host. Validation must restore the complete candidate session.
    public sealed class AtomicSaveStore
    {
        private readonly string path;
        private readonly Func<string, bool> valid;
        private readonly Action<string> fault;
        public AtomicSaveStore(string path, Func<string, bool> semanticValidator, Action<string> faultInjection = null)
        {
            if (string.IsNullOrEmpty(path) || semanticValidator == null) throw new ArgumentException("A save path and semantic validator are required.");
            this.path = path; valid = semanticValidator; fault = faultInjection;
        }
        private void Check(string point) { if (fault != null) fault(point); }
        private bool ValidFile(string candidate)
        { try { return File.Exists(candidate) && valid(File.ReadAllText(candidate, Encoding.UTF8)); } catch { return false; } }
        public void Save(string payload)
        {
            if (!valid(payload)) throw new ArgumentException("Save candidate failed semantic validation.");
            string directory = Path.GetDirectoryName(path); if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            try
            {
                Check("before_temp_write");
                using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(payload); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                Check("after_temp_flush");
                if (!ValidFile(temporary)) throw new IOException("Written save did not pass semantic validation.");
                // Retain only verified historical saves. A broken primary never displaces a good backup.
                if (ValidFile(path))
                {
                    for (int i = 3; i >= 2; i--)
                    {
                        string older = path + ".bak" + (i - 1);
                        if (ValidFile(older)) File.Copy(older, path + ".bak" + i, true);
                    }
                    File.Copy(path, path + ".bak1", true);
                }
                Check("before_commit");
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public string Load(out string recoveredFrom)
        {
            for (int i = 0; i <= 3; i++)
            {
                string candidate = i == 0 ? path : path + ".bak" + i;
                if (!ValidFile(candidate)) continue;
                recoveredFrom = candidate; return File.ReadAllText(candidate, Encoding.UTF8);
            }
            throw new InvalidDataException("No semantically valid save or backup was found.");
        }
    }
}
