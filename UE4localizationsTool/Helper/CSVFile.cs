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
            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };
                foreach (var line in CsvReader.Read(textReader, options))
                {
                    if (line.ColumnCount < 3 || line[0].StartsWith("#"))
                        continue;

                    if (!string.IsNullOrEmpty(line[2]))
                    {
                        // Find the line with the desired name
                        int rowIndex = -1;

                        foreach (DataGridViewRow row in dataGrid.Rows)
                        {
                            if (row.Cells["Name"].Value != null && row.Cells["Name"].Value.ToString() == line[0])
                            {
                                rowIndex = row.Index;
                                break;
                            }
                        }

                        if (rowIndex != -1)
                        {
                            dataGrid.SetValue(dataGrid.Rows[rowIndex].Cells["Text value"], line[2]);
                        }
                        else
                        {
                            Console.WriteLine("Matching line not found: ", line[0]);
                            continue;
                        }

                        
                    }
                        
                }
            }

        }

        public void LoadNewLines(NDataGridView dataGrid, string filePath, LocresFile asset)
        {
            using (var textReader = new StreamReader(filePath))
            {
                var options = new CsvOptions() { AllowNewLineInEnclosedFieldValues = true };
                foreach (var line in CsvReader.Read(textReader, options))
                {
                    if (!string.IsNullOrEmpty(line[1]))
                    {
                        System.Data.DataTable dt = (System.Data.DataTable)dataGrid.DataSource;

                        string RowName = line[0];
                        string Value = line[1];

                        var HashTable = new HashTable()
                        {
                            NameHash  = 0, // empty NameSpace
                            KeyHash   = 0, // usually always 0
                            ValueHash = asset.CalcHashExperimental(Value) // hash generated from original string
                        };

                        // If there is a translation, add it to the table
                        if (line.ColumnCount > 2 && !string.IsNullOrEmpty(line[2]))
                        {
                            Value = line[2];
                        }

                        dt.Rows.Add(RowName, Value, HashTable); // RowName = NameSpace::Key
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
