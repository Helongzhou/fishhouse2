using System;
using System.IO;
using Aquarium.UnityAdapter;

internal static class Program
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL " + name);
        Console.WriteLine("PASS " + name);
        checks++;
    }
    static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
    }

    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "aquarium-save-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string player = Path.Combine(root, "PlayerRoot");
            Directory.CreateDirectory(player);
            string neighbor = Path.Combine(root, "PlayerRoot-extra");
            Directory.CreateDirectory(neighbor);
            string linkParent = Path.Combine(root, "link-parent");
            Directory.CreateSymbolicLink(linkParent, player);

            Check(SaveRootGuard.SameOrInside(Path.Combine(player, "child"), player), "child is inside its parent");
            Check(!SaveRootGuard.SameOrInside(Path.Combine(neighbor, "child"), player), "prefix neighbor is outside the boundary");
            Check(SaveRootGuard.SameOrInside(Path.Combine(root, "playerroot", "child"), player), "case variant matches when comparison ignores case");

            string created = SaveRootGuard.CreateFreshDirectory(Path.Combine(neighbor, "fresh-leaf"), player);
            Check(Directory.Exists(created) && SaveRootGuard.SameOrInside(created, neighbor), "fresh leaf is created only under the allowed parent");
            Check(Rejects(() => SaveRootGuard.CreateFreshDirectory(Path.Combine(linkParent, "fresh-leaf"), player)), "parent symlink into the player root is rejected");
            Check(Rejects(() => SaveRootGuard.CreateFreshDirectory(created, player)), "existing save root is rejected and not deleted");
            Check(Directory.Exists(created), "rejected existing root is still present");
            Check(Rejects(() => SaveRootGuard.CreateFreshDirectory(Path.Combine(player, "nested"), player)), "directory inside the forbidden root is rejected");

            string reportParent = Path.Combine(neighbor, "reports");
            Directory.CreateDirectory(reportParent);
            string report = Path.Combine(reportParent, "result.json");
            SaveRootGuard.RejectInside(report, player);
            Check(!File.Exists(report), "report guard does not create the report or its parent");
            Check(Rejects(() => SaveRootGuard.RejectInside(Path.Combine(linkParent, "result.json"), player)), "report path through a player symlink is rejected");

            Console.WriteLine("case_insensitive_volume: not_tested_on_this_linux_vm");
            Console.WriteLine("Application.persistentDataPath: not_called");
            Console.WriteLine("Save root guard checks passed: " + checks);
            return 0;
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
