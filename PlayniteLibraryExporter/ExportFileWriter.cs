using System;
using System.IO;
using System.Text;

namespace PlayniteLibraryExporter
{
    public class ExportFileWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public string WriteAtomic(string outputDirectory, string baseName, ExportFormat format, string content)
        {
            Directory.CreateDirectory(outputDirectory);

            var targetPath = Path.Combine(outputDirectory, baseName + format.ToExtension());
            var tempPath = Path.Combine(
                outputDirectory,
                $"{baseName}.{format.ToId()}.{Guid.NewGuid():N}.tmp");

            File.WriteAllText(tempPath, content ?? string.Empty, Utf8NoBom);

            try
            {
                if (File.Exists(targetPath))
                {
                    File.Replace(tempPath, targetPath, null, true);
                }
                else
                {
                    File.Move(tempPath, targetPath);
                }
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }

            return targetPath;
        }
    }
}
