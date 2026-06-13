using AssetParser;
using Csv;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using UE4localizationsTool.Core.locres;

namespace UE4localizationsTool.Helper
{
    public class CSVFile
    {
        public static CSVFile Instance { get; } = new CSVFile();

        public char Delimiter { get; set; } = ',';
        public bool HasHeader { get; set; } = true;

        public void Load(NDataGridView dataGrid, string filePath)
        {
            int i = -1;
            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };
                foreach (var line in CsvReader.Read(textReader, options))
                {
                    ++i;
                    if (line.ColumnCount < 3)
                        continue;

                    if (!string.IsNullOrEmpty(line[2]))
                        dataGrid.SetValue(dataGrid.Rows[i].Cells["Text value"], line[2]);
                }
            }

        }

        public void LoadByKeys(NDataGridView dataGrid, string filePath)
        {
            var keyToRowIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in dataGrid.Rows)
            {
                if (row.Cells["Name"].Value != null)
                {
                    string gridKey = row.Cells["Name"].Value.ToString();
                    if (!keyToRowIndex.ContainsKey(gridKey))
                    {
                        keyToRowIndex.Add(gridKey, row.Index);
                    }
                }
            }

            var missingKeys = new List<string>();
            int totalImportedLines = 0;
            int skippedUntranslatedLines = 0;

            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };
                foreach (var line in CsvReader.Read(textReader, options))
                {
                    if (line.ColumnCount < 3 || line[0].StartsWith("#"))
                        continue;

                    if (!string.IsNullOrEmpty(line[2]))
                    {
                        totalImportedLines++;
                        string csvKey = line[0];

                        if (keyToRowIndex.TryGetValue(csvKey, out int rowIndex))
                        {
                            dataGrid.SetValue(dataGrid.Rows[rowIndex].Cells["Text value"], line[2]);
                        }
                        else
                        {
                            missingKeys.Add($"Key: {csvKey} | Text: {line[1]}");
                        }
                    }
                    else
                    {
                        skippedUntranslatedLines++;
                    }
                }
            }

            // Process results after import
            if (totalImportedLines == 0)
            {
                MessageBox.Show("No translation data found in the CSV file.", "Import Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Scenario A: NONE of the keys were found
            if (missingKeys.Count == totalImportedLines)
            {
                MessageBox.Show(
                    "Error: NONE of the keys from the CSV file were found in the table!\n\n" +
                    "Please check if the key format in your CSV matches the 'Name' column exactly (e.g. dots '.' vs colons '::').",
                    "Import Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            // Scenario B: SOME keys were found, SOME were missing
            if (missingKeys.Count > 0)
            {
                try
                {
                    File.WriteAllLines("missing_keys_log.txt", missingKeys);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not write missing_keys_log.txt: {ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                string message = $"Import finished successfully!\n" +
                         $"Total processed lines: {totalImportedLines - missingKeys.Count}\n" +
                         $"Skipped untranslated lines: {skippedUntranslatedLines}\n\n" +
                         $"{missingKeys.Count} keys were not found in the table.\n" +
                         string.Join("\n", missingKeys.Take(5));

                if (missingKeys.Count > 5)
                {
                    message += $"\n... and {missingKeys.Count - 5} more.";
                }

                message += "\n\n📂 The full list of missing keys has been saved to 'missing_keys_log.txt'.";

                MessageBox.Show(message, "Import Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (missingKeys.Count > 0)
            {
                string message = $"Import finished successfully!\n" +
                                 $"Total processed lines: {totalImportedLines}\n" +
                                 $"Skipped untranslated lines: {skippedUntranslatedLines}";

                MessageBox.Show(message, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void LoadNewLines(NDataGridView dataGrid, string filePath, LocresFile asset)
        {
            int totalImportedLines = 0;
            int skippedDuplicates = 0;
            var duplicateLogs = new List<string>();
            var processedCsvKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };
                System.Data.DataTable dt = (System.Data.DataTable)dataGrid.DataSource;

                foreach (var line in CsvReader.Read(textReader, options))
                {
                    if (string.IsNullOrEmpty(line[0]) || line[0].StartsWith("#") || line.ColumnCount < 2)
                        continue;

                    if (!string.IsNullOrEmpty(line[1]))
                    {
                        string RowName = line[0];       // Namespace::Key
                        string SourceText = line[1];     // Source

                        if (totalImportedLines == 0 && skippedDuplicates == 0 && RowName.Equals("key", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (processedCsvKeys.Contains(RowName))
                        {
                            skippedDuplicates++;
                            string currentTranslation = (line.ColumnCount > 2 && !string.IsNullOrEmpty(line[2])) ? line[2] : SourceText;
                            duplicateLogs.Add($"Key: {RowName} | Text: {SourceText}");
                            continue;
                        }

                        processedCsvKeys.Add(RowName);
                        totalImportedLines++;

                        string TranslationValue = (line.ColumnCount > 2 && !string.IsNullOrEmpty(line[2])) ? line[2] : SourceText;

                        var items = RowName.Split(new string[] { "::" }, StringSplitOptions.None);
                        string nameSpaceStr = "";
                        string keyStr = "";

                        if (items.Length == 2)
                        {
                            nameSpaceStr = items[0];
                            keyStr = items[1];
                        }
                        else
                        {
                            keyStr = items[0];
                        }

                        uint nameHash = asset.CalcHash(nameSpaceStr);
                        uint keyHash = asset.CalcHash(keyStr);
                        uint valueHash = asset.CalcHashForValue(SourceText);

                        var HashTable = new HashTable(nameHash, keyHash, valueHash);
                        asset.AddString(nameSpaceStr, keyStr, TranslationValue, nameHash, keyHash, valueHash);
                        dt.Rows.Add(RowName, TranslationValue, HashTable);
                    }
                }
            }

            if (skippedDuplicates > 0)
            {
                try
                {
                    File.WriteAllLines("duplicates_log.txt", duplicateLogs);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not write import_duplicates_log.txt: {ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                string message = $"Import finished!\n" +
                                 $"Total imported lines: {totalImportedLines}\n\n" +
                                 $"Duplicates skipped: {skippedDuplicates}\n" +
                                 string.Join("\n", duplicateLogs.Take(5));

                if (duplicateLogs.Count > 5)
                {
                    message += $"\n... and {duplicateLogs.Count - 5} more.";
                }

                message += "\n\n📂 The full list of duplicates has been saved to 'duplicates_log.txt'.";

                MessageBox.Show(message, "Import Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void LoadAndAddNewLines(NDataGridView dataGrid, string filePath, LocresFile locresFile)
        {
            if (!(dataGrid.DataSource is System.Data.DataTable dataTable))
            {
                MessageBox.Show("Error: DataGrid data source is not a DataTable.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var nameColumn = dataTable.Columns["Name"];
            var textValueColumn = dataTable.Columns["Text value"];
            var hashTableColumn = dataTable.Columns["Hash Table"];

            if (nameColumn == null || textValueColumn == null || hashTableColumn == null)
            {
                MessageBox.Show("Error: Required columns are missing.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var keyToRow = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dataTable.Rows)
            {
                if (row[nameColumn] != DBNull.Value && row[nameColumn] != null)
                {
                    string gridKey = row[nameColumn].ToString();
                    if (!keyToRow.ContainsKey(gridKey))
                    {
                        keyToRow.Add(gridKey, row);
                    }
                }
            }

            int updatedLines = 0;
            int addedLines = 0;
            int totalImportedLines = 0;
            int skippedDuplicates = 0;

            var duplicateLogs = new List<string>();
            var processedCsvKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            dataTable.BeginLoadData();

            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };

                foreach (var line in CsvReader.Read(textReader, options))
                {
                    if (line.ColumnCount < 2 || string.IsNullOrEmpty(line[0]) || line[0].StartsWith("#"))
                        continue;

                    string csvKey = line[0];
                    string csvSourceString = line[1];
                    string csvValue = (line.ColumnCount > 2 && !string.IsNullOrEmpty(line[2])) ? line[2] : csvSourceString;

                    if (totalImportedLines == 0 && skippedDuplicates == 0 && csvKey.Equals("key", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (processedCsvKeys.Contains(csvKey))
                    {
                        skippedDuplicates++;
                        duplicateLogs.Add($"Key: {csvKey} | Text: {csvSourceString}");
                        continue;
                    }

                    processedCsvKeys.Add(csvKey);
                    totalImportedLines++;

                    if (keyToRow.TryGetValue(csvKey, out DataRow existingRow))
                    {
                        existingRow[textValueColumn] = csvValue;

                        ParseKey(csvKey, out string nameSpaceStr, out string keyStr);
                        locresFile.AddString(nameSpaceStr, keyStr, csvValue);

                        updatedLines++;
                    }
                    else
                    {
                        ParseKey(csvKey, out string nameSpaceStr, out string keyStr);

                        uint nameSpaceHash = locresFile.CalcHash(nameSpaceStr);
                        uint keyHash = locresFile.CalcHash(keyStr);
                        uint valueHash = locresFile.CalcHashForValue(csvSourceString);

                        locresFile.AddString(nameSpaceStr, keyStr, csvValue, nameSpaceHash, keyHash, valueHash);

                        var newHashTable = new HashTable(nameSpaceHash, keyHash, valueHash);

                        DataRow newRow = dataTable.NewRow();
                        newRow[nameColumn] = csvKey;
                        newRow[textValueColumn] = csvValue;
                        newRow[hashTableColumn] = newHashTable;
                        dataTable.Rows.Add(newRow);

                        keyToRow[csvKey] = newRow;

                        addedLines++;
                    }
                }
            }

            dataTable.EndLoadData();

            string logPreview = string.Empty;
            if (skippedDuplicates > 0)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "duplicates_log.txt");
                File.WriteAllLines(logPath, duplicateLogs);

                var previewItems = duplicateLogs.Take(5).Select(x => $"  - {x}");
                logPreview = $"\nDuplicates skipped: {skippedDuplicates}\n" +
                             string.Join("\n", previewItems) +
                             (duplicateLogs.Count > 5 ? "\n  ...and others. The full list is saved in import_duplicates_log.txt" : "");

                MessageBox.Show(
                    $"Import finished successfully!\n\n" +
                    $"Updated existing rows: {updatedLines}\n" +
                    $"Added new rows (with hashes): {addedLines}\n" +
                    $"Total processed lines: {totalImportedLines}\n" +
                    logPreview,
                    "Done",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        private void ParseKey(string fullKey, out string nameSpace, out string key)
        {
            var parts = fullKey.Split(new string[] { "::" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                nameSpace = parts[0];
                key = parts[1];
            }
            else
            {
                nameSpace = "";
                key = parts[0];
            }
        }

        public void Save(DataGridView dataGrid, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                var rows = new List<string[]>();
                foreach (DataGridViewRow row in dataGrid.Rows)
                {
                    rows.Add(new[] { row.Cells["Name"].Value.ToString(), row.Cells["Text value"].Value.ToString(), "" });
                }
                CsvWriter.Write(writer, new string[] { "key", "source", "Translation" }, rows);
            }
        }

        public string[] Load(string filePath, bool NoNames = false)
        {
            var list = new List<string>();
            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };
                foreach (var line in CsvReader.Read(textReader, options))
                {
                    list.Add(Merge(line.Values, NoNames));
                }
            }

            return list.ToArray();
        }

        private string Merge(string[] strings, bool NoNames = false)
        {
            int i = 0;
            int CollsCount = !NoNames ? 2 : 3;
            string text = "";
            if (!NoNames && strings[i++] != "[~PATHFile~]")
            {
                text += strings[i - 1] + "=";
            }
            else
            {
                return strings[i];
            }

            if (strings.Length < CollsCount || string.IsNullOrEmpty(strings.LastOrDefault()))
            {
                text += strings[i++];
            }
            else
            {
                text += strings.LastOrDefault();
            }

            return text;

        }

        public void Save(List<List<string>> Strings, string filePath, bool NoNames = true)
        {
            using (var writer = new StreamWriter(filePath))
            {
                var rows = Strings.Select(x => NoNames ? new string[] { x[1], "" } : new string[] { x[0], x[1], "" });
                CsvWriter.Write(writer, NoNames ? new string[] { "source", "Translation" } : new string[] { "key", "source", "Translation" }, rows);
            }
        }
    }

}
