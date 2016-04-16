﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using log4net;

namespace Octopus.Cli.Util
{
    public class OctopusPhysicalFileSystem : IOctopusFileSystem
    {
        readonly ILog log;

        public OctopusPhysicalFileSystem(ILog log)
        {
            this.log = log;
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool DirectoryIsEmpty(string path)
        {
            try
            {
                return !Directory.GetFileSystemEntries(path).Any();
            }
            catch (Exception)
            {
                log.Error("Failed to list directory contents");
                return false;
            }
        }

        public void DeleteFile(string path)
        {
            DeleteFile(path, null);
        }

        public void DeleteFile(string path, DeletionOptions options)
        {
            options = options ?? DeletionOptions.TryThreeTimes;

            if (string.IsNullOrWhiteSpace(path))
                return;

            var firstAttemptFailed = false;
            for (var i = 0; i < options.RetryAttempts; i++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        if (firstAttemptFailed)
                        {
                            File.SetAttributes(path, FileAttributes.Normal);
                        }
                        File.Delete(path);
                        return;
                    }
                }
                catch
                {
                    Thread.Sleep(options.SleepBetweenAttemptsMilliseconds);
                    firstAttemptFailed = true;
                    if (i == options.RetryAttempts - 1)
                    {
                        if (options.ThrowOnFailure)
                        {
                            throw;
                        }

                        break;
                    }
                }
            }
        }

        public void DeleteDirectory(string path)
        {
            Directory.Delete(path, true);
        }

        public void DeleteDirectory(string path, DeletionOptions options)
        {
            options = options ?? DeletionOptions.TryThreeTimes;

            if (string.IsNullOrWhiteSpace(path))
                return;

            for (var i = 0; i < options.RetryAttempts; i++)
            {
                try
                {
                    var dir = new DirectoryInfo(path);
                    if (dir.Exists)
                    {
                        dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;
                        dir.Delete(true);
                        return;
                    }
                }
                catch
                {
                    Thread.Sleep(options.SleepBetweenAttemptsMilliseconds);

                    if (i == options.RetryAttempts - 1)
                    {
                        if (options.ThrowOnFailure)
                        {
                            throw;
                        }
                        break;
                    }
                }
            }
        }

