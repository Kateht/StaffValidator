using System;
using System.IO;
using System.Text.Json;

namespace StaffValidator.Checker.Utils
{
    public static class ReportWriter
    {
        public static void WriteReport(string? outputPath, object report)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(report, options);
                File.WriteAllText(outputPath, json);
                Console.WriteLine($"\n📄 Report written to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed writing report: {ex.Message}");
            }
        }
    }
}
