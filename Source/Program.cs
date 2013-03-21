#region Copyright (c) 2006-2011 LOKAD SAS. All rights reserved

// You must not remove this notice, or any other, from this software.
// This document is the property of LOKAD SAS and must not be disclosed

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Lokad.CodeDsl
{
    class Program
    {
        public const string FileNamePattern = "*.ddd";
        static readonly Mutex AppLock = new Mutex(true, "2DB34E68-F80D-4ED0-975A-409C2CDAF241");

        static readonly ConcurrentDictionary<string, string> States = new ConcurrentDictionary<string, string>();

        static INotify _notify;
        public static NotifyIcon TrayIcon;

        private static IEnumerable<FileSystemWatcher> _notifiers;
        private static string[] _files;

        static void Main(string[] args)
        {
            if (!AppLock.WaitOne(TimeSpan.Zero, true))
            {
                return;
            }

            if (args.Length == 0)
            {
                Console.WriteLine("DSL .NET contracts generator.");
                Console.WriteLine("\tUsage: dsl.exe <path1> .. <pathN> [--tray]");
                Console.WriteLine("\t <path1> .. <pathN> - path to folder with dsl files.");
                Console.WriteLine("\t --tray - options to start tray application.");
            }

            if (args.Any(x => x.Equals("--tray")))
            {
                CreateTrayIconApp();

                _notify = new TrayNotify(TrayIcon);

                var options = args.ToList();
                options.Remove("--tray");

                _files = CreateFileWatchers(options.ToArray());
                StartupRebuild(_files);

                AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
                Application.ThreadExit += ApplicationThreadExit;
                Application.Run(new Empty());
            }
            else
            {
                _notify = new ConsoleNotify();

                _files = CreateFileWatchers(args);
                StartupRebuild(_files);

                Console.ReadLine();
            }
        }

        private static string[] CreateFileWatchers(string [] args)
        {
            var lookupPaths = FigureOutLookupPath(args);

            _notifiers = GetDirectoryWatchers(lookupPaths);

            foreach (var notifier in _notifiers)
            {
                notifier.Changed += NotifierOnChanged;
                notifier.EnableRaisingEvents = true;
            }
            return lookupPaths;
        }

        private static void StartupRebuild(string [] lookupPAth)
        {
            var files = new List<FileInfo>();

            foreach (var path in lookupPAth)
            {
                var info = new DirectoryInfo(path);
                files.AddRange(info.GetFiles(FileNamePattern, SearchOption.AllDirectories));
            }

            var message = string.Format(
                "Lookup path: {1}{0}{1}{1}",
                string.Join("\r\n", lookupPAth), Environment.NewLine);

            if (files.Any())
            {
                message += "Files: \r\n" + String.Join("\r\n", files.Select(x => x.Name));
            }
            else
            {
                message += "Found no files () to watch";
            }

            message += "\r\n\r\nClick icon to see last message.";

             _notify.Notify("Dsl started", message, ToolTipIcon.Info);

            foreach (var fileInfo in files)
            {
                var text = File.ReadAllText(fileInfo.FullName);
                Console.WriteLine("  Watch: {0}", fileInfo.Name);
                Changed(fileInfo.FullName, text);
                try
                {
                    Rebuild(text, fileInfo.FullName);
                }
                catch (Exception ex)
                {
                    _notify.Notify("Parse error - " + fileInfo.Name, ex.Message, ToolTipIcon.Error);
                }
            }
        }

        private static IEnumerable<FileSystemWatcher> GetDirectoryWatchers(IEnumerable<string> lookupPaths)
        {
            return lookupPaths
                .Where(Directory.Exists)
                .Distinct()
                .Select(d => new FileSystemWatcher(d, FileNamePattern) { IncludeSubdirectories = true})
                .ToArray();
        }
        
        static void ApplicationThreadExit(object sender, EventArgs e)
        {
            Close();
        }

        static void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            Close();
        }

        static void TrayIconClick(object sender, EventArgs e)
        {
            // repeat last notification
            _notify.Notify(string.Empty, string.Empty, ToolTipIcon.Info);
        }

        static void Close()
        {
            if (TrayIcon != null)
            {
                TrayIcon.Dispose();
            }

            Application.Exit();
        }

        static string [] FigureOutLookupPath(string[] args)
        {
            if (args.Length > 0)
            {
                return args;
            }

            var current = Directory.GetCurrentDirectory();
            var dir = new DirectoryInfo(current);
            switch (dir.Name)
            {
                case "Release":
                case "Debug":
                    return new[] {"../../.."};
                default:
                    return new[] { dir.FullName };
            }
        }

        static void NotifierOnChanged(object sender, FileSystemEventArgs args)
        {
            if (!File.Exists(args.FullPath)) return;

            try
            {
                var text = File.ReadAllText(args.FullPath);

                if (!Changed(args.FullPath, text))
                    return;


                var message = string.Format("Changed: {1}-{0}", args.Name, args.ChangeType);
                Console.WriteLine(message);
                Rebuild(text, args.FullPath);

                _notify.Notify(args.Name, "File rebuild complete", ToolTipIcon.Info);
                SystemSounds.Beep.Play();
            }
            catch (IOException) {}
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                _notify.Notify("Error - " + args.Name, ex.Message, ToolTipIcon.Error);

                SystemSounds.Exclamation.Play();
            }
        }

        static bool Changed(string path, string value)
        {
            var changed = false;
            States.AddOrUpdate(path, key =>
                {
                    changed = true;
                    return value;
                }, (s, s1) =>
                    {
                        changed = s1 != value;
                        return value;
                    });
            return changed;
        }

        static void Rebuild(string text, string fullPath)
        {
            var dsl = text;
            var generator = new TemplatedGenerator
                {
                    GenerateInterfaceForEntityWithModifiers = "?",
                    TemplateForInterfaceName = "public interface I{0}Aggregate",
                    TemplateForInterfaceMember = "void When({0} {1});",
                    ClassNameTemplate = @"[DataContract(Namespace = {1})]
public partial class {0}",
                    MemberTemplate = "[DataMember(Order = {0})] public {1} {2} {{ get; private set; }}",
                };

  
            File.WriteAllText(Path.ChangeExtension(fullPath, "cs"), GeneratorUtil.Build(dsl, generator));
        }

        private static void CreateTrayIconApp()
        {
            var iconStream = Assembly.GetEntryAssembly().GetManifestResourceStream("Lokad.CodeDsl.code_colored.ico");
            TrayIcon = new NotifyIcon
            {
                Visible = true,
            };

            if (iconStream != null)
                TrayIcon.Icon = new Icon(iconStream);

            TrayIcon.Click += TrayIconClick;
            TrayIcon.ContextMenu = new ContextMenu(
                new[] { new MenuItem("Close", (sender, eventArgs) => Close()) });
        }
    }

    public interface INotify
    {
        void Notify(string message, string title, ToolTipIcon type);
    }

    class TrayNotify : INotify
    {
        static string _lastTitle;
        static string _lastMessage;
        static ToolTipIcon _lastIcon;

        private readonly NotifyIcon _icon;

        public TrayNotify(NotifyIcon icon)
        {
            _icon = icon;
        }

        public void Notify(string message, string title, ToolTipIcon type)
        {
            if (!string.IsNullOrEmpty(message))
            {
                _lastMessage = message;
                _lastTitle = title;
                _lastIcon = type;
            }

            _icon.ShowBalloonTip(10000, _lastTitle, _lastMessage, _lastIcon);
        }
    }

    public class ConsoleNotify : INotify
    {
        public void Notify(string message, string title, ToolTipIcon type)
        {
            Console.WriteLine("{0}\r\n\tMessage: {1}\r\n\t{2}", type.ToString(), title, message);
        }
    }

    public enum NotifyType
    {
        Info,
        Error
    }
}