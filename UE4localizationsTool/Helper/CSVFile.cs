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

            // Read CSV and update DataGridView based on keys
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

                        // Try to find the key in the DataGridView and update the translation
                        if (keyToRowIndex.TryGetValue(csvKey, out int rowIndex))
                        {
                            dataGrid.SetValue(dataGrid.Rows[rowIndex].Cells["Text value"], line[2]);
                        }
                        else
                        {
                            missingKeys.Add(csvKey);
                        }
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
                    File.WriteAllLines("missing_keys.txt", missingKeys);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not write missing_keys.txt: {ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                string message = $"Import finished, but {missingKeys.Count} keys were not found in the table.\n\n" +
                                 "First few missing keys:\n" +
                                 string.Join("\n", missingKeys.Take(5));

                if (missingKeys.Count > 5)
                {
                    message += $"\n... and {missingKeys.Count - 5} more.";
                }

                message += "\n\n📂 The full list of missing keys has been saved to 'missing_keys.txt'.";

                MessageBox.Show(message, "Import Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void LoadNewLines(NDataGridView dataGrid, string filePath, LocresFile asset)
        {
            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };
                System.Data.DataTable dt = (System.Data.DataTable)dataGrid.DataSource;

                foreach (var line in CsvReader.Read(textReader, options))
                {
                    if (!string.IsNullOrEmpty(line[1]))
                    {
                        string RowName = line[0];       // Namespace::Key
                        string SourceText = line[1];     // Source

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
