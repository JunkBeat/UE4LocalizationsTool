﻿using AssetParser;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace UE4localizationsTool
{
    public partial class FrmMain : Form
    {
        struct DataRow
        {
          public  int Index;
          public string StringValue;
        }


        Uasset Uasset;
        Uexp Uexp;
        locres locres;
        String ToolName = Application.ProductName + " v" + Application.ProductVersion;
        string FilePath = "";
        FrmState state;
        Stack<DataRow> BackupDataUndo;
        Stack<DataRow> BackupDataRedo;
        int BackupDataIndex = 0;
        List<List<string>> ListrefValues;

        public FrmMain()
        {
            InitializeComponent();
            this.Text = ToolName;
            this.saveToolStripMenuItem.Enabled = false;
            this.exportAllTextToolStripMenuItem.Enabled = false;
            this.importAllTextToolStripMenuItem.Enabled = false;
            this.undoToolStripMenuItem.Enabled = false;
            this.redoToolStripMenuItem.Enabled = false;
            this.filterToolStripMenuItem.Enabled = false;
            this.StateLabel.Text = "";
            this.BackupDataUndo = new Stack<DataRow>();
            this.BackupDataRedo = new Stack<DataRow>();
            this.dataGridView1.RowsAdded += (x, y) => this.UpdateCounter();
            this.dataGridView1.RowsRemoved += (x, y) => this.UpdateCounter();
            this.DataCount.Text = "";
        }

        private void AddToDataView()
        {

            if (ListrefValues == null) return;
            int Index = 0;
            foreach (var item in ListrefValues)
            {
                dataGridView1.Rows.Add(item[0], item[1],Index);
                //dataGridView1.Rows[Index].Cells[1].Style.WrapMode = DataGridViewTriState.True;
                //dataGridView1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; 
                Index++;
            }
            dataGridView1.AutoResizeRows();
        }
        private void OpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All localizations files|*.uasset;*.locres|Uasset File|*.uasset|Locres File|*.locres";
            ofd.Title = "Open localizations File";


            if (ofd.ShowDialog() == DialogResult.OK)
            {

                LoadFile(ofd.FileName);
            }
        }

        private async void LoadFile(string filePath)
        {

            this.dataGridView1.Rows.Clear();
            this.saveToolStripMenuItem.Enabled = false;
            this.exportAllTextToolStripMenuItem.Enabled = false;
            this.importAllTextToolStripMenuItem.Enabled = false;
            this.undoToolStripMenuItem.Enabled = false;
            this.redoToolStripMenuItem.Enabled = false;
            this.filterToolStripMenuItem.Enabled = false;
            this.FilePath = "";
            this.StateLabel.Text = "";
            this.DataCount.Text = "";
            this.Text = ToolName;
            this.BackupDataUndo = new Stack<DataRow>();
            this.BackupDataRedo = new Stack<DataRow>();
            try
            {
                state = new FrmState(this, "loading File", "loading File please wait...");
                this.BeginInvoke(new Action(() => state.ShowDialog()));


                if (filePath.ToLower().EndsWith(".locres"))
                {
                    locres = await Task.Run(() => new locres(filePath));
                    ListrefValues = locres.Strings;
                    AddToDataView();

                }
                else if (filePath.ToLower().EndsWith(".uasset"))
                {
                    Uasset = await Task.Run(() => new Uasset(filePath));
                    Uexp = await Task.Run(() => new Uexp(Uasset));
                    ListrefValues = Uexp.Strings;
                    AddToDataView();
                    if (!Uexp.IsGood)
                    {
                        StateLabel.Text = "Warning: This file is't fully parsed and may not contain some text.";
                    }

                }
                this.undoToolStripMenuItem.Enabled = true;
                this.redoToolStripMenuItem.Enabled = true;
                this.saveToolStripMenuItem.Enabled = true;
                this.exportAllTextToolStripMenuItem.Enabled = true;
                this.importAllTextToolStripMenuItem.Enabled = true;
                this.filterToolStripMenuItem.Enabled = true;
                this.FilePath = filePath;
                this.Text = ToolName + " - " + Path.GetFileName(FilePath);
                state.Close();
            }
            catch (Exception ex)
            {
                state.Close();
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void exportAllTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] DataGridStrings = new string[dataGridView1.Rows.Count];
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                DataGridStrings[i] = dataGridView1.Rows[i].Cells[1].Value.ToString();
            }
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text File|*.txt";
            sfd.Title = "Export All Text";
            sfd.FileName = Path.GetFileName(FilePath) + ".txt";


            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    System.IO.File.WriteAllLines(sfd.FileName, DataGridStrings);
                    MessageBox.Show("Successful export!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch
                {
                    MessageBox.Show("Can't write export file!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            }
        }

        private void importAllTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Text File|*.txt";
            ofd.Title = "Import All Text";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string[] DataGridStrings;
                try
                {
                    DataGridStrings = System.IO.File.ReadAllLines(ofd.FileName);
                }
                catch
                {
                    MessageBox.Show("Can't read file or this file is using in Another process", "File is corrupted", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (DataGridStrings.Length < dataGridView1.Rows.Count)
                {
                    MessageBox.Show("This file does't contain enough strings for reimport", "Out of range", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                for (int n = 0; n < dataGridView1.Rows.Count; n++)
                {
                    dataGridView1.Rows[n].Cells[1].Value = DataGridStrings[n];
                }
                MessageBox.Show("Successful import!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);


            }



        }

        private async void SaveFile(object sender, EventArgs e)
        {

            SaveFileDialog sfd = new SaveFileDialog();
            if (FilePath.ToLower().EndsWith(".locres"))
            {
                sfd.Filter = "locres File|*.locres";
            }
            else if (FilePath.ToLower().EndsWith(".uasset"))
            {
                sfd.Filter = "Uasset File|*.uasset";
            }
            sfd.Title = "Save localizations file";
            sfd.FileName = Path.GetFileNameWithoutExtension(FilePath) + "_NEW";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
               try
                {
                  state = new FrmState(this, "Saving File", "Saving File please wait...");
                  this.BeginInvoke(new Action(() => state.ShowDialog()));
                  if (FilePath.ToLower().EndsWith(".locres"))
                  {
                      await Task.Run(() => locres.SaveFile(sfd.FileName));
                  
                  }
                  else if (FilePath.ToLower().EndsWith(".uasset"))
                  {
                  
                      await Task.Run(() => Uexp.SaveFile(sfd.FileName));
                  }
                }
                catch (Exception ex)
                {
                   MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                state.Close();
            }
        }

        private void SearchHide_Click(object sender, EventArgs e)
        {
            SearchPanal.Visible = false;
        }

        private void search_Click(object sender, EventArgs e)
        {
            SearchPanal.Visible = !SearchPanal.Visible;
            if (SearchPanal.Visible)
            {
                InputSearch.Focus();
                InputSearch.SelectAll();
            }
        }

        List<int> FindArray = new List<int>();
        int FindIndex = 0;
        string OldFind = "";
        private void Find_Click(object sender, EventArgs e)
        {
            FindArray.Clear();
            OldFind = InputSearch.Text;
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                if (dataGridView1.Rows[i].Cells[1].Value.ToString().ToLower().Contains(InputSearch.Text.ToLower()))
                {
                    FindArray.Add(i);
                }
            }

            if (FindArray.Count == 0|| InputSearch.Text=="")
            {
                MessageBox.Show($"can't find '{InputSearch.Text}'", "No results", MessageBoxButtons.OK, MessageBoxIcon.Information);
                FindArray.Clear();
                searchcount.Text = "found: " + FindArray.Count;
                return;
            }
            dataGridView1.ClearSelection();
            dataGridView1.Rows[FindArray[0]].Selected = true;
            dataGridView1.FirstDisplayedScrollingRowIndex = FindArray[0];
            FindIndex = 0;
            searchcount.Text = "found: "+ FindArray.Count;
        }

        private void FindNext_Click(object sender, EventArgs e)
        {
            if (FindArray.Count == 0 || OldFind != InputSearch.Text)
            {
                Find_Click(sender, e);
                return;
            }
            FindIndex++;
            if (FindIndex < FindArray.Count)
            {
                dataGridView1.ClearSelection();
                dataGridView1.Rows[FindArray[FindIndex]].Selected = true;
                dataGridView1.FirstDisplayedScrollingRowIndex = FindArray[FindIndex];
            }
            else
            {
                FindIndex = 0;
                dataGridView1.ClearSelection();
                dataGridView1.Rows[FindArray[FindIndex]].Selected = true;
                dataGridView1.FirstDisplayedScrollingRowIndex = FindArray[FindIndex];
            }
        }

        private void FindPrevious_Click(object sender, EventArgs e)
        {
            if (FindArray.Count == 0 || OldFind != InputSearch.Text)
            {
                Find_Click(sender, e);
                return;
            }
            FindIndex--;
            if (FindIndex > -1)
            {
                dataGridView1.ClearSelection();
                dataGridView1.Rows[FindArray[FindIndex]].Selected = true;
                dataGridView1.FirstDisplayedScrollingRowIndex = FindArray[FindIndex];
            }
            else
            {
                FindIndex = FindArray.Count - 1;
                dataGridView1.ClearSelection();
                dataGridView1.Rows[FindArray[FindIndex]].Selected = true;
                dataGridView1.FirstDisplayedScrollingRowIndex = FindArray[FindIndex];
            }
        }

        private void dataGridView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(this, new Point(e.X + ((Control)sender).Left, e.Y + ((Control)sender).Top));

            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
                if (dataGridView1.SelectedCells.Count > 0)
                Clipboard.SetText(dataGridView1.SelectedCells[0].Value.ToString());
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedCells.Count > 0)
            {
                if (BackupDataUndo.Count == 0)
                {
                    BackupDataRedo.Clear();
                }
                BackupDataUndo.Push(new DataRow() { Index = dataGridView1.SelectedCells[0].RowIndex, StringValue = dataGridView1.Rows[dataGridView1.SelectedCells[0].RowIndex].Cells[1].Value != null ? dataGridView1.Rows[dataGridView1.SelectedCells[0].RowIndex].Cells[1].Value.ToString() : "" });
                dataGridView1.Rows[dataGridView1.SelectedCells[0].RowIndex].Cells[1].Value = Clipboard.GetText();              
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void InputSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (!InputSearch.Focused)
            {
               InputSearch.Focus();  
            }
            
            if (e.KeyCode == Keys.Enter)
            {
                FindNext_Click(sender, e);
            }
            
        }

        private void dataGridView1_Scroll(object sender, ScrollEventArgs e)
        {
            dataGridView1.ClearSelection();
            dataGridView1.Rows[dataGridView1.FirstDisplayedScrollingRowIndex].Selected = true;
        }

        private void FrmMain_DragDrop(object sender, DragEventArgs e)
        {
            string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
            LoadFile(array[0]);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1.Created)
            {
                ListrefValues[int.Parse(dataGridView1.Rows[e.RowIndex].Cells[2].Value.ToString())][1] = dataGridView1.Rows[e.RowIndex].Cells[1].Value.ToString();
                dataGridView1.Rows[e.RowIndex].Cells[1].Style.BackColor = System.Drawing.Color.FromArgb(255, 204, 153);
            }
        }

        private void fontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Dialog Font select
            FontDialog fd = new FontDialog();
            fd.Font = dataGridView1.Font;
            if (fd.ShowDialog() == DialogResult.OK)
            {
                dataGridView1.Font = fd.Font;
                dataGridView1.AutoResizeRows();
            }
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            
            if (dataGridView1.CancelEdit()&& dataGridView1.Focused)
            {
                if (e.KeyCode == Keys.V && e.Control)
                {
                    pasteToolStripMenuItem_Click(sender, e);
                }               
                else if (e.KeyCode == Keys.Z && e.Control)
                {
                    undoToolStripMenuItem_Click(sender, e);

                }
                else if ((e.KeyCode == Keys.Y && e.Control)|| (e.KeyCode == Keys.Z && e.Control && e.Shift))
                {
                    redoToolStripMenuItem_Click(sender, e);
                }


                else if (e.KeyCode == Keys.L && e.Control && e.Alt)
                {
                    dataGridView1.RightToLeft = RightToLeft.No;
                }
                else if (e.KeyCode == Keys.R && e.Control && e.Alt)
                {
                    dataGridView1.RightToLeft = RightToLeft.Yes;
                }
                

            }
        }

        private void rightToLeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dataGridView1.RightToLeft = dataGridView1.RightToLeft == RightToLeft.Yes ? RightToLeft.No : RightToLeft.Yes;
        }


        private void dataGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (BackupDataUndo.Count == 0)
            {
                BackupDataRedo.Clear();
            }
            BackupDataUndo.Push(new DataRow() { Index = e.RowIndex, StringValue = dataGridView1.Rows[e.RowIndex].Cells[1].Value.ToString()});
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (BackupDataUndo.Count > 0)
            {

                DataRow dataRow = BackupDataUndo.Pop();
                BackupDataRedo.Push(new DataRow() { Index = dataRow.Index, StringValue = dataGridView1.Rows[dataRow.Index].Cells[1].Value != null ? dataGridView1.Rows[dataRow.Index].Cells[1].Value.ToString() : "" });

                //MessageBox.Show(dataRow.StringValue);
                dataGridView1.Rows[dataRow.Index].Cells[1].Value = dataRow.StringValue;
                if (dataRow.StringValue == ListrefValues[dataRow.Index][1])
                    dataGridView1.Rows[dataRow.Index].Cells[1].Style.BackColor = System.Drawing.Color.FromArgb(255, 255, 255);
                else
                {
                    dataGridView1.Rows[dataRow.Index].Cells[1].Style.BackColor = System.Drawing.Color.FromArgb(255, 204, 153);
                }
                dataGridView1.ClearSelection();
                dataGridView1.Rows[dataRow.Index].Selected = true;
            }

        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (BackupDataRedo.Count > 0)
            {
                //MessageBox.Show(BackupDataRedo.Peek().StringValue);
              
                DataRow dataRow = BackupDataRedo.Pop();
                BackupDataUndo.Push(new DataRow() { Index = dataRow.Index, StringValue = dataGridView1.Rows[dataRow.Index].Cells[1].Value != null ? dataGridView1.Rows[dataRow.Index].Cells[1].Value.ToString() : "" });
                dataGridView1.Rows[dataRow.Index].Cells[1].Value = dataRow.StringValue;
                if (dataRow.StringValue == ListrefValues[dataRow.Index][1])
                    dataGridView1.Rows[dataRow.Index].Cells[1].Style.BackColor = System.Drawing.Color.FromArgb(255, 255, 255);
                else
                {
                    dataGridView1.Rows[dataRow.Index].Cells[1].Style.BackColor = System.Drawing.Color.FromArgb(255, 204, 153);
                }

                dataGridView1.ClearSelection();
                dataGridView1.Rows[dataRow.Index].Selected = true;
            }
        }

        private void commandLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Program.commandlines, "Command Lines",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            new FrmAbout(this).ShowDialog();
        }



        private void byNameToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (ListrefValues==null)
            {
                return;
            }

            FrmFilter frmFilter = new FrmFilter(this);
            frmFilter.Text = "Filter by name";
           
          
            if (frmFilter.ShowDialog() == DialogResult.OK)
            {   

                dataGridView1.Rows.Clear();
                for (int x = 0; x < ListrefValues.Count; x++)
                {
                    if (frmFilter.UseMatching)
                    {
                        frmFilter.ArrayValues.ForEach(Value =>
                        {
                            if (frmFilter.RegularExpression)
                            {
                                try
                                {
                                    if (Regex.IsMatch(ListrefValues[x][0], Value))
                                    {
                                        dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                if (ListrefValues[x][0] == Value)
                                {
                                    dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                }
                            }
                        });

                    }
                    else
                    {
                        frmFilter.ArrayValues.ForEach(Value =>
                        {
                            if (frmFilter.RegularExpression)
                            {
                                try
                                {
                                    if (Regex.IsMatch(ListrefValues[x][0], Value, RegexOptions.IgnoreCase))
                                    {
                                        dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                if (ListrefValues[x][0].ToLower().Contains(Value.ToLower()))
                                {
                                    dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                }
                            }
                        });
                    }
                }
            }
        }

        private void clearFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dataGridView1.Rows.Clear();
            AddToDataView();
        }

        private void byValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ListrefValues == null)
            {
                return;
            }
            FrmFilter frmFilter = new FrmFilter(this);
            frmFilter.Text = "Filter by value";
            if (frmFilter.ShowDialog() == DialogResult.OK)
            {
                dataGridView1.Rows.Clear();

                for (int x = 0; x < ListrefValues.Count; x++)
                {
                    if (frmFilter.UseMatching)
                    {
                        frmFilter.ArrayValues.ForEach(Value =>
                        {
                            if (frmFilter.RegularExpression)
                            {
                                try
                                {
                                    if (Regex.IsMatch(ListrefValues[x][1], Value))
                                    {
                                        dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                if (ListrefValues[x][1]==Value)
                                {
                                    dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                }
                            }
                        });

                    }
                    else
                    {
                        frmFilter.ArrayValues.ForEach(Value =>
                        {
                            if (frmFilter.RegularExpression)
                            {
                                try
                                {
                                    if (Regex.IsMatch(ListrefValues[x][1], Value, RegexOptions.IgnoreCase))
                                    {
                                        dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                if (ListrefValues[x][1].ToLower().Contains(Value.ToLower()))
                                {
                                    dataGridView1.Rows.Add(ListrefValues[x][0], ListrefValues[x][1], x);
                                }
                            }
                        });
                    }
                }
            }
        }

        private void UpdateCounter()
        {
            DataCount.Text = "Text count: " + dataGridView1.Rows.Count;
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