        public IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns)
        {
            return searchPatterns.Length == 0
                ? Directory.EnumerateFiles(parentDirectoryPath, "*", SearchOption.TopDirectoryOnly)
                : searchPatterns.SelectMany(pattern => Directory.EnumerateFiles(parentDirectoryPath, pattern, SearchOption.TopDirectoryOnly));
        }

        public IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns)
        {
            if (!DirectoryExists(parentDirectoryPath))
                return Enumerable.Empty<string>();

            return searchPatterns.Length == 0
                ? Directory.EnumerateFiles(parentDirectoryPath, "*", SearchOption.AllDirectories)
                : searchPatterns.SelectMany(pattern => Directory.EnumerateFiles(parentDirectoryPath, pattern, SearchOption.AllDirectories));
        }

        public IEnumerable<string> EnumerateDirectories(string parentDirectoryPath)
        {
            if (!DirectoryExists(parentDirectoryPath))
                return Enumerable.Empty<string>();

            return Directory.EnumerateDirectories(parentDirectoryPath);
        }

        public IEnumerable<string> EnumerateDirectoriesRecursively(string parentDirectoryPath)
        {
            if (!DirectoryExists(parentDirectoryPath))
                return Enumerable.Empty<string>();

            return Directory.EnumerateDirectories(parentDirectoryPath, "*", SearchOption.AllDirectories);
        }

        public long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        public string ReadFile(string path)
        {
            return File.ReadAllText(path);
        }

        public void AppendToFile(string path, string contents)
        {
            File.AppendAllText(path, contents);
        }

        public void OverwriteFile(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public Stream OpenFile(string path, FileAccess access, FileShare share)
        {
            return OpenFile(path, FileMode.OpenOrCreate, access, share);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            try
            {
                return new FileStream(path, mode, access, share);
            }
            catch (UnauthorizedAccessException)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists && (fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    // Throw a more helpful message than .NET's
                    // System.UnauthorizedAccessException: Access to the path ... is denied.
                    throw new IOException(path + " is a directory not a file");
                }
                throw;
            }
        }

        public Stream CreateTemporaryFile(string extension, out string path)
        {
            if (!extension.StartsWith("."))
                extension = "." + extension;

            path = Path.Combine(GetTempBasePath(), Guid.NewGuid() + extension);

            return OpenFile(path, FileAccess.ReadWrite, FileShare.Read);
        }

        static string GetTempBasePath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly().GetName().Name : "Octopus");
            path = Path.Combine(path, "Temp");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        public void OverwriteAndDelete(string originalFile, string temporaryReplacement)
        {
            var backup = originalFile + ".backup" + Guid.NewGuid();

            if (!File.Exists(originalFile))
                File.Copy(temporaryReplacement, originalFile, true);
            else
                File.Replace(temporaryReplacement, originalFile, backup);

            File.Delete(temporaryReplacement);
            if (File.Exists(backup))
                File.Delete(backup);
        }

        public void WriteAllBytes(string filePath, byte[] data)
        {
            File.WriteAllBytes(filePath, data);
        }

        public string RemoveInvalidFileNameChars(string path)
        {
            var invalidChars = Path.GetInvalidPathChars();
            path = new string(path.Where(c => !invalidChars.Contains(c)).ToArray());
            return path;
        }

        public void MoveFile(string sourceFile, string destinationFile)
        {
            File.Move(sourceFile, destinationFile);
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
            if (!DirectoryExists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        // ReSharper disable AssignNullToNotNullAttribute

        public void CopyDirectory(string sourceDirectory, string targetDirectory, int overwriteFileRetryAttempts = 3)
        {
            CopyDirectory(sourceDirectory, targetDirectory, CancellationToken.None, overwriteFileRetryAttempts);
        }

        public void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel, int overwriteFileRetryAttempts = 3)
        {
            if (!DirectoryExists(sourceDirectory))
                return;

            if (!DirectoryExists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var files = Directory.GetFiles(sourceDirectory, "*");
            foreach (var sourceFile in files)
            {
                cancel.ThrowIfCancellationRequested();

                var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
                CopyFile(sourceFile, targetFile, overwriteFileRetryAttempts);
            }

            foreach (var childSourceDirectory in Directory.GetDirectories(sourceDirectory))
            {
                var name = Path.GetFileName(childSourceDirectory);
                var childTargetDirectory = Path.Combine(targetDirectory, name);
                CopyDirectory(childSourceDirectory, childTargetDirectory, cancel, overwriteFileRetryAttempts);
            }
        }

        public ReplaceStatus CopyFile(string sourceFile, string targetFile, int overwriteFileRetryAttempts = 3)
        {
            var result = ReplaceStatus.Updated;
            for (var i = 0; i < overwriteFileRetryAttempts; i++)
            {
                try
                {
                    FileInfo fi = new FileInfo(targetFile);
                    if (fi.Exists)
                    {
                        if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly;
                        }
                    }
                    else
                    {
                        result = ReplaceStatus.Created;
                    }
                    File.Copy(sourceFile, targetFile, true);
                    return result;
                }
                catch
                {
                    if (i == overwriteFileRetryAttempts - 1)
                    {
                        throw;
                    }
                    Thread.Sleep(1000 + (2000 * i));
                }
            }
            throw new Exception("Internal error, cannot get here");
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath)
        {
            EnsureDiskHasEnoughFreeSpace(directoryPath, 500*1024*1024);
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            ulong freeBytesAvailable;
            ulong totalNumberOfBytes;
            ulong totalNumberOfFreeBytes;

            var success = GetDiskFreeSpaceEx(directoryPath, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes);
            if (!success)
                return;

            // Always make sure at least 500MB are available regardless of what we need 
            var required = requiredSpaceInBytes < 0 ? 0 : (ulong)requiredSpaceInBytes;
            required = Math.Max(required, 500L*1024*1024);
            if (totalNumberOfFreeBytes < required)
            {
                throw new IOException(string.Format("The drive containing the directory '{0}' on machine '{1}' does not have enough free disk space available for this operation to proceed. The disk only has {2} available; please free up at least {3}.", directoryPath, Environment.MachineName, totalNumberOfFreeBytes.ToFileSizeString(), required.ToFileSizeString()));
            }
        }

        public string GetFullPath(string relativeOrAbsoluteFilePath)
        {
            if (!Path.IsPathRooted(relativeOrAbsoluteFilePath))
            {
                relativeOrAbsoluteFilePath = Path.Combine(Environment.CurrentDirectory, relativeOrAbsoluteFilePath);
            }

            relativeOrAbsoluteFilePath = Path.GetFullPath(relativeOrAbsoluteFilePath);
            return relativeOrAbsoluteFilePath;
        }

        /// <summary>
        /// Creates, updates or skips a file based on a file content comparison
        /// </summary>
        /// <remarks>
        /// Useful for cases where you do not want a file's timestamp to change when overwriting
        /// it with identical contents or you want clearer logging as to what changed.
        /// </remarks>
        public ReplaceStatus Replace(string oldFilePath, Stream newStream, int overwriteFileRetryAttempts = 3)
        {
            for (var i = 0; i < overwriteFileRetryAttempts; i++)
            {
                try
                {
                    var oldDirectory = Path.GetDirectoryName(oldFilePath);
                    if (!DirectoryExists(Path.GetDirectoryName(oldFilePath)))
                    {
                        Directory.CreateDirectory(oldDirectory);
                    }

                    var fi = new FileInfo(oldFilePath);
                    if (!fi.Exists)
                    {
                        // Getting unauthorized exception - file is readonly
                        using (var fileStream = File.Create(oldFilePath))
                        {
                            newStream.CopyTo(fileStream);
                            fileStream.Flush();
                        }
                        return ReplaceStatus.Created;
                    }

                    if ((fi.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        // Throw a more helpful message than .NET's
                        // System.UnauthorizedAccessException: Access to the path ... is denied.
                        throw new IOException("Cannot overwrite a directory with a file " + oldFilePath);
                    }

                    if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly;
                    }

                    bool equal;
                    using (var oldStream = File.OpenRead(oldFilePath))
                    {
                        equal = EqualHash(oldStream, newStream);
                    }

                    if (equal)
                    {
                        return ReplaceStatus.Unchanged;
                    }
                    newStream.Seek(0, SeekOrigin.Begin);
                    using (var oldStream = File.Create(oldFilePath))
                    {
                        newStream.CopyTo(oldStream);
                        newStream.Flush();
                    }
                    return ReplaceStatus.Updated;
                }
                catch
                {
                    if (i == overwriteFileRetryAttempts - 1)
                    {
                        throw;
                    }
                    Thread.Sleep(1000 + (2000*i));
                }
            }
            throw new Exception("Internal error, cannot get here");
        }

        public string ReadAllText(string scriptFile)
        {
            return File.ReadAllText(scriptFile);
        }

        public bool EqualHash(Stream first, Stream second)
        {
            first.Seek(0, SeekOrigin.Begin);
            second.Seek(0, SeekOrigin.Begin);

            var firstHash = MD5.Create().ComputeHash(first);
            var secondHash = MD5.Create().ComputeHash(second);

            for (var i = 0; i < firstHash.Length; i++)
            {
                if (firstHash[i] != secondHash[i])
                    return false;
            }
            return true;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);
    }
}