﻿namespace RiftRipper.Utility;

public static class FileDialog
{
    public class NoImplementationException : Exception { }

    // METHODS

    public static string OpenFile(string title = "Choose a file", string filter = "")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (PrefersKdialog())
                return KDialog.OpenFile(title, filter);

            return Zenity.OpenFile(title, filter);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Win32Dialog.OpenFile(title, filter);

        throw new NoImplementationException();
    }

    public static string SaveFile(string title = "Enter the name of the file to save to", string filter = "")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (PrefersKdialog())
                return KDialog.SaveFile(title);

            return Zenity.SaveFile(title);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Win32Dialog.SaveFile(title, filter);

        throw new NoImplementationException();
    }

    public static string OpenFolder(string title = "Choose a folder")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (PrefersKdialog())
                return KDialog.OpenFolder(title);

            return Zenity.OpenFolder(title);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Win32Dialog.OpenFolder(title);

        throw new NoImplementationException();
    }

    private static bool PrefersKdialog()
    {
        string desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        string xdgCurrentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");

        bool probablyKde = (desktopSession != null && desktopSession.ToLower().Equals("kde")) ||
                           (xdgCurrentDesktop != null && xdgCurrentDesktop.ToLower().Equals("kde"));

        bool hasKdialogBinary = ExecutableInPath("kdialog");
        bool hasZenityBinary = ExecutableInPath("zenity");

        if (hasKdialogBinary && (probablyKde || !hasZenityBinary))
            return true;

        if (hasZenityBinary)
            return false;

        throw new NoImplementationException();
    }

    private static bool ExecutableInPath(string fileName)
    {
        if (File.Exists(fileName))
            return true;

        String? values = Environment.GetEnvironmentVariable("PATH");

        if (values != null)
        {
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return true;
            }
        }

        return false;
    }

    private static string RunProcess(string filename, List<string> args)
    {
        Process process = new Process();
        process.StartInfo.FileName = filename;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        foreach (string arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();

        return output.Replace("\n", "").Replace("\r", "");
    }




    // CLASSES



    private static class KDialog
    {
        private static string RunKDialog(List<string>? args = null, Dictionary<string, string>? kwargs = null)
        {
            List<string> zArgs = new List<string>();
            if (args != null)
                foreach (string arg in args)
                    zArgs.Add("--" + arg);

            if (kwargs != null)
                foreach (var entry in kwargs)
                {
                    zArgs.Add("--" + entry.Key);
                    zArgs.Add(entry.Value);
                }

            return RunProcess("kdialog", zArgs);
        }

        public static string OpenFile(string title, string filter = "")
        {
            return RunKDialog(new List<string>() { "getopenfilename" },
                new Dictionary<string, string>() { { "title", title } });
        }

        public static List<string> OpenMultipleFiles(string title)
        {
            var result = RunKDialog(new List<string>() { "getopenfilename", "multiple" },
                new Dictionary<string, string>() { { "title", title } });

            return result.Split(' ').ToList();
        }

        public static string SaveFile(string title)
        {
            return RunKDialog(new List<string>() { "getsavefilename" },
                new Dictionary<string, string>() { { "title", title } });
        }

        public static string OpenFolder(string title)
        {
            return RunKDialog(new List<string>() { "getexistingdirectory" },
                new Dictionary<string, string>() { { "title", title } });
        }
    }

    private static class Zenity
    {
        private static string RunZenity(List<string>? args = null, Dictionary<string, string>? kwargs = null)
        {
            List<string> zArgs = new List<string>();
            if (args != null)
                foreach (string arg in args)
                    zArgs.Add("--" + arg);

            if (kwargs != null)
                foreach (var entry in kwargs)
                    zArgs.Add("--" + entry.Key + "=" + entry.Value);

            return RunProcess("zenity", zArgs);
        }

        public static string OpenFile(string title, string filter = "")
        {
            return RunZenity(new List<string>() { "file-selection" },
                new Dictionary<string, string>() { { "title", title } });
        }

        public static List<string> OpenMultipleFiles(string title)
        {
            var result = RunZenity(new List<string>() { "file-selection", "multiple" },
                new Dictionary<string, string>() { { "title", title } });

            return result.Split("|").ToList();
        }

        public static string SaveFile(string title)
        {
            return RunZenity(new List<string>() { "file-selection", "save", "confirm-overwrite" },
                new Dictionary<string, string>() { { "title", title } });
        }

        public static string OpenFolder(string title)
        {
            return RunZenity(new List<string>() { "file-selection", "directory" },
                new Dictionary<string, string>() { { "title", title } });
        }
    }

    private static class Win32Dialog
    {
        private static string BuildFilterList(string filter)
        {
            var wildcardFound = false;
            var filters = new List<string>();
            foreach (string group in filter.Split('|'))
            {
                var extensions = new List<string>();
                foreach (string f in group.Split(';'))
                {
                    if (f == ".*") wildcardFound = true;
                    extensions.Add($"*{f}");
                }
                var extString = String.Join(';', extensions);
                filters.Add($"Supported files ({extString})|{extString}");
            }

            if (!wildcardFound)
            {
                filters.Add("All files (*.*)|*.*");
            }
            return String.Join('|', filters);
        }

        public static string OpenFile(string title, string filter = "")
        {
#if WIN
                var dialog = new System.Windows.Forms.OpenFileDialog();
                dialog.Title = title;
                dialog.Filter = BuildFilterList(filter);
                dialog.ShowDialog();
                return dialog.FileName;
#else
            throw new NoImplementationException();
#endif
        }

        public static List<string> OpenMultipleFiles(string title)
        {
#if WIN
                throw new NoImplementationException();
#else
            throw new NoImplementationException();
#endif
        }

        public static string SaveFile(string title, string filter = "")
        {
#if WIN
                var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Title = title;
                dialog.Filter = BuildFilterList(filter);
                dialog.ShowDialog();
                return dialog.FileName;
#else
            throw new NoImplementationException();
#endif
        }

        public static string OpenFolder(string title)
        {
#if WIN
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = title;
                dialog.UseDescriptionForTitle = true;
                dialog.ShowDialog();
                return dialog.SelectedPath;
#else
            throw new NoImplementationException();
#endif
        }
    }
}
