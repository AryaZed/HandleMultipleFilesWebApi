using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace HandleMultipleFilesWebApi.Service.Files
{
    public class FileProcessingService
    {
        public void UnzipFiles(List<string> filePaths, string zipPassword, string outputDirectory)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    using FileStream fs = File.OpenRead(filePath);
                    using var zf = new ICSharpCode.SharpZipLib.Zip.ZipFile(fs);
                    if (!string.IsNullOrEmpty(zipPassword))
                    {
                        // Set the password used for unzipping
                        zf.Password = zipPassword;
                    }

                    foreach (ZipEntry zipEntry in zf)
                    {
                        if (!zipEntry.IsFile)
                        {
                            // Ignore directories
                            continue;
                        }

                        string entryFileName = zipEntry.Name;
                        // Create full directory path
                        string fullZipToPath = Path.Combine(outputDirectory, entryFileName);
                        string directoryName = Path.GetDirectoryName(fullZipToPath);

                        if (directoryName.Length > 0)
                        {
                            Directory.CreateDirectory(directoryName);
                        }

                        // Unzip file in buffered chunks
                        byte[] buffer = new byte[4096]; // 4K is optimum
                        using (Stream zipStream = zf.GetInputStream(zipEntry))
                        using (FileStream streamWriter = File.Create(fullZipToPath))
                        {
                            StreamUtils.Copy(zipStream, streamWriter, buffer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions (e.g., incorrect password, corrupted ZIP file)
                    Console.WriteLine($"Error unzipping file {filePath}: {ex.Message}");
                }
            }
        }

        public string RepackageFilesIntoZip(List<string> filePaths, string outputZipPath)
        {
            // Create a new ZIP archive at the specified output path
            using (var zipToOpen = new FileStream(outputZipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                foreach (var filePath in filePaths)
                {
                    if (!File.Exists(filePath))
                    {
                        // Handle the missing file (e.g., log a warning)
                        continue;
                    }
                    // The name of the file in the ZIP archive will be the base name of the file being added
                    string entryName = Path.GetFileName(filePath);
                    // Create a new entry in the ZIP archive for this file
                    archive.CreateEntryFromFile(filePath, entryName);
                }
            }

            return outputZipPath;
        }
    }
}
