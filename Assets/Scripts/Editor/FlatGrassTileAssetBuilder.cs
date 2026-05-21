#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class LubanBeanXmlUpdater
{
    private const string ConfigRoot = @"D:\Cube\Config";
    private const string ExcelDir = @"D:\Cube\Assets\Data\Excel";
    private const string XmlDir = @"D:\Cube\Config\Defines";
    private const string GenAllBat = @"D:\Cube\Config\gen_all.bat";

    [MenuItem("Tools/Luban/Update Bean Xml And Gen All")]
    public static void UpdateBeanXmlAndGenAll()
    {
        try
        {
            int updateCount = UpdateAllXml();
            Debug.Log("Luban bean xml update finished. Updated xml count: " + updateCount);

            RunGenAllBat();

            AssetDatabase.Refresh();

            Debug.Log("Luban gen_all finished.");
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);
        }
    }

    [MenuItem("Tools/Luban/Only Update Bean Xml")]
    public static void OnlyUpdateBeanXml()
    {
        try
        {
            int updateCount = UpdateAllXml();
            AssetDatabase.Refresh();

            Debug.Log("Luban bean xml update finished. Updated xml count: " + updateCount);
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);
        }
    }

    [MenuItem("Tools/Luban/Only Run Gen All")]
    public static void OnlyRunGenAll()
    {
        try
        {
            RunGenAllBat();
            AssetDatabase.Refresh();

            Debug.Log("Luban gen_all finished.");
        }
        catch (Exception exception)
        {
            Debug.LogError(exception);
        }
    }

    private static int UpdateAllXml()
    {
        if (!Directory.Exists(ExcelDir))
        {
            throw new DirectoryNotFoundException("Excel dir not found: " + ExcelDir);
        }

        if (!Directory.Exists(XmlDir))
        {
            throw new DirectoryNotFoundException("Xml dir not found: " + XmlDir);
        }

        string[] xmlFiles = Directory.GetFiles(XmlDir, "*.xml", SearchOption.AllDirectories);

        int updatedCount = 0;

        for (int i = 0; i < xmlFiles.Length; i++)
        {
            string xmlFile = xmlFiles[i];
            string fileName = Path.GetFileName(xmlFile);

            if (string.Equals(fileName, "__root__.xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool updated = UpdateXmlFile(xmlFile);

            if (updated)
            {
                updatedCount++;
            }
        }

        return updatedCount;
    }

    private static bool UpdateXmlFile(string xmlPath)
    {
        XDocument document = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);

        XElement root = document.Root;

        if (root == null)
        {
            return false;
        }

        List<XElement> tableElements = root
            .Descendants("table")
            .ToList();

        if (tableElements.Count == 0)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < tableElements.Count; i++)
        {
            XElement tableElement = tableElements[i];

            string beanName = tableElement.Attribute("value")?.Value?.Trim();
            string input = tableElement.Attribute("input")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(beanName) || string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            string excelPath = Path.Combine(ExcelDir, input);

            if (!File.Exists(excelPath))
            {
                Debug.LogWarning("Excel not found. Xml: " + xmlPath + ", input: " + input);
                continue;
            }

            List<FieldInfo> fields = ReadExcelFields(excelPath);

            if (fields.Count == 0)
            {
                Debug.LogWarning("No fields found. Excel: " + excelPath);
                continue;
            }

            XElement beanElement = root
                .Descendants("bean")
                .FirstOrDefault(x => string.Equals(x.Attribute("name")?.Value, beanName, StringComparison.Ordinal));

            if (beanElement == null)
            {
                Debug.LogWarning("Bean not found. Xml: " + xmlPath + ", bean: " + beanName);
                continue;
            }

            ReplaceBeanVars(beanElement, fields);

            changed = true;

            Debug.Log("Updated bean. Xml: " + xmlPath + ", bean: " + beanName + ", excel: " + input);
        }

        if (!changed)
        {
            return false;
        }

        Backup(xmlPath);
        SaveXml(document, xmlPath);

        return true;
    }

    private static List<FieldInfo> ReadExcelFields(string excelPath)
    {
        XlsxSheetData sheetData = XlsxReader.ReadFirstSheet(excelPath);

        List<FieldInfo> fields = new List<FieldInfo>();

        int fieldNameRow = 1;
        int fieldTypeRow = 2;
        int fieldCommentRow = 3;

        int maxColumn = sheetData.GetMaxColumn(fieldNameRow);

        for (int column = 1; column <= maxColumn; column++)
        {
            string name = sheetData.GetCell(fieldNameRow, column);
            string type = sheetData.GetCell(fieldTypeRow, column);
            string comment = sheetData.GetCell(fieldCommentRow, column);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (name.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                Debug.LogWarning("Skip field without type. Excel: " + excelPath + ", field: " + name);
                continue;
            }

            FieldInfo field = new FieldInfo();
            field.Name = name.Trim();
            field.Type = NormalizeType(type.Trim());
            field.Comment = comment.Trim();

            fields.Add(field);
        }

        return fields;
    }

    private static void ReplaceBeanVars(XElement beanElement, List<FieldInfo> fields)
    {
        beanElement.RemoveNodes();

        beanElement.Add(new XText(Environment.NewLine));

        for (int i = 0; i < fields.Count; i++)
        {
            FieldInfo field = fields[i];

            beanElement.Add(new XText("    "));

            XElement varElement = new XElement("var");
            varElement.SetAttributeValue("name", field.Name);
            varElement.SetAttributeValue("type", field.Type);

            if (!string.IsNullOrWhiteSpace(field.Comment))
            {
                varElement.SetAttributeValue("comment", field.Comment);
            }

            beanElement.Add(varElement);
            beanElement.Add(new XText(Environment.NewLine));
        }

        beanElement.Add(new XText("  "));
    }

    private static string NormalizeType(string type)
    {
        string value = type.Trim();

        return value switch
        {
            "Int" => "int",
            "INT" => "int",
            "integer" => "int",
            "Integer" => "int",

            "Long" => "long",
            "LONG" => "long",

            "Float" => "float",
            "FLOAT" => "float",

            "Double" => "double",
            "DOUBLE" => "double",

            "Bool" => "bool",
            "BOOL" => "bool",
            "boolean" => "bool",
            "Boolean" => "bool",

            "String" => "string",
            "STRING" => "string",

            _ => value
        };
    }

    private static void Backup(string xmlPath)
    {
        string backupRoot = Path.Combine(ConfigRoot, "_xml_backup");

        if (!Directory.Exists(backupRoot))
        {
            Directory.CreateDirectory(backupRoot);
        }

        string relativePath = MakeRelativePath(XmlDir, xmlPath);
        string backupPath = Path.Combine(backupRoot, relativePath + ".bak");

        string backupDir = Path.GetDirectoryName(backupPath);

        if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        File.Copy(xmlPath, backupPath);
    }

    private static string MakeRelativePath(string rootDir, string fullPath)
    {
        Uri rootUri = new Uri(AppendDirectorySeparatorChar(rootDir));
        Uri fullUri = new Uri(fullPath);

        string relativePath = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fullUri).ToString());
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparatorChar(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static void SaveXml(XDocument document, string path)
    {
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Encoding = new UTF8Encoding(false);
        settings.Indent = true;
        settings.NewLineChars = "\n";
        settings.OmitXmlDeclaration = false;

        using FileStream stream = File.Create(path);
        using XmlWriter writer = XmlWriter.Create(stream, settings);

        document.Save(writer);
    }

    private static void RunGenAllBat()
    {
        if (!File.Exists(GenAllBat))
        {
            throw new FileNotFoundException("gen_all.bat not found: " + GenAllBat);
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = "/c \"" + GenAllBat + "\"";
        startInfo.WorkingDirectory = ConfigRoot;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        using Process process = new Process();
        process.StartInfo = startInfo;

        StringBuilder outputBuilder = new StringBuilder();
        StringBuilder errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                outputBuilder.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                errorBuilder.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        string output = outputBuilder.ToString();
        string error = errorBuilder.ToString();

        if (!string.IsNullOrWhiteSpace(output))
        {
            Debug.Log(output);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Debug.LogWarning(error);
        }

        if (process.ExitCode != 0)
        {
            throw new Exception("gen_all.bat failed. ExitCode: " + process.ExitCode);
        }
    }

    private sealed class FieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    private sealed class XlsxSheetData
    {
        private readonly Dictionary<int, Dictionary<int, string>> cells = new Dictionary<int, Dictionary<int, string>>();

        public void SetCell(int row, int column, string value)
        {
            if (!cells.TryGetValue(row, out Dictionary<int, string> rowCells))
            {
                rowCells = new Dictionary<int, string>();
                cells.Add(row, rowCells);
            }

            rowCells[column] = value;
        }

        public string GetCell(int row, int column)
        {
            if (!cells.TryGetValue(row, out Dictionary<int, string> rowCells))
            {
                return string.Empty;
            }

            if (!rowCells.TryGetValue(column, out string value))
            {
                return string.Empty;
            }

            return value ?? string.Empty;
        }

        public int GetMaxColumn(int row)
        {
            if (!cells.TryGetValue(row, out Dictionary<int, string> rowCells))
            {
                return 0;
            }

            if (rowCells.Count == 0)
            {
                return 0;
            }

            return rowCells.Keys.Max();
        }
    }

    private static class XlsxReader
    {
        public static XlsxSheetData ReadFirstSheet(string xlsxPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(xlsxPath);

            List<string> sharedStrings = ReadSharedStrings(archive);

            string sheetPath = FindFirstSheetPath(archive);

            ZipArchiveEntry sheetEntry = archive.GetEntry(sheetPath);

            if (sheetEntry == null)
            {
                throw new FileNotFoundException("Sheet xml not found in xlsx: " + sheetPath);
            }

            XlsxSheetData sheetData = new XlsxSheetData();

            using Stream stream = sheetEntry.Open();
            XDocument document = XDocument.Load(stream);

            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            IEnumerable<XElement> rows = document
                .Descendants(ns + "sheetData")
                .Descendants(ns + "row");

            foreach (XElement rowElement in rows)
            {
                int rowIndex = ParseInt(rowElement.Attribute("r")?.Value, 0);

                if (rowIndex <= 0)
                {
                    continue;
                }

                IEnumerable<XElement> cellElements = rowElement.Elements(ns + "c");

                foreach (XElement cellElement in cellElements)
                {
                    string cellRef = cellElement.Attribute("r")?.Value;

                    if (string.IsNullOrWhiteSpace(cellRef))
                    {
                        continue;
                    }

                    int columnIndex = ColumnNameToIndex(GetColumnName(cellRef));

                    if (columnIndex <= 0)
                    {
                        continue;
                    }

                    string value = ReadCellValue(cellElement, ns, sharedStrings);

                    sheetData.SetCell(rowIndex, columnIndex, value);
                }
            }

            return sheetData;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            List<string> result = new List<string>();

            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");

            if (entry == null)
            {
                return result;
            }

            using Stream stream = entry.Open();
            XDocument document = XDocument.Load(stream);

            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            IEnumerable<XElement> items = document.Descendants(ns + "si");

            foreach (XElement item in items)
            {
                StringBuilder builder = new StringBuilder();

                IEnumerable<XElement> textElements = item.Descendants(ns + "t");

                foreach (XElement textElement in textElements)
                {
                    builder.Append(textElement.Value);
                }

                result.Add(builder.ToString());
            }

            return result;
        }

        private static string FindFirstSheetPath(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookEntry == null || relsEntry == null)
            {
                return "xl/worksheets/sheet1.xml";
            }

            XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            string firstSheetRelId;

            using (Stream workbookStream = workbookEntry.Open())
            {
                XDocument workbookDocument = XDocument.Load(workbookStream);

                XElement firstSheet = workbookDocument
                    .Descendants(mainNs + "sheet")
                    .FirstOrDefault();

                firstSheetRelId = firstSheet?.Attribute(relNs + "id")?.Value;
            }

            if (string.IsNullOrWhiteSpace(firstSheetRelId))
            {
                return "xl/worksheets/sheet1.xml";
            }

            using Stream relsStream = relsEntry.Open();
            XDocument relsDocument = XDocument.Load(relsStream);

            XElement relationship = relsDocument
                .Descendants(packageRelNs + "Relationship")
                .FirstOrDefault(x => string.Equals(x.Attribute("Id")?.Value, firstSheetRelId, StringComparison.Ordinal));

            string target = relationship?.Attribute("Target")?.Value;

            if (string.IsNullOrWhiteSpace(target))
            {
                return "xl/worksheets/sheet1.xml";
            }

            target = target.Replace("\\", "/");

            if (target.StartsWith("/"))
            {
                target = target.TrimStart('/');
            }
            else
            {
                target = "xl/" + target;
            }

            return target;
        }

        private static string ReadCellValue(XElement cellElement, XNamespace ns, List<string> sharedStrings)
        {
            string cellType = cellElement.Attribute("t")?.Value;

            if (cellType == "s")
            {
                string rawIndex = cellElement.Element(ns + "v")?.Value;

                int index = ParseInt(rawIndex, -1);

                if (index >= 0 && index < sharedStrings.Count)
                {
                    return sharedStrings[index];
                }

                return string.Empty;
            }

            if (cellType == "inlineStr")
            {
                XElement inlineString = cellElement.Element(ns + "is");

                if (inlineString == null)
                {
                    return string.Empty;
                }

                return string.Concat(inlineString.Descendants(ns + "t").Select(x => x.Value));
            }

            string value = cellElement.Element(ns + "v")?.Value;

            return value ?? string.Empty;
        }

        private static string GetColumnName(string cellRef)
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < cellRef.Length; i++)
            {
                char c = cellRef[i];

                if (char.IsLetter(c))
                {
                    builder.Append(c);
                }
                else
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private static int ColumnNameToIndex(string columnName)
        {
            int result = 0;

            for (int i = 0; i < columnName.Length; i++)
            {
                char c = char.ToUpperInvariant(columnName[i]);

                if (c < 'A' || c > 'Z')
                {
                    return 0;
                }

                result *= 26;
                result += c - 'A' + 1;
            }

            return result;
        }

        private static int ParseInt(string value, int defaultValue)
        {
            if (int.TryParse(value, out int result))
            {
                return result;
            }

            return defaultValue;
        }
    }
}

#endif