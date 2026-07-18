using System;
using System.IO;
using Game.Ability.Configuration;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args == null || args.Length < 2)
        {
            Console.Error.WriteLine("Usage: AbilityConfigValidator <excel-dir> <project-root>");
            return 2;
        }

        try
        {
            string excelDir = Path.GetFullPath(args[0]);
            string projectRoot = Path.GetFullPath(args[1]);
            AbilityValidationReport report = AbilityExcelValidationRunner.Validate(excelDir, projectRoot);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                AbilityValidationIssue issue = report.Issues[i];
                string location = issue.Source != null ? " | " + issue.Source : string.Empty;
                string chain = string.IsNullOrEmpty(issue.ReferenceChain) ? string.Empty : " | " + issue.ReferenceChain;
                Console.WriteLine(issue.Severity + " [" + issue.Code + "] " + issue.Message + location + chain);
            }

            Console.WriteLine(
                "Ability validation: " + report.ErrorCount + " error(s), " +
                report.WarningCount + " warning(s), " + report.InfoCount + " info item(s).");
            return report.IsValid ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Ability validation failed to run: " + exception.GetType().Name + ": " + exception.Message);
            Exception inner = exception.InnerException;
            while (inner != null)
            {
                Console.Error.WriteLine("  " + inner.GetType().Name + ": " + inner.Message);
                inner = inner.InnerException;
            }
            return 2;
        }
    }
}
