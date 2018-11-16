//*******************************************************************************
//	Notice:		Copyright (c) Sercurity Perspectives Inc. All Rights Reserved
//	Website:	https://www.securityperspectives.com/
//	Project:	tool_github-repo-validator
//	File:		Program.cs
//	Author:		Joshua Vinters
//	Date:		2018/11/15
//	Time:		7:17 PM
//*******************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tool_GitHubFileScanner
{
    class Program
    {
        private static string separator = "========================================================================================";
        private static string prefix = "#\t";
        private static string allowedRegexMatched = @"([a-z0-9\-\\/\s:_-]+)";
        private static string noScannedItemsError = "Scan command has not been run. Please scan a directory first";
        private static List<ScannedItem> scannedItems = new List<ScannedItem>();

        /// <summary>
        /// Represents a scanned item in the search directory
        /// </summary>
        struct ScannedItem
        {
            public enum Type { File, Directory }

            public string Path;
            public Type ItemType;

            public ScannedItem(string path, Type type)
            {
                this.Path = path;
                this.ItemType = type;
            }
        }

        /// <summary>
        /// Outputs the console instructions
        /// </summary>
        static void OutputInstructions()
        {
            Console.WriteLine(separator);
            Console.WriteLine(prefix + "Use /scan <directory> to start a scan.");
            Console.WriteLine(prefix + "Use /init to initialize all empty directory with a README.md file.");
            Console.WriteLine(prefix + "Use /open to open file explorer to each file over 100mb");
            Console.WriteLine(separator);
        }

        static void Main(string[] args)
        {
            //Output the initial instructions for using the program
            OutputInstructions();
            //
            while (true)
            {
                //Read in the current line
                string line = Console.ReadLine();
                //
                if (!string.IsNullOrEmpty(line))
                {
                    //Match the line for a /scan {allowed characters}
                    Match scanMatch = Regex.Match(line, @"/scan " + allowedRegexMatched, RegexOptions.IgnoreCase);
                    if (scanMatch.Success && scanMatch.Groups.Count > 1)
                    {
                        ProcessScanCommand(scanMatch.Groups[1].Value);
                        OutputInstructions();
                        continue;
                    }

                    Match initMatch = Regex.Match(line, @"/init", RegexOptions.IgnoreCase);
                    if (initMatch.Success)
                    {
                        ProcessInitCommand();
                        OutputInstructions();
                        continue;
                    }

                    Match openMatch = Regex.Match(line, @"/open", RegexOptions.IgnoreCase);
                    if (openMatch.Success)
                    {
                        ProcessOpenCommand();
                        OutputInstructions();
                        continue;
                    }

                }
                Console.WriteLine("Error\n");
            }
        }

        /// <summary>
        /// Processes the scan command
        /// </summary>
        /// <param name="path"></param>
        static void ProcessScanCommand(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Console.Clear();
                if (!IsValidGitRepoPath(path))
                {
                    Console.Write("Not a valid git repo. Please include a \".git\" folder\n");
                    return ;
                }

                Console.Write("Scanning...");

                //Scan the path and get all the scanned items
                scannedItems = ScanPath(path);

                //Filter out the directories and files from all scanned items
                var filteredDirectories = FilterByType(scannedItems, ScannedItem.Type.Directory);
                var filteredFiles = FilterByType(scannedItems, ScannedItem.Type.File);

                //Filter out the empty directories and and files over the 100mb
                var emptyDirs = FilterEmptyDirectories(filteredDirectories);
                var filesOver100Mb = FilterFilesOver100Mb(filteredFiles);

                //Clear the console and output the instructions again
                Console.Clear();
                OutputInstructions();

                //Output the scanned information

                Console.Write("Total Directories            :\t" + filteredDirectories.Count + "\n");
                Console.Write("Total Files                  :\t" + filteredFiles.Count + "\n\n");

                Console.Write("Total Empty Directories      :\t" + emptyDirs.Count + "\n\n");
                emptyDirs.ForEach(x => Console.Write($"\t{x.Path}\n"));
                Console.Write("\n");
                Console.Write("Total Files Over 100mb       :\t" + filesOver100Mb.Count + "\n\n");
                filesOver100Mb.ForEach(x => Console.Write($"\t{x.Path}\n"));
                Console.Write("\n");
                //
                return;
            }
            Console.Write($"{path} is not a valid directory!\n");
        }

        /// <summary>
        /// Processes the init command
        /// </summary>
        static void ProcessInitCommand()
        {
            if (scannedItems.Count > 0)
            {
                var emptyDirs = FilterEmptyDirectories(scannedItems);
                while (true)
                {
                    Console.WriteLine($"{emptyDirs.Count} file(s) will initialized with a README.md file. Are you sure? Y/N");
                    var info = Console.ReadKey(true);

                    if (info.Key == ConsoleKey.Y)
                    {
                        foreach (var item in emptyDirs)
                        {
                            InitializeDirectoryWithReadme(item.Path);
                        }
                        break;
                    }

                    if (info.Key == ConsoleKey.N)
                    {
                        break;
                    }
                }

                return;
            }

            Console.WriteLine(noScannedItemsError);
        }

        /// <summary>
        /// Processes the open command
        /// </summary>
        static void ProcessOpenCommand()
        {
            if (scannedItems.Count > 0)
            {
                var filesOver100Mb = FilterFilesOver100Mb(scannedItems);
                while (true)
                {
                    Console.WriteLine($"{filesOver100Mb.Count} file locations will be opened. Are you sure? Y/N");
                    var info = Console.ReadKey(true);

                    if (info.Key == ConsoleKey.Y)
                    {
                        foreach (var item in filesOver100Mb)
                        {
                            if (File.Exists(item.Path))
                            {
                                Process.Start("explorer.exe", Path.GetDirectoryName(item.Path));
                            }
                        }
                        break;
                    }

                    if (info.Key == ConsoleKey.N)
                    {
                        break;
                    }
                }

                return;
            }

            Console.WriteLine(noScannedItemsError);
        }

        /// <summary>
        /// Recursively scans the root path and converts them into a "ScannedItem" object.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static List<ScannedItem> ScanPath(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    //Running count of directories
                    var items = new List<ScannedItem>();

                    //Root directories for current path
                    var rootDirs = Directory.GetDirectories(path);
                    var rootFiles = Directory.GetFiles(path);

                    //Add directories count to running count
                    rootDirs.ToList()
                        .Where(x => (!new DirectoryInfo(x).Attributes.HasFlag(FileAttributes.Hidden)))
                        .ToList()
                        .ForEach(y => items.Add(new ScannedItem { Path = y, ItemType = ScannedItem.Type.Directory }));

                    rootFiles.ToList().ForEach(x => items.Add(new ScannedItem { Path = x, ItemType = ScannedItem.Type.File }));

                    foreach (var item in new List<ScannedItem>(items))
                    {
                        if(item.ItemType == ScannedItem.Type.Directory)
                            items.AddRange(ScanPath(item.Path));
                    }

                    return items;
                }

                return new List<ScannedItem>();
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" + e);
                return new List<ScannedItem>();
            }
        }

        #region Helpers

        /// <summary>
        /// Filters a list of scanned items by checking for completely empty directories
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        static List<ScannedItem> FilterEmptyDirectories(List<ScannedItem> items)
        {
            List<ScannedItem> ret = new List<ScannedItem>();
            foreach (var item in items)
            {
                if (item.ItemType == ScannedItem.Type.Directory)
                {
                    var files = Directory.GetFiles(item.Path);
                    var dirs = Directory.GetDirectories(item.Path);
                    if (files.Length == 0 && dirs.Length == 0) ret.Add(item);
                }
            }
            return ret;
        }

        /// <summary>
        /// Fillers a list of scanned items for files over 100Mb
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        static List<ScannedItem> FilterFilesOver100Mb(List<ScannedItem> items)
        {
            List<ScannedItem> ret = new List<ScannedItem>();
            foreach (var item in items)
            {
                if (item.ItemType == ScannedItem.Type.File)
                {
                    var file = new FileInfo(item.Path);
                    var mb = ConvertBytesToMegabytes(file.Length);
                    if (mb >= 100) ret.Add(item);
                }
            }
            return ret;
        }

        /// <summary>
        /// Filter a list of scanned items by a item type
        /// </summary>
        /// <param name="list"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        static List<ScannedItem> FilterByType(List<ScannedItem> list, ScannedItem.Type type)
        {
            return list.Where(x => x.ItemType == type).ToList();
        }

        /// <summary>
        /// Convert bytes to megabytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        static double ConvertBytesToMegabytes(long bytes)
        {
            return (bytes / 1000f) / 1000f;
        }

        /// <summary>
        /// Checks if the specified path has a .git folder
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static bool IsValidGitRepoPath(string path)
        {
            if (Directory.Exists(path))
            {
                return Directory.GetDirectories(path).ToList().Where(x => x.Contains(".git")).ToList().Count > 0;
            }
            return false;
        }

        /// <summary>
        /// Creates a new readme file for a directory
        /// </summary>
        /// <param name="path"></param>
        static void InitializeDirectoryWithReadme(string path)
        {
            if (Directory.Exists(path))
            {
                string fileName = "README.md";
                string contents = "# Empty-Readme" +
                                  "This is an empty directory. Please Describe why this directory is needed.";

               File.WriteAllText(Path.Combine(path, fileName), contents);

               Console.WriteLine(prefix + "Adding README.md to directory: " + path);
            }
        }

        #endregion
    }
}
